using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Goedde's ultimate: enter enraged form and summon 3 book traps from the floor.
	/// After a rising delay the books snap shut, dealing heavy damage and stunning
	/// enemies caught inside. While enraged, all of Goedde's outgoing damage is
	/// multiplied (the "headshot bonus" from the design spec, applied as a global
	/// boost until per-hitbox headshot detection is added).
	///
	/// Visual: glowing eyes / enraged effect on Goedde, procedural book-shaped
	/// rectangles that animate rising from the floor then slam closed.
	///
	/// Charged by dealing damage (IUltimateAbility.AccelerateCooldownFromDamage).
	/// Triggered by the X key.
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Goedde Ultimate Ability")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(5)]
	public class GoeddeUltimateAbility : AbilityBase, IUltimateAbility
	{
		private const int TRAP_COUNT = 3;

		// ─── IAbility ────────────────────────────────────────────────────

		public override EAbilitySlot Slot => EAbilitySlot.Ultimate;
		public override bool IsActive => _isEnraged;
		public override bool HasDuration => true;
		public override float DurationRemainingTime => _durationTimer.RemainingTime(Runner).GetValueOrDefault();
		public override float DurationTotal => _enragedDuration;

		// ─── Inspector ───────────────────────────────────────────────────

		[Header("Charge")]
		[SerializeField, Tooltip("Seconds of cooldown removed per 1 damage dealt.")]
		private float _cooldownSecondsPerDamage = 0.05f;

		[Header("Enraged State")]
		[SerializeField] private float _enragedDuration = 8f;
		[SerializeField, Tooltip("Multiplier applied to all outgoing damage while enraged.")]
		private float _enragedDamageMultiplier = 1.4f;

		[Header("Book Traps")]
		[SerializeField, Tooltip("Distance from Goedde to the center trap.")]
		private float _trapDistance = 5f;
		[SerializeField, Tooltip("Angular spread between side traps and center (degrees).")]
		private float _trapSpreadAngle = 35f;
		[SerializeField, Tooltip("Seconds between activation and the snap (books rising).")]
		private float _snapDelay = 0.8f;
		[SerializeField] private float _trapDamage = 80f;
		[SerializeField] private float _stunDuration = 2f;
		[SerializeField, Tooltip("Half-extents of each trap's damage box (width, height, depth).")]
		private Vector3 _trapHalfExtents = new(1f, 1.5f, 0.75f);
		[SerializeField] private LayerMask _targetMask = Physics.DefaultRaycastLayers;

		[Header("Trap Visuals")]
		[SerializeField] private Color _bookColor = new(0.35f, 0.12f, 0.08f, 1f);
		[SerializeField] private Color _bookSnapColor = new(1f, 0.85f, 0.3f, 1f);
		[SerializeField, Tooltip("Full visual size of each book rectangle (world units).")]
		private Vector3 _bookVisualSize = new(2f, 3f, 0.2f);

		[Header("Character Visuals")]
		[SerializeField, Tooltip("GameObject toggled on during enraged state (e.g. glowing eyes particle).")]
		private GameObject _enragedEffect;
		[SerializeField, Tooltip("Emissive color applied to the character's materials during enraged state.")]
		private Color _enragedEmissiveColor = new(0.8f, 0.2f, 0f, 1f);
		[SerializeField, Tooltip("Intensity of the emissive glow on the character while enraged.")]
		private float _enragedEmissiveIntensity = 2f;

		// ─── Networked State ─────────────────────────────────────────────

		[Networked] private NetworkBool _isEnraged { get; set; }
		[Networked] private TickTimer _durationTimer { get; set; }
		[Networked] private TickTimer _snapTimer { get; set; }
		[Networked] private NetworkBool _hasSnapped { get; set; }
		[Networked] private Vector3 _trapPos0 { get; set; }
		[Networked] private Vector3 _trapPos1 { get; set; }
		[Networked] private Vector3 _trapPos2 { get; set; }
		[Networked] private Quaternion _trapRot0 { get; set; }
		[Networked] private Quaternion _trapRot1 { get; set; }
		[Networked] private Quaternion _trapRot2 { get; set; }

		// ─── Static Enraged Tracking ─────────────────────────────────────

		private static readonly Dictionary<PlayerRef, float> _enragedMultipliers = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			_enragedMultipliers.Clear();
		}

		/// <summary>
		/// Queried by HitUtility to apply the enraged damage bonus to all of Goedde's attacks.
		/// </summary>
		public static float GetEnragedDamageMultiplier(NetworkRunner runner, PlayerRef instigatorRef)
		{
			if (runner == null || instigatorRef == default)
				return 1f;
			return _enragedMultipliers.TryGetValue(instigatorRef, out float mult) ? mult : 1f;
		}

		// ─── Local Visual State ──────────────────────────────────────────

		private GameObject[] _bookVisuals;
		private Renderer[] _bookRenderers;
		private bool _wasEnragedRender;
		private float _snapAnimTime;
		private Material _bookMaterial;
		private Material _bookSnapMaterial;
		private List<Renderer> _characterRenderers;
		private MaterialPropertyBlock _propertyBlock;
		private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

		// ─── Lifecycle ───────────────────────────────────────────────────

		public override void Spawned()
		{
			if (HasStateAuthority && _cooldown > 0f)
				StartCooldown();
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			_enragedMultipliers.Remove(Object.InputAuthority);
			DestroyBookVisuals();
		}

		public override void FixedUpdateNetwork()
		{
			if (!ValidateCanAct())
			{
				if (_isEnraged && HasStateAuthority)
					ForceDeactivate();
				return;
			}

			if (_isEnraged)
			{
				_enragedMultipliers[Object.InputAuthority] = _enragedDamageMultiplier;

				if (!_hasSnapped && _snapTimer.Expired(Runner))
					SnapTraps();

				if (_durationTimer.Expired(Runner))
				{
					Deactivate();
					return;
				}
			}
			else
			{
				_enragedMultipliers.Remove(Object.InputAuthority);
			}

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.X))
				TryActivate();
		}

		public override void Render()
		{
			if (_enragedEffect != null)
				_enragedEffect.SetActive(_isEnraged);

			if (_isEnraged && !_wasEnragedRender)
				OnEnragedStart();

			if (!_isEnraged && _wasEnragedRender)
				OnEnragedEnd();

			if (_isEnraged)
				UpdateBookVisuals();

			UpdateCharacterGlow();

			_wasEnragedRender = _isEnraged;
		}

		// ─── IUltimateAbility ────────────────────────────────────────────

		public void AccelerateCooldownFromDamage(float damageDealt)
		{
			if (HasStateAuthority == false)
				return;
			if (_cooldownSecondsPerDamage <= 0f || damageDealt <= 0f)
				return;

			ReduceCooldownSeconds(damageDealt * _cooldownSecondsPerDamage);
		}

		// ─── Activation ──────────────────────────────────────────────────

		private void TryActivate()
		{
			if (_isEnraged || IsOnCooldown || HasStateAuthority == false)
				return;

			_isEnraged     = true;
			_hasSnapped    = false;
			_durationTimer = TickTimer.CreateFromSeconds(Runner, _enragedDuration);
			_snapTimer     = TickTimer.CreateFromSeconds(Runner, _snapDelay);

			CalculateTrapPositions();
		}

		private void Deactivate()
		{
			_isEnraged = false;
			_enragedMultipliers.Remove(Object.InputAuthority);
			StartCooldown();
		}

		private void ForceDeactivate()
		{
			_isEnraged     = false;
			_hasSnapped    = false;
			_durationTimer = default;
			_cooldownTimer = default;
			_enragedMultipliers.Remove(Object.InputAuthority);
		}

		// ─── Trap Placement ──────────────────────────────────────────────

		/// <summary>
		/// Places 3 traps in a fan: center, left, right — each at _trapDistance
		/// from Goedde in the horizontal aim direction.
		/// </summary>
		private void CalculateTrapPositions()
		{
			Vector3 forward = GetHorizontalAimForward();
			Vector3 origin  = transform.position;

			Vector3 centerPos = origin + forward * _trapDistance;
			Quaternion centerRot = Quaternion.LookRotation(forward, Vector3.up);

			Quaternion leftTurn  = Quaternion.Euler(0f, -_trapSpreadAngle, 0f);
			Quaternion rightTurn = Quaternion.Euler(0f,  _trapSpreadAngle, 0f);

			Vector3 leftDir  = leftTurn * forward;
			Vector3 rightDir = rightTurn * forward;

			_trapPos0 = centerPos;
			_trapPos1 = origin + leftDir * _trapDistance;
			_trapPos2 = origin + rightDir * _trapDistance;

			_trapRot0 = centerRot;
			_trapRot1 = Quaternion.LookRotation(leftDir, Vector3.up);
			_trapRot2 = Quaternion.LookRotation(rightDir, Vector3.up);
		}

		// ─── Trap Damage ─────────────────────────────────────────────────

		/// <summary>
		/// One-shot damage pass: OverlapBox at each trap position, damage + stun enemies.
		/// Uses the same HitUtility pipeline as all other damage in the game.
		/// </summary>
		private void SnapTraps()
		{
			_hasSnapped = true;

			var instigatorTeam = _agent.Owner != null ? _agent.Owner.Team : ETeam.None;
			var processed = new HashSet<int>();

			Vector3[] positions   = { _trapPos0, _trapPos1, _trapPos2 };
			Quaternion[] rotations = { _trapRot0, _trapRot1, _trapRot2 };

			for (int i = 0; i < TRAP_COUNT; i++)
			{
				Vector3 center    = positions[i] + Vector3.up * _trapHalfExtents.y;
				Quaternion orient = rotations[i];

				var colliders = Physics.OverlapBox(
					center, _trapHalfExtents, orient,
					_targetMask, QueryTriggerInteraction.Collide);

				foreach (var col in colliders)
				{
					var agent = col.GetComponentInParent<PlayerAgent>();
					if (agent == null || agent.Owner == null || agent.Object == null)
						continue;

					int id = agent.Object.GetInstanceID();
					if (!processed.Add(id))
						continue;

					if (instigatorTeam != ETeam.None && agent.Owner.Team == instigatorTeam)
						continue;

					if (agent.Health == null || agent.Health.IsAlive == false)
						continue;

					Vector3 rawDir = agent.transform.position - positions[i];
					Vector3 dir = rawDir.sqrMagnitude > 0.001f ? rawDir.normalized : Vector3.up;

					var hitData = new HitData
					{
						Action        = EHitAction.Damage,
						Amount        = _trapDamage,
						Position      = agent.transform.position,
						Direction     = dir,
						Normal        = -dir,
						InstigatorRef = Object.InputAuthority,
						Target        = agent.Health,
						HitType       = EHitType.Explosion,
					};

					HitUtility.ProcessHit(ref hitData);
					agent.ApplyStun(_stunDuration);
				}
			}
		}

		// ─── Visuals: Book Traps ─────────────────────────────────────────

		/// <summary>
		/// Creates 3 primitive cube GameObjects styled as book rectangles.
		/// Client-side only, not networked.
		/// </summary>
		private void EnsureBookVisuals()
		{
			if (_bookVisuals != null)
				return;

			if (_bookMaterial == null)
			{
				var shader = Shader.Find("Universal Render Pipeline/Lit");
				if (shader == null) shader = Shader.Find("Standard");
				_bookMaterial = new Material(shader);
				_bookMaterial.color = _bookColor;
				_bookMaterial.EnableKeyword("_EMISSION");
				_bookMaterial.SetColor(EmissionColor, Color.black);
			}

			if (_bookSnapMaterial == null)
			{
				_bookSnapMaterial = new Material(_bookMaterial);
				_bookSnapMaterial.color = _bookSnapColor;
				_bookSnapMaterial.SetColor(EmissionColor, _bookSnapColor * 2f);
			}

			_bookVisuals   = new GameObject[TRAP_COUNT];
			_bookRenderers = new Renderer[TRAP_COUNT];

			for (int i = 0; i < TRAP_COUNT; i++)
			{
				var book = GameObject.CreatePrimitive(PrimitiveType.Cube);
				book.name = $"GoeddeBookTrap_{i}";

				var col = book.GetComponent<Collider>();
				if (col != null) Destroy(col);

				var rend = book.GetComponent<Renderer>();
				rend.material = _bookMaterial;
				rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

				book.SetActive(false);
				_bookVisuals[i]   = book;
				_bookRenderers[i] = rend;
			}
		}

		private void DestroyBookVisuals()
		{
			if (_bookVisuals == null)
				return;

			for (int i = 0; i < _bookVisuals.Length; i++)
			{
				if (_bookVisuals[i] != null)
					Destroy(_bookVisuals[i]);
			}

			_bookVisuals   = null;
			_bookRenderers = null;

			if (_bookMaterial != null) { Destroy(_bookMaterial); _bookMaterial = null; }
			if (_bookSnapMaterial != null) { Destroy(_bookSnapMaterial); _bookSnapMaterial = null; }
		}

		/// <summary>
		/// Client-side entry: show book visuals, reset animation timer.
		/// </summary>
		private void OnEnragedStart()
		{
			_snapAnimTime = 0f;
			EnsureBookVisuals();

			if (_bookVisuals != null)
			{
				for (int i = 0; i < TRAP_COUNT; i++)
					_bookVisuals[i].SetActive(true);
			}
		}

		/// <summary>
		/// Client-side exit: hide and clean up book visuals.
		/// </summary>
		private void OnEnragedEnd()
		{
			if (_bookVisuals != null)
			{
				for (int i = 0; i < TRAP_COUNT; i++)
					_bookVisuals[i].SetActive(false);
			}
		}

		/// <summary>
		/// Animates book traps: rise from below the floor, then flash on snap.
		/// Books start at Y = -bookHeight below their trap position and lerp
		/// up to Y = 0 over the snap delay. After snap they pulse bright then fade.
		/// </summary>
		private void UpdateBookVisuals()
		{
			if (_bookVisuals == null)
				return;

			_snapAnimTime += Time.deltaTime;

			Vector3[] positions   = { _trapPos0, _trapPos1, _trapPos2 };
			Quaternion[] rotations = { _trapRot0, _trapRot1, _trapRot2 };

			float riseT = Mathf.Clamp01(_snapAnimTime / _snapDelay);
			float easeRise = riseT * riseT * (3f - 2f * riseT);
			float yOffset = Mathf.Lerp(-_bookVisualSize.y, 0f, easeRise);

			bool snapped = _hasSnapped;

			for (int i = 0; i < TRAP_COUNT; i++)
			{
				if (_bookVisuals[i] == null)
					continue;

				Vector3 pos = positions[i] + Vector3.up * (yOffset + _bookVisualSize.y * 0.5f);
				_bookVisuals[i].transform.SetPositionAndRotation(pos, rotations[i]);

				float scaleX = _bookVisualSize.x;
				if (snapped)
				{
					float snapT = Mathf.Clamp01((_snapAnimTime - _snapDelay) / 0.15f);
					float slam = 1f + Mathf.Sin(snapT * Mathf.PI) * 0.3f;
					scaleX *= slam;
				}

				_bookVisuals[i].transform.localScale = new Vector3(
					scaleX, _bookVisualSize.y, _bookVisualSize.z);

				if (_bookRenderers[i] != null)
				{
					_bookRenderers[i].material = snapped ? _bookSnapMaterial : _bookMaterial;
				}

				float fadeTime = _enragedDuration - 1f;
				if (_snapAnimTime > fadeTime)
				{
					float fadeT = Mathf.Clamp01((_snapAnimTime - fadeTime) / 1f);
					Color c = _bookRenderers[i].material.color;
					c.a = 1f - fadeT;
					_bookRenderers[i].material.color = c;
				}
			}
		}

		// ─── Visuals: Enraged Character Glow ─────────────────────────────

		/// <summary>
		/// Applies an emissive glow to the character's renderers while enraged,
		/// giving the "glowing eyes" / strict form visual from the design spec.
		/// </summary>
		private void UpdateCharacterGlow()
		{
			if (_characterRenderers == null)
			{
				_characterRenderers = new List<Renderer>();
				GetComponentsInChildren(true, _characterRenderers);
				_characterRenderers.RemoveAll(r =>
					r.gameObject.name.Contains("BookTrap") ||
					r.gameObject.name.Contains("Phase"));
			}

			Color targetEmission = _isEnraged
				? _enragedEmissiveColor * _enragedEmissiveIntensity
				: Color.black;

			if (_propertyBlock == null)
				_propertyBlock = new MaterialPropertyBlock();

			foreach (var rend in _characterRenderers)
			{
				if (rend == null) continue;

				rend.GetPropertyBlock(_propertyBlock);
				_propertyBlock.SetColor(EmissionColor, targetEmission);
				rend.SetPropertyBlock(_propertyBlock);
			}
		}
	}
}
