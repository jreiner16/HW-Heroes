using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Goedde's right-click ability: aim at a floor surface and press right-click to hurl a
	/// magic book at that spot. The book arcs through the air and crashes down, spawning a
	/// swirling portal circle that erupts pages and damages enemies inside.
	/// </summary>
	[DefaultExecutionOrder(5)]
	public class GoeddeFlamethrowerAbility : AbilityBase
	{
		public override EAbilitySlot Slot => EAbilitySlot.RightClick;
		public override bool IsActive => _isActive;
		public override bool IsReady => base.IsReady && _isActive == false;
		public bool HasValidTarget => _hasValidTarget;
		public override bool HasDuration => true;
		public override float DurationRemainingTime => _durationTimer.RemainingTime(Runner).GetValueOrDefault();
		public override float DurationTotal => _burnDuration;

		[Header("Book Throw Settings")]
		[SerializeField, Tooltip("How long the portal remains active at the landing spot.")]
		private float _burnDuration = 3f;
		[SerializeField, Tooltip("Maximum distance for floor aim targeting.")]
		private float _maxAimDistance = 200f;
		[SerializeField, Tooltip("Minimum Y component of the surface normal to count as floor (0.7 ≈ 45°).")]
		private float _minGroundNormalY = 0.7f;
		[SerializeField, Tooltip("Layers treated as valid floor surfaces.")]
		private LayerMask _groundLayers = Physics.DefaultRaycastLayers;

		[Header("References")]
		[SerializeField, Tooltip("NetworkObject prefab (GoeddeFlamethrowerArea / book portal) spawned at the floor target.")]
		private NetworkObject _flamethrowerAreaPrefab;
		[SerializeField, Tooltip("Local-only indicator shown on the floor at the current aim target.")]
		private GameObject _aimIndicator;

		[Header("Book Arc Visuals")]
		[SerializeField, Tooltip("Color of the flying book.")]
		private Color _bookColor = new Color(0.30f, 0.10f, 0.06f, 1f);
		[SerializeField, Tooltip("Peak height of the book's parabolic arc above the midpoint.")]
		private float _bookArcHeight = 2.5f;
		[SerializeField, Tooltip("Seconds the book takes to reach the target.")]
		private float _bookFlightDuration = 0.30f;

		// ─── Networked State ─────────────────────────────────────────────

		[Networked] private NetworkBool _isActive { get; set; }
		[Networked] private NetworkBool _hasValidTarget { get; set; }
		[Networked] private Vector3 _targetPosition { get; set; }
		[Networked] private TickTimer _durationTimer { get; set; }

		// ─── Local Visual State ──────────────────────────────────────────

		private bool _wasActive;
		private GameObject _flyingBook;
		private Material _flyingBookMat;
		private float _flightProgress;   // 0 → 1
		private Vector3 _flightStart;

		private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

		// ─── NetworkBehaviour ─────────────────────────────────────────────

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			DestroyFlyingBook();
		}

		public override void FixedUpdateNetwork()
		{
			if (!ValidateCanAct())
			{
				if (_isActive)
					ForceDeactivate();
				return;
			}

			if (_isActive && _durationTimer.Expired(Runner))
				_isActive = false;

			UpdateFloorTarget();

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.Ability))
				TryActivate();
		}

		public override void Render()
		{
			// Aim indicator (local player only)
			if (_aimIndicator != null)
			{
				bool show = HasInputAuthority && !IsOnCooldown && !_isActive;
				if (show && TryGetLocalFloorTarget(out Vector3 localTarget))
				{
					_aimIndicator.SetActive(true);
					_aimIndicator.transform.position = localTarget;
				}
				else
				{
					_aimIndicator.SetActive(false);
				}
			}

			// Spawn flying book on activation (local player only for accurate start position)
			if (_isActive && !_wasActive && HasInputAuthority)
				StartBookArc();

			// Update the arc each frame
			if (_flyingBook != null)
				UpdateBookArc();

			// Clean up if the ability ended before the arc finished
			if (!_isActive && _flyingBook != null)
				DestroyFlyingBook();

			_wasActive = _isActive;
		}

		// ─── Server-Side Activation ───────────────────────────────────────

		private void UpdateFloorTarget()
		{
			if (HasStateAuthority == false)
				return;

			var weapons = _agent.Weapons;
			if (weapons?.FireTransform == null)
			{
				_hasValidTarget = false;
				return;
			}

			Vector3 origin    = weapons.FireTransform.position;
			Vector3 direction = weapons.AimDirection;
			LayerMask mask    = _groundLayers & ~(1 << gameObject.layer);

			if (Runner.GetPhysicsScene().Raycast(origin, direction, out RaycastHit hit, _maxAimDistance, mask, QueryTriggerInteraction.Ignore))
			{
				if (hit.normal.y >= _minGroundNormalY)
				{
					_hasValidTarget = true;
					_targetPosition = hit.point;
				}
				else
				{
					_hasValidTarget = false;
				}
			}
			else
			{
				_hasValidTarget = false;
			}
		}

		private bool TryGetLocalFloorTarget(out Vector3 target)
		{
			target = Vector3.zero;

			var sceneCamera = Context?.Camera;
			if (sceneCamera == null) return false;

			var camTransform = sceneCamera.Camera.transform;
			LayerMask mask   = _groundLayers & ~(1 << gameObject.layer);

			if (Physics.Raycast(camTransform.position, camTransform.forward, out RaycastHit hit, _maxAimDistance, mask, QueryTriggerInteraction.Ignore))
			{
				if (hit.normal.y >= _minGroundNormalY)
				{
					target = hit.point;
					return true;
				}
			}

			return false;
		}

		private void TryActivate()
		{
			if (_isActive || IsOnCooldown || _hasValidTarget == false)
				return;

			if (HasStateAuthority == false)
				return;

			_isActive      = true;
			_durationTimer = TickTimer.CreateFromSeconds(Runner, _burnDuration);
			StartCooldown();

			if (_flamethrowerAreaPrefab != null)
				Runner.Spawn(_flamethrowerAreaPrefab, _targetPosition, Quaternion.identity, Object.InputAuthority);
		}

		private void ForceDeactivate()
		{
			_isActive      = false;
			_durationTimer = default;
			_cooldownTimer = default;
		}

		// ─── Flying Book Visuals ──────────────────────────────────────────

		private void StartBookArc()
		{
			DestroyFlyingBook();
			_flightProgress = 0f;

			var weapons = _agent.Weapons;
			_flightStart = weapons?.FireTransform != null
				? weapons.FireTransform.position
				: transform.position + Vector3.up * 1.5f;

			_flyingBook      = GameObject.CreatePrimitive(PrimitiveType.Cube);
			_flyingBook.name = "GoeddeBookThrow";

			// No physics collider — purely visual
			var col = _flyingBook.GetComponent<Collider>();
			if (col != null) Destroy(col);

			_flyingBook.transform.localScale = new Vector3(0.22f, 0.30f, 0.04f);
			_flyingBook.transform.position   = _flightStart;

			if (_flyingBookMat == null)
			{
				var shader = Shader.Find("Universal Render Pipeline/Lit");
				if (shader == null) shader = Shader.Find("Standard");
				_flyingBookMat = new Material(shader);
				_flyingBookMat.color = _bookColor;
				_flyingBookMat.EnableKeyword("_EMISSION");
				_flyingBookMat.SetColor(EmissionColor, _bookColor * 0.4f);
			}

			var rend = _flyingBook.GetComponent<Renderer>();
			rend.sharedMaterial    = _flyingBookMat;
			rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		}

		private void UpdateBookArc()
		{
			if (_flyingBook == null) return;

			_flightProgress += Time.deltaTime / Mathf.Max(0.01f, _bookFlightDuration);

			if (_flightProgress >= 1f)
			{
				DestroyFlyingBook();
				return;
			}

			float t  = _flightProgress;
			Vector3 p0  = _flightStart;
			Vector3 p2  = _targetPosition;
			Vector3 mid = (p0 + p2) * 0.5f;
			Vector3 p1  = mid + Vector3.up * _bookArcHeight;

			// Quadratic Bezier position
			float mt  = 1f - t;
			Vector3 pos = mt * mt * p0 + 2f * mt * t * p1 + t * t * p2;
			_flyingBook.transform.position = pos;

			// Tangent gives the forward direction for orientation
			Vector3 tangent = (2f * mt * (p1 - p0) + 2f * t * (p2 - p1)).normalized;
			if (tangent.sqrMagnitude > 0.001f)
			{
				// Face the direction of travel and spin around its long axis to mimic tumbling
				_flyingBook.transform.rotation =
					Quaternion.LookRotation(tangent) *
					Quaternion.Euler(t * 540f, 0f, 0f);
			}
		}

		private void DestroyFlyingBook()
		{
			if (_flyingBook != null)
			{
				Destroy(_flyingBook);
				_flyingBook = null;
			}
		}
	}
}
