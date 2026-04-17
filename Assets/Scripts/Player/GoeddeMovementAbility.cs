using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Goedde's movement ability: phase-dash forward (or in held movement direction) up to
	/// <see cref="_teleportDistance"/>, stopping short of walls and sliding along surfaces
	/// hit at an angle. While phased, Goedde is invisible, invulnerable, and cannot move.
	/// Activation is blocked when pressed against a wall with no viable travel path,
	/// preventing the cooldown from being wasted.
	/// Triggered by the E key.
	///
	/// Wall detection uses a back-stepped CapsuleCast so that point-blank walls are always
	/// caught (Unity's CapsuleCast ignores colliders the capsule starts inside).
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Goedde Movement Ability")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(5)]
	public class GoeddeMovementAbility : AbilityBase
	{
		// ─── IAbility ────────────────────────────────────────────────────

		public override EAbilitySlot Slot => EAbilitySlot.Movement;
		public bool IsPhased => _isPhased;
		public override bool IsActive => _isPhased;
		public override bool IsReady => base.IsReady && _isPhased == false;
		public override bool HasDuration => true;
		public override float DurationRemainingTime => _durationTimer.RemainingTime(Runner).GetValueOrDefault();
		public override float DurationTotal => _duration;

		// ─── Inspector ───────────────────────────────────────────────────

		[Header("Phase Settings")]
		[SerializeField] private float _duration = 2f;

		[Tooltip("Maximum phase-dash distance in meters.")]
		[SerializeField] private float _teleportDistance = 4f;

		[Tooltip("How long the smooth dash to the destination takes (seconds).")]
		[SerializeField] private float _slideDuration = 0.35f;

		[Tooltip("Minimum travel distance to allow activation. Rejects the ability (no cooldown) if below this.")]
		[SerializeField] private float _minTravelDistance = 0.3f;

		[Tooltip("Speed curve controlling the dash motion. X: 0-1 normalized time, Y: 0-1 progress.")]
		[SerializeField] private AnimationCurve _slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

		[Header("Collision")]
		[SerializeField] private float _capsuleRadius = 0.35f;
		[SerializeField] private float _capsuleHeight = 1.8f;
		[Tooltip("Layers treated as solid for both wall detection and ground snapping. Should include terrain.")]
		[SerializeField] private LayerMask _wallLayers = Physics.DefaultRaycastLayers;

		[Tooltip("Gap maintained between the destination and wall surfaces.")]
		[SerializeField] private float _wallSkinWidth = 0.05f;

		[Tooltip("Fraction of remaining distance kept when redirecting along a wall (0 = full stop, 1 = no loss).")]
		[SerializeField, Range(0f, 1f)] private float _wallSlideRetention = 0.7f;

		[Tooltip("Max downward snap distance to keep the player grounded during the dash.")]
		[SerializeField] private float _groundSnapDistance = 0.5f;

		[Header("Visual References")]
		[SerializeField] private GameObject _visual;
		[SerializeField] private GameObject _phaseEffect;

		[Tooltip("Optional burst particle system played at the entry position when phasing starts.")]
		[SerializeField] private ParticleSystem _entryBurstVFX;

		[Tooltip("Optional burst particle system played at the exit position when phasing ends.")]
		[SerializeField] private ParticleSystem _exitBurstVFX;

		[Tooltip("Optional looping trail particle system active while dashing.")]
		[SerializeField] private ParticleSystem _phaseTrailVFX;

		// ─── Networked State ─────────────────────────────────────────────

		[Networked] private NetworkBool _isPhased { get; set; }
		[Networked] private TickTimer _durationTimer { get; set; }
		[Networked] private TickTimer _slideTimer { get; set; }
		[Networked] private Vector3 _slideStartPos { get; set; }
		[Networked] private Vector3 _slideEndPos { get; set; }

		// ─── Local State ─────────────────────────────────────────────────

		private HitboxRoot _hitboxRoot;
		private bool _wasPhasedRender;

		// ─── Lifecycle ───────────────────────────────────────────────────

		protected override void Awake()
		{
			base.Awake();
			_hitboxRoot = GetComponent<HitboxRoot>();
		}

		public override void FixedUpdateNetwork()
		{
			if (!ValidateCanAct())
			{
				if (_isPhased && HasStateAuthority)
					ForceDeactivate();
				return;
			}

			if (_isPhased && _durationTimer.Expired(Runner))
			{
				Deactivate();
				return;
			}

			if (_isPhased)
			{
				UpdateSlide();

				_agent.MoveSpeedMultiplier = 0f;
				_agent.JumpMultiplier      = 0f;

				if (_hitboxRoot != null)
					_hitboxRoot.HitboxRootActive = false;
			}

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.E))
				TryActivate(input);
		}

		public override void Render()
		{
			if (_visual != null)
				_visual.SetActive(!_isPhased);

			if (_phaseEffect != null)
				_phaseEffect.SetActive(_isPhased);

			if (_isPhased && !_wasPhasedRender)
			{
				if (_entryBurstVFX != null)
					_entryBurstVFX.Play();
				if (_phaseTrailVFX != null)
					_phaseTrailVFX.Play();
			}

			if (!_isPhased && _wasPhasedRender)
			{
				if (_exitBurstVFX != null)
					_exitBurstVFX.Play();
				if (_phaseTrailVFX != null)
					_phaseTrailVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
			}

			_wasPhasedRender = _isPhased;
		}

		// ─── Activation ──────────────────────────────────────────────────

		/// <summary>
		/// Pre-validates the travel path before committing. If the resolved distance is
		/// below <see cref="_minTravelDistance"/> (e.g. pressed into a wall), the ability
		/// silently fails without consuming cooldown.
		/// No HasStateAuthority guard: both client (for prediction) and server run this
		/// so the dash feels responsive without waiting for a server round-trip.
		/// </summary>
		private void TryActivate(GameplayInput input)
		{
			if (_isPhased || IsOnCooldown)
				return;

			Vector3 direction     = ResolveMoveDirection(input);
			float travelDistance   = CalculateTravelPath(direction, out Vector3 destination);

			if (travelDistance < _minTravelDistance)
				return;

			Activate(destination);
		}

		private void Activate(Vector3 destination)
		{
			_isPhased      = true;
			_durationTimer = TickTimer.CreateFromSeconds(Runner, _duration);
			_slideStartPos = transform.position;
			_slideEndPos   = destination;
			_slideTimer    = TickTimer.CreateFromSeconds(Runner, _slideDuration);

			_agent.Health.SetImmortality(_duration + 0.5f);

			if (_hitboxRoot != null)
				_hitboxRoot.HitboxRootActive = false;
		}

		private void Deactivate()
		{
			_isPhased = false;
			StartCooldown();

			_agent.Health.StopImmortality();
			_agent.MoveSpeedMultiplier = 1f;
			_agent.JumpMultiplier      = 1f;

			if (_hitboxRoot != null)
				_hitboxRoot.HitboxRootActive = true;
		}

		private void ForceDeactivate()
		{
			_isPhased      = false;
			_durationTimer = default;
			_cooldownTimer = default;
			_agent.MoveSpeedMultiplier = 1f;
			_agent.JumpMultiplier      = 1f;
		}

		// ─── Slide Interpolation ─────────────────────────────────────────

		/// <summary>
		/// Moves the player along the pre-computed slide path using the animation curve,
		/// with a downward raycast each tick to keep the character grounded over terrain.
		/// </summary>
		private void UpdateSlide()
		{
			if (_slideTimer.ExpiredOrNotRunning(Runner))
				return;

			float remaining     = _slideTimer.RemainingTime(Runner).GetValueOrDefault();
			float normalizedTime = 1f - Mathf.Clamp01(remaining / _slideDuration);
			float t             = _slideCurve.Evaluate(normalizedTime);

			Vector3 slidePos = Vector3.Lerp(_slideStartPos, _slideEndPos, t);

			if (Physics.Raycast(slidePos + Vector3.up * _groundSnapDistance, Vector3.down,
				out RaycastHit groundHit, _groundSnapDistance * 2f,
				_wallLayers, QueryTriggerInteraction.Ignore))
			{
				float delta = slidePos.y - groundHit.point.y;
				if (Mathf.Abs(delta) > 0.01f && Mathf.Abs(delta) < _groundSnapDistance)
					slidePos.y = groundHit.point.y;
			}

			_agent.KCC.SetPosition(slidePos);
		}

		// ─── Direction Resolution ────────────────────────────────────────

		/// <summary>
		/// Returns the horizontal movement direction from WASD input, or the player's
		/// facing direction when no movement keys are held.
		/// </summary>
		private Vector3 ResolveMoveDirection(GameplayInput input)
		{
			if (input.MoveDirection.sqrMagnitude > 0.01f)
			{
				var worldMove = _agent.KCC.TransformRotation
					* new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
				return new Vector3(worldMove.x, 0f, worldMove.z).normalized;
			}

			var forward = _agent.KCC.TransformRotation * Vector3.forward;
			return new Vector3(forward.x, 0f, forward.z).normalized;
		}

		// ─── Travel Path Calculation ─────────────────────────────────────

		/// <summary>
		/// Computes the dash endpoint using a two-phase sweep:
		///
		/// Phase 1 — Primary sweep: CapsuleCast from a back-stepped origin so that walls
		/// at point-blank range are always detected (Physics.CapsuleCast ignores colliders
		/// the capsule starts inside, which is the root cause of the wall-phasing exploit).
		///
		/// Phase 2 — Wall slide: if the primary sweep hits a wall at an angle, the leftover
		/// distance is projected onto the wall tangent plane and a second sweep validates
		/// the slide path. This lets the player dash along walls instead of dead-stopping.
		///
		/// Returns the total usable distance. <paramref name="destination"/> is set to the
		/// resolved world-space endpoint.
		/// </summary>
		private float CalculateTravelPath(Vector3 direction, out Vector3 destination)
		{
			Vector3 origin    = transform.position;
			float halfHeight  = Mathf.Max(0f, _capsuleHeight * 0.5f - _capsuleRadius);

			float backStep     = _capsuleRadius + 0.05f;
			Vector3 castOrigin = origin - direction * backStep;
			Vector3 capBottom  = castOrigin + Vector3.up * _capsuleRadius;
			Vector3 capTop     = castOrigin + Vector3.up * (_capsuleRadius + halfHeight * 2f);
			float castMaxDist  = _teleportDistance + backStep;

			bool hitWall = Physics.CapsuleCast(
				capBottom, capTop, _capsuleRadius, direction,
				out RaycastHit hit, castMaxDist, _wallLayers,
				QueryTriggerInteraction.Ignore);

			if (!hitWall)
			{
				destination = origin + direction * _teleportDistance;
				return _teleportDistance;
			}

			float primaryTravel = Mathf.Max(0f, hit.distance - backStep - _wallSkinWidth);
			Vector3 endPoint    = origin + direction * primaryTravel;

			float leftover = _teleportDistance - primaryTravel;
			if (leftover > _minTravelDistance && _wallSlideRetention > 0f)
			{
				Vector3 slideOffset = ComputeWallSlide(
					endPoint, direction, hit.normal, leftover, halfHeight);
				endPoint += slideOffset;
			}

			destination = endPoint;
			return Vector3.Distance(origin, destination);
		}

		/// <summary>
		/// Projects remaining travel distance onto the wall tangent plane and validates
		/// the slide path with a second CapsuleCast. Returns the displacement vector to
		/// add to the current endpoint.
		/// </summary>
		private Vector3 ComputeWallSlide(
			Vector3 slideOrigin, Vector3 moveDir, Vector3 wallNormal,
			float leftover, float halfHeight)
		{
			Vector3 flatNormal = wallNormal;
			flatNormal.y = 0f;

			if (flatNormal.sqrMagnitude < 0.001f)
				return Vector3.zero;

			flatNormal.Normalize();

			Vector3 slideDir = moveDir - Vector3.Dot(moveDir, flatNormal) * flatNormal;
			slideDir.y = 0f;

			if (slideDir.sqrMagnitude < 0.01f)
				return Vector3.zero;

			slideDir.Normalize();
			float slideMax = leftover * _wallSlideRetention;

			Vector3 capBottom = slideOrigin + Vector3.up * _capsuleRadius;
			Vector3 capTop    = slideOrigin + Vector3.up * (_capsuleRadius + halfHeight * 2f);

			if (Physics.CapsuleCast(
				capBottom, capTop, _capsuleRadius, slideDir,
				out RaycastHit slideHit, slideMax, _wallLayers,
				QueryTriggerInteraction.Ignore))
			{
				slideMax = Mathf.Max(0f, slideHit.distance - _wallSkinWidth);
			}

			return slideDir * slideMax;
		}
	}
}
