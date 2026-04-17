using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Spawned at the landing point of Goedde's thrown book.
	/// Server: one-shot area damage burst on spawn.
	/// Client: glowing swirl portal rings on the floor + burst of flying pages.
	/// Auto-despawns after _despawnDelay seconds.
	/// </summary>
	[AddComponentMenu("Projectiles/Goedde Book Portal Impact")]
	public class GoeddeBookPortalImpact : ContextBehaviour
	{
		// ─── Inspector ────────────────────────────────────────────────────

		[SerializeField] private float _despawnDelay = 3f;

		[Header("Damage")]
		[SerializeField] private float _damageRadius  = 4f;
		[SerializeField] private float _centerDamage  = 70f;
		[SerializeField] private float _outerDamage   = 20f;
		[SerializeField] private LayerMask _targetMask = Physics.DefaultRaycastLayers;

		[Header("Portal Visuals")]
		[SerializeField] private Color _portalColorA = new Color(0.45f, 0.08f, 0.85f, 1f);
		[SerializeField] private Color _portalColorB = new Color(0.15f, 0.50f, 1.00f, 1f);
		[SerializeField] private Color _pageColor    = new Color(0.96f, 0.92f, 0.82f, 1f);
		[SerializeField] private float _portalRadius = 2.0f;

		// ─── Server State ─────────────────────────────────────────────────

		private TickTimer _despawnTimer;

		// ─── Client Visual State ──────────────────────────────────────────

		private bool          _visualsCreated;
		private float         _elapsedTime;
		private Vector3       _floorCenter;

		// Rings
		private LineRenderer _outerRing;
		private LineRenderer _innerRing;
		private LineRenderer _runeRing;
		private Material     _ringMat;

		// Pages — parallel lists
		private readonly List<GameObject> _pageGos        = new();
		private readonly List<Material>   _pageMats       = new();
		private readonly List<Vector3>    _pageVelocities = new();
		private readonly List<Vector3>    _pageAngVels    = new();
		private readonly List<float>      _pageAges       = new();
		private readonly List<float>      _pageMaxAges    = new();

		private Material _pageSharedMat;

		private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

		// ─── NetworkBehaviour ─────────────────────────────────────────────

		public override void Spawned()
		{
			if (HasStateAuthority)
			{
				ApplyDamageBurst();
				_despawnTimer = TickTimer.CreateFromSeconds(Runner, _despawnDelay);
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (!HasStateAuthority) return;
			if (_despawnTimer.Expired(Runner))
				Runner.Despawn(Object);
		}

		public override void Render()
		{
			if (!_visualsCreated)
			{
				// Snap the visual center to the floor directly below the spawn point.
				_floorCenter = SnapToFloor(transform.position);
				BuildVisuals();
				BurstInitialPages();
				_visualsCreated = true;
			}

			_elapsedTime += Time.deltaTime;

			float fadeStart = _despawnDelay - 0.8f;
			float alpha = _elapsedTime < fadeStart
				? 1f
				: 1f - Mathf.Clamp01((_elapsedTime - fadeStart) / 0.8f);

			UpdateRings(alpha);
			UpdatePages(alpha);
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			CleanupVisuals();
		}

		// ─── Server: Damage ───────────────────────────────────────────────

		private void ApplyDamageBurst()
		{
			ETeam instigatorTeam = ETeam.None;
			if (Object.InputAuthority != PlayerRef.None &&
			    Runner.TryGetPlayerObject(Object.InputAuthority, out var playerObj))
			{
				var player = playerObj.GetComponent<Player>();
				if (player != null) instigatorTeam = player.Team;
			}

			var hits = Physics.OverlapSphere(
				transform.position + Vector3.up * 0.5f,
				_damageRadius,
				_targetMask,
				QueryTriggerInteraction.Collide);

			foreach (var col in hits)
			{
				var agent = col.GetComponentInParent<PlayerAgent>();
				if (agent == null || agent.Owner == null || agent.Object == null)       continue;
				if (instigatorTeam != ETeam.None && agent.Owner.Team == instigatorTeam) continue;
				if (agent.Health == null || !agent.Health.IsAlive)                      continue;

				Vector3 rawDir = agent.transform.position - transform.position;
				float   dist   = rawDir.magnitude;
				Vector3 dir    = dist > 0.001f ? rawDir / dist : Vector3.up;

				float innerEdge = _damageRadius * 0.35f;
				float damage = dist <= innerEdge
					? _centerDamage
					: Mathf.Lerp(_centerDamage, _outerDamage, (dist - innerEdge) / (_damageRadius - innerEdge));

				var hitData = new HitData
				{
					Action        = EHitAction.Damage,
					Amount        = damage,
					Position      = agent.transform.position,
					Direction     = dir,
					Normal        = -dir,
					InstigatorRef = Object.InputAuthority,
					Target        = agent.Health,
					HitType       = EHitType.Explosion,
				};

				HitUtility.ProcessHit(ref hitData);
			}
		}

		// ─── Client: Portal Rings ─────────────────────────────────────────

		private static Vector3 SnapToFloor(Vector3 spawnPos)
		{
			if (Physics.Raycast(spawnPos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3f))
				return hit.point + Vector3.up * 0.04f;
			return spawnPos + Vector3.up * 0.04f;
		}

		private void BuildVisuals()
		{
			var shader = Shader.Find("Sprites/Default");
			if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
			if (shader == null) shader = Shader.Find("Standard");

			_ringMat      = new Material(shader) { color = _portalColorA };
			_pageSharedMat = new Material(shader) { color = _pageColor };

			_outerRing = MakeRing("BPImpactOuter", _floorCenter, _portalRadius,        48, 0.09f);
			_innerRing = MakeRing("BPImpactInner", _floorCenter, _portalRadius * 0.60f, 36, 0.06f);
			_runeRing  = MakeRing("BPImpactRune",  _floorCenter, _portalRadius * 0.32f, 24, 0.04f);
		}

		private LineRenderer MakeRing(string goName, Vector3 center, float radius, int segments, float width)
		{
			var go = new GameObject(goName);
			go.transform.position = center;

			var lr = go.AddComponent<LineRenderer>();
			lr.sharedMaterial    = _ringMat;
			lr.startWidth        = width;
			lr.endWidth          = width;
			lr.loop              = true;
			lr.useWorldSpace     = true;
			lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			lr.positionCount     = segments;

			SetRingPositions(lr, center, radius, segments, 0f);
			return lr;
		}

		private void UpdateRings(float globalAlpha)
		{
			if (_ringMat == null) return;

			float t   = 0.5f + 0.5f * Mathf.Sin(_elapsedTime * 5f);
			Color col = Color.Lerp(_portalColorA, _portalColorB, t);
			col.a = globalAlpha;
			_ringMat.color = col;

			if (_innerRing != null)
				SetRingPositions(_innerRing, _floorCenter, _portalRadius * 0.60f, 36,  _elapsedTime *  80f);
			if (_runeRing != null)
				SetRingPositions(_runeRing,  _floorCenter, _portalRadius * 0.32f, 24, -_elapsedTime * 130f);
		}

		private static void SetRingPositions(LineRenderer lr, Vector3 center, float radius, int segments, float angleDeg)
		{
			float offset = angleDeg * Mathf.Deg2Rad;
			for (int i = 0; i < segments; i++)
			{
				float a = i / (float)segments * Mathf.PI * 2f + offset;
				lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
			}
		}

		// ─── Client: Pages ────────────────────────────────────────────────

		private void BurstInitialPages()
		{
			for (int i = 0; i < 22; i++)
			{
				float angle   = Random.Range(0f, Mathf.PI * 2f);
				float speed   = Random.Range(4f, 8.5f);
				float upSpeed = Random.Range(2.5f, 7f);
				var vel = new Vector3(Mathf.Cos(angle) * speed, upSpeed, Mathf.Sin(angle) * speed);
				AddPage(vel, Random.Range(0.8f, 1.9f));
			}
		}

		private void AddPage(Vector3 velocity, float maxAge)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
			go.name = "BookPortalPage";
			var col = go.GetComponent<Collider>();
			if (col != null) Destroy(col);

			float w = Random.Range(0.12f, 0.25f);
			float h = Random.Range(0.17f, 0.33f);
			go.transform.localScale = new Vector3(w, h, 1f);
			go.transform.position   = _floorCenter + new Vector3(
				Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
			go.transform.rotation = Random.rotation;

			var mat  = new Material(_pageSharedMat);
			var rend = go.GetComponent<Renderer>();
			rend.sharedMaterial   = mat;
			rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

			_pageGos.Add(go);
			_pageMats.Add(mat);
			_pageVelocities.Add(velocity);
			_pageAngVels.Add(new Vector3(
				Random.Range(-450f, 450f),
				Random.Range(-220f, 220f),
				Random.Range(-220f, 220f)));
			_pageAges.Add(0f);
			_pageMaxAges.Add(maxAge);
		}

		private void UpdatePages(float globalAlpha)
		{
			const float gravity = -7f;

			for (int i = _pageGos.Count - 1; i >= 0; i--)
			{
				if (_pageGos[i] == null) { RemoveAt(i); continue; }

				float age    = _pageAges[i] + Time.deltaTime;
				float maxAge = _pageMaxAges[i];

				if (age >= maxAge) { DestroyAt(i); continue; }

				Vector3 vel = _pageVelocities[i];
				vel.y += gravity * Time.deltaTime;
				_pageVelocities[i] = vel;
				_pageAges[i]       = age;

				_pageGos[i].transform.position += vel * Time.deltaTime;
				_pageGos[i].transform.Rotate(_pageAngVels[i] * Time.deltaTime, Space.Self);

				float ageT = age / maxAge;
				Color c    = _pageColor;
				c.a        = (1f - ageT * ageT) * globalAlpha;
				_pageMats[i].color = c;
			}
		}

		private void RemoveAt(int i)
		{
			_pageGos.RemoveAt(i);
			_pageMats.RemoveAt(i);
			_pageVelocities.RemoveAt(i);
			_pageAngVels.RemoveAt(i);
			_pageAges.RemoveAt(i);
			_pageMaxAges.RemoveAt(i);
		}

		private void DestroyAt(int i)
		{
			Destroy(_pageGos[i]);
			Destroy(_pageMats[i]);
			RemoveAt(i);
		}

		private void CleanupVisuals()
		{
			for (int i = _pageGos.Count - 1; i >= 0; i--)
			{
				if (_pageGos[i] != null) Destroy(_pageGos[i]);
				if (_pageMats[i] != null) Destroy(_pageMats[i]);
			}
			_pageGos.Clear(); _pageMats.Clear(); _pageVelocities.Clear();
			_pageAngVels.Clear(); _pageAges.Clear(); _pageMaxAges.Clear();

			if (_outerRing     != null) { Destroy(_outerRing.gameObject);  _outerRing     = null; }
			if (_innerRing     != null) { Destroy(_innerRing.gameObject);  _innerRing     = null; }
			if (_runeRing      != null) { Destroy(_runeRing.gameObject);   _runeRing      = null; }
			if (_ringMat       != null) { Destroy(_ringMat);               _ringMat       = null; }
			if (_pageSharedMat != null) { Destroy(_pageSharedMat);         _pageSharedMat = null; }
		}
	}
}
