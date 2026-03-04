using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Goedde's right-click ability: aim at a floor surface, then press right-click to summon
	/// a flamethrower at that spot.
	/// The flamethrower burns enemies inside its radius for <see cref="_burnDuration"/> seconds.
	/// The aim ray from the weapon barrel provides line-of-sight — if anything blocks the view to
	/// the floor point, the target is rejected. Any distance is valid as long as the floor is visible.
	/// The cooldown starts immediately on activation.
	/// </summary>
	[DefaultExecutionOrder(5)]
	public class GoeddeFlamethrowerAbility : ContextBehaviour
	{
		// PUBLIC MEMBERS

		public bool  IsActive       => _isActive;
		public bool  IsOnCooldown   => _cooldownTimer.ExpiredOrNotRunning(Runner) == false;
		public bool  IsReady        => _cooldownTimer.ExpiredOrNotRunning(Runner) && _isActive == false;
		public bool  HasValidTarget => _hasValidTarget;

		public float CooldownRemainingTime => _cooldownTimer.RemainingTime(Runner).GetValueOrDefault();
		public float CooldownTotal         => _cooldown;
		public float DurationRemainingTime => _durationTimer.RemainingTime(Runner).GetValueOrDefault();
		public float DurationTotal         => _burnDuration;

		// PRIVATE MEMBERS

		[Header("Ability Settings")]
		[SerializeField]
		private float _cooldown = 8f;
		[SerializeField, Tooltip("How long the flamethrower burns at the targeted location.")]
		private float _burnDuration = 3f;
		[SerializeField, Tooltip("Maximum distance for floor aim targeting.")]
		private float _maxAimDistance = 200f;
		[SerializeField, Tooltip("Minimum Y component of the surface normal to count as floor (0.7 ≈ 45°).")]
		private float _minGroundNormalY = 0.7f;
		[SerializeField, Tooltip("Layers treated as valid floor surfaces.")]
		private LayerMask _groundLayers = Physics.DefaultRaycastLayers;

		[Header("References")]
		[SerializeField, Tooltip("NetworkObject prefab spawned at the targeted floor point.")]
		private NetworkObject _flamethrowerAreaPrefab;
		[SerializeField, Tooltip("Local-only indicator shown on the floor at the current aim target.")]
		private GameObject _aimIndicator;

		[Networked]
		private NetworkBool _isActive { get; set; }
		[Networked]
		private NetworkBool _hasValidTarget { get; set; }
		[Networked]
		private Vector3 _targetPosition { get; set; }
		[Networked]
		private TickTimer _durationTimer { get; set; }
		[Networked]
		private TickTimer _cooldownTimer { get; set; }

		private PlayerAgent _agent;

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_agent = GetComponent<PlayerAgent>();
		}

		// NetworkBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			if (_agent.Owner == null || _agent.Health.IsAlive == false)
			{
				if (_isActive)
				{
					ForceDeactivate();
				}

				return;
			}

			// When the burn duration elapses, clear the active flag.
			if (_isActive && _durationTimer.Expired(Runner))
			{
				_isActive = false;
			}

			// Update the floor aim target (state authority only — writes networked state).
			UpdateFloorTarget();

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.Ability))
			{
				TryActivate();
			}
		}

		public override void Render()
		{
			if (_aimIndicator == null)
				return;

			// Show the aim indicator only for the local player while the ability is ready.
			if (HasInputAuthority == false || IsOnCooldown || _isActive)
			{
				_aimIndicator.SetActive(false);
				return;
			}

			// Raycast from the actual camera for responsive, lag-free visual feedback.
			if (TryGetLocalFloorTarget(out Vector3 localTarget))
			{
				_aimIndicator.SetActive(true);
				_aimIndicator.transform.position = localTarget;
			}
			else
			{
				_aimIndicator.SetActive(false);
			}
		}

		// PRIVATE METHODS

		private void UpdateFloorTarget()
		{
			// Only state authority can write networked properties.
			if (HasStateAuthority == false)
				return;

			var fireTransform = _agent.Weapons?.FireTransform;
			if (fireTransform == null)
			{
				_hasValidTarget = false;
				return;
			}

			var origin    = fireTransform.position;
			var direction = fireTransform.forward;

			// Exclude the player's own layer so they can't target their own feet.
			LayerMask mask = _groundLayers & ~(1 << gameObject.layer);

			if (Runner.GetPhysicsScene().Raycast(origin, direction, out RaycastHit hit, _maxAimDistance, mask, QueryTriggerInteraction.Ignore))
			{
				// Accept hits on surfaces with a sufficiently upward normal (floor, not wall/ceiling).
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

			var sceneCamera = Context.Camera;
			if (sceneCamera == null)
				return false;

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

			Activate();
		}

		private void Activate()
		{
			_isActive      = true;
			_durationTimer = TickTimer.CreateFromSeconds(Runner, _burnDuration);
			_cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);

			if (_flamethrowerAreaPrefab != null)
			{
				Runner.Spawn(_flamethrowerAreaPrefab, _targetPosition, Quaternion.identity, Object.InputAuthority);
			}
		}

		private void ForceDeactivate()
		{
			_isActive      = false;
			_durationTimer = default;
			_cooldownTimer = default;
		}
	}
}
