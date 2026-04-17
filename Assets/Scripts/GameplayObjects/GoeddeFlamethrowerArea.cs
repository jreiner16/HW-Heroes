using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// The book portal area spawned by Goedde's right-click ability when the thrown book lands.
	/// Visually: a glowing swirling circle on the floor that erupts flying pages.
	/// Mechanically: applies periodic area damage to enemies inside the portal radius.
	/// Automatically despawns after its duration.
	/// </summary>
	[AddComponentMenu("Projectiles/Goedde Book Portal Area")]
	public class GoeddeFlamethrowerArea : ContextBehaviour
	{
		// ─── Inspector ────────────────────────────────────────────────────

		[SerializeField, Tooltip("How long the portal stays active.")]
		private float _duration = 3f;

		[SerializeField, Tooltip("Damage per pulse applied to enemies inside the portal.")]
		private float _damagePerPulse = 8f;

		[SerializeField, Tooltip("Radius of the portal and damage area.")]
		private float _portalRadius = 1.5f;

		[SerializeField, Tooltip("Damage pulses per second.")]
		private int _pulsesPerSecond = 4;

		[SerializeField, Tooltip("Layers to scan for damage targets.")]
		private LayerMask _targetMask = Physics.DefaultRaycastLayers;

		[Header("Portal Visuals")]
		[SerializeField] private Color _portalColorA  = new Color(0.45f, 0.08f, 0.85f, 1f);
		[SerializeField] private Color _portalColorB  = new Color(0.15f, 0.50f, 1.00f, 1f);
		[SerializeField] private Color _pageColor     = new Color(0.96f, 0.92f, 0.82f, 1f);

		// ─── Networked State ─────────────────────────────────────────────

		[Networked] private TickTimer _pulseTimer   { get; set; }
		[Networked] private TickTimer _despawnTimer { get; set; }

		// ─── Client Visual State ──────────────────────────────────────────

		private bool  _visualsCreated;
		private float _elapsedTime;
		private float _continuousPageTimer;

		// Rings
		private LineRenderer _outerRing;
		private LineRenderer _innerRing;
		private LineRenderer _runeRing;
		private Material     _ringMat;

		// Pages — parallel lists (avoids struct copy issues)
		private readonly List<GameObject> _pageGos         = new List<GameObject>();
		private readonly List<Material>   _pageMats        = new List<Material>();
		private readonly List<Vector3>    _pageVelocities  = new List<Vector3>();
		private readonly List<Vector3>    _pageAngVels     = new List<Vector3>();
		private readonly List<float>      _pageAges        = new List<float>();
		private readonly List<float>      _pageMaxAges     = new List<float>();

		private Material _pageSharedMat; // base for cloning

		private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

		// ─── NetworkBehaviour ─────────────────────────────────────────────

		public override void Spawned()
		{
			if (!HasStateAuthority) return;

			float interval = 1f / Mathf.Max(1, _pulsesPerSecond);
			_pulseTimer   = TickTimer.CreateFromSeconds(Runner, interval);
			_despawnTimer = TickTimer.CreateFromSeconds(Runner, _duration);
		}

		public override void FixedUpdateNetwork()
		{
			if (!HasStateAuthority) return;

			if (_despawnTimer.Expired(Runner))
			{
				Runner.Despawn(Object);
				return;
			}

			if (_pulseTimer.ExpiredOrNotRunning(Runner))
			{
				ApplyDamagePulse();
				float interval = 1f / Mathf.Max(1, _pulsesPerSecond);
				_pulseTimer = TickTimer.CreateFromSeconds(Runner, interval);
			}
		}

		public override void Render()
		{
			if (!_visualsCreated)
			{
				BuildVisuals();
				BurstInitialPages();
				_visualsCreated = true;
			}

			_elapsedTime += Time.deltaTime;

			// Global alpha: fade out over the last 0.8 s
			float fadeStart = _duration - 0.8f;
			float alpha = _elapsedTime < fadeStart
				? 1f
				: 1f - Mathf.Clamp01((_elapsedTime - fadeStart) / 0.8f);

			UpdateRings(alpha);
			SpawnContinuousPages();
			UpdatePages(alpha);
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			CleanupVisuals();
		}

		// ─── Server: Area Damage ──────────────────────────────────────────

		private void ApplyDamagePulse()
		{
			ETeam instigatorTeam = ETeam.None;
			if (Object.InputAuthority != PlayerRef.None &&
			    Runner.TryGetPlayerObject(Object.InputAuthority, out var playerObj))
			{
				var player = playerObj.GetComponent<Player>();
				if (player != null) instigatorTeam = player.Team;
			}

			// Scan a short cylinder above the floor so players standing in the portal are hit
			var hits = Physics.OverlapSphere(
				transform.position + Vector3.up * 0.6f,
				_portalRadius,
				_targetMask,
				QueryTriggerInteraction.Collide);

			foreach (var col in hits)
			{
				var agent = col.GetComponentInParent<PlayerAgent>();
				if (agent == null || agent.Owner == null || agent.Object == null)   continue;
				if (instigatorTeam != ETeam.None && agent.Owner.Team == instigatorTeam) continue;
				if (agent.Health == null || !agent.Health.IsAlive)                  continue;

				Vector3 rawDir = agent.transform.position - transform.position;
				Vector3 dir    = rawDir.sqrMagnitude > 0.001f ? rawDir.normalized : Vector3.up;

				var hit = new HitData
				{
					Action        = EHitAction.Damage,
					Amount        = _damagePerPulse,
					Position      = agent.transform.position,
					Direction     = dir,
					Normal        = -dir,
					InstigatorRef = Object.InputAuthority,
					Target        = agent.Health,
					HitType       = EHitType.Explosion,
				};

				HitUtility.ProcessHit(ref hit);
			}
		}

		// ─── Client: Portal Rings ─────────────────────────────────────────

		private void BuildVisuals()
		{
			// Sprites/Default works in every URP version and supports alpha blending.
			var shader = Shader.Find("Sprites/Default");
			if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
			if (shader == null) shader = Shader.Find("Standard");

			_ringMat = new Material(shader) { color = _portalColorA };

			_pageSharedMat = new Material(shader) { color = _pageColor };

			Vector3 floorPos = transform.position + Vector3.up * 0.04f;
			_outerRing = MakeRing("BPOuterRing", floorPos, _portalRadius,        48, 0.09f);
			_innerRing = MakeRing("BPInnerRing", floorPos, _portalRadius * 0.60f, 36, 0.06f);
			_runeRing  = MakeRing("BPRuneRing",  floorPos, _portalRadius * 0.32f, 24, 0.04f);
		}

		private LineRenderer MakeRing(string goName, Vector3 center, float radius, int segments, float width)
		{
			var go = new GameObject(goName);
			go.transform.position = center;

			var lr = go.AddComponent<LineRenderer>();
			lr.sharedMaterial   = _ringMat;
			lr.startWidth       = width;
			lr.endWidth         = width;
			lr.loop             = true;
			lr.useWorldSpace    = true;
			lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			lr.positionCount    = segments;

			SetRingPositions(lr, center, radius, segments, 0f);
			return lr;
		}

		private void UpdateRings(float globalAlpha)
		{
			if (_ringMat == null) return;

			// Pulse between two colours
			float t     = 0.5f + 0.5f * Mathf.Sin(_elapsedTime * 5f);
			Color col   = Color.Lerp(_portalColorA, _portalColorB, t);
			col.a       = globalAlpha;
			_ringMat.color = col;

			// Rotate inner and rune rings
			Vector3 floorPos = transform.position + Vector3.up * 0.04f;
			if (_innerRing != null)
				SetRingPositions(_innerRing, floorPos, _portalRadius * 0.60f, 36,  _elapsedTime *  80f);
			if (_runeRing != null)
				SetRingPositions(_runeRing,  floorPos, _portalRadius * 0.32f, 24, -_elapsedTime * 130f);
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

		/// <summary>Initial burst of pages when the book lands.</summary>
		private void BurstInitialPages()
		{
			for (int i = 0; i < 18; i++)
			{
				float angle   = Random.Range(0f, Mathf.PI * 2f);
				float speed   = Random.Range(3.5f, 7f);
				float upSpeed = Random.Range(2f, 5.5f);
				var vel = new Vector3(Mathf.Cos(angle) * speed, upSpeed, Mathf.Sin(angle) * speed);
				AddPage(vel, Random.Range(0.7f, 1.6f));
			}
		}

		/// <summary>Trickle a few pages per second while the portal is active.</summary>
		private void SpawnContinuousPages()
		{
			_continuousPageTimer += Time.deltaTime;
			if (_continuousPageTimer < 0.28f || _elapsedTime > _duration - 0.5f)
				return;

			_continuousPageTimer = 0f;
			float angle = Random.Range(0f, Mathf.PI * 2f);
			float speed = Random.Range(0.8f, 2.2f);
			var vel = new Vector3(Mathf.Cos(angle) * speed, Random.Range(2f, 5f), Mathf.Sin(angle) * speed);
			AddPage(vel, Random.Range(0.4f, 0.9f));
		}

		private void AddPage(Vector3 velocity, float maxAge)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
			go.name = "BookPortalPage";

			var col = go.GetComponent<Collider>();
			if (col != null) Destroy(col);

			float w = Random.Range(0.12f, 0.23f);
			float h = Random.Range(0.17f, 0.30f);
			go.transform.localScale = new Vector3(w, h, 1f);
			go.transform.position   = transform.position + Vector3.up * 0.12f +
			                          new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.25f, 0.25f));
			go.transform.rotation   = Random.rotation;

			// Each page gets its own material instance so we can fade them independently.
			var mat = new Material(_pageSharedMat);
			var rend = go.GetComponent<Renderer>();
			rend.sharedMaterial   = mat;
			rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

			_pageGos.Add(go);
			_pageMats.Add(mat);
			_pageVelocities.Add(velocity);
			_pageAngVels.Add(new Vector3(
				Random.Range(-400f, 400f),
				Random.Range(-200f, 200f),
				Random.Range(-200f, 200f)));
			_pageAges.Add(0f);
			_pageMaxAges.Add(maxAge);
		}

		private void UpdatePages(float globalAlpha)
		{
			const float gravity = -7f;

			for (int i = _pageGos.Count - 1; i >= 0; i--)
			{
				if (_pageGos[i] == null) { RemovePageAt(i); continue; }

				float age    = _pageAges[i] + Time.deltaTime;
				float maxAge = _pageMaxAges[i];

				if (age >= maxAge) { DestroyPageAt(i); continue; }

				// Manual gravity + movement
				Vector3 vel = _pageVelocities[i];
				vel.y += gravity * Time.deltaTime;
				_pageVelocities[i] = vel;
				_pageAges[i] = age;

				_pageGos[i].transform.position += vel * Time.deltaTime;
				_pageGos[i].transform.Rotate(_pageAngVels[i] * Time.deltaTime, Space.Self);

				// Fade: quadratic falloff so pages stay visible then vanish quickly
				float ageT  = age / maxAge;
				Color c     = _pageColor;
				c.a         = (1f - ageT * ageT) * globalAlpha;
				_pageMats[i].color = c;
			}
		}

		private void RemovePageAt(int i)
		{
			_pageGos.RemoveAt(i);
			_pageMats.RemoveAt(i);
			_pageVelocities.RemoveAt(i);
			_pageAngVels.RemoveAt(i);
			_pageAges.RemoveAt(i);
			_pageMaxAges.RemoveAt(i);
		}

		private void DestroyPageAt(int i)
		{
			Destroy(_pageGos[i]);
			Destroy(_pageMats[i]);
			RemovePageAt(i);
		}

		// ─── Cleanup ──────────────────────────────────────────────────────

		private void CleanupVisuals()
		{
			for (int i = _pageGos.Count - 1; i >= 0; i--)
			{
				if (_pageGos[i] != null) Destroy(_pageGos[i]);
				if (_pageMats[i] != null) Destroy(_pageMats[i]);
			}
			_pageGos.Clear();
			_pageMats.Clear();
			_pageVelocities.Clear();
			_pageAngVels.Clear();
			_pageAges.Clear();
			_pageMaxAges.Clear();

			if (_outerRing != null) { Destroy(_outerRing.gameObject); _outerRing = null; }
			if (_innerRing != null) { Destroy(_innerRing.gameObject); _innerRing = null; }
			if (_runeRing  != null) { Destroy(_runeRing.gameObject);  _runeRing  = null; }
			if (_ringMat        != null) { Destroy(_ringMat);        _ringMat        = null; }
			if (_pageSharedMat  != null) { Destroy(_pageSharedMat);  _pageSharedMat  = null; }
		}
	}
}
