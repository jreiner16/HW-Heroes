using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Theiss Q-key ability: Jump Smash — leaps into the air, hangs momentarily with
	/// floaty gravity, then crashes down at high speed. On landing, deals AoE damage
	/// and knocks nearby enemies outward.
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Theiss Jump Smash Ability")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(-10)]
	public class TheissJumpSmashAbility : AbilityBase
	{
		public override EAbilitySlot Slot     => EAbilitySlot.RightClick;
		public override bool         IsActive => _phase != SmashPhase.Idle;

		private enum SmashPhase : byte { Idle, Launch, Hang, Descend }

		[Header("Launch")]
		[SerializeField, Tooltip("Upward impulse applied when the ability activates")]
		private float _launchImpulse = 14f;
		[SerializeField, Tooltip("Seconds to wait in Launch before transitioning to Hang")]
		private float _launchDuration = 0.35f;

		[Header("Hang (floaty)")]
		[SerializeField, Tooltip("Gravity value during the floaty hang phase (very low)")]
		private float _hangGravity = 1.5f;
		[SerializeField, Tooltip("Duration of the floaty hang phase in seconds")]
		private float _hangDuration = 0.65f;

		[Header("Descend")]
		[SerializeField, Tooltip("Gravity value during the fast descent phase")]
		private float _descendGravity = 70f;
		[SerializeField, Tooltip("Safety timeout for descent in case the player never lands")]
		private float _maxDescendTime = 5f;

		[Header("Smash")]
		[SerializeField, Tooltip("Radius of the AoE damage on landing")]
		private float _smashRadius = 5f;
		[SerializeField, Tooltip("Damage dealt to enemies in smash radius")]
		private float _smashDamage = 80f;
		[SerializeField, Tooltip("Horizontal knockback force applied to hit targets")]
		private float _knockbackForce = 8f;
		[SerializeField, Tooltip("Upward bounce impulse applied to hit targets")]
		private float _verticalKnockback = 4f;

		[Networked] private SmashPhase  _phase               { get; set; }
		[Networked] private TickTimer   _phaseTimer          { get; set; }
		[Networked] private NetworkBool _wasGroundedLastTick  { get; set; }

		// AbilityBase / NetworkBehaviour INTERFACE ---------------------------

		public override void FixedUpdateNetwork()
		{
			if (ValidateCanAct() == false)
			{
				if (_phase != SmashPhase.Idle && HasStateAuthority)
					_phase = SmashPhase.Idle;
				return;
			}

			switch (_phase)
			{
				case SmashPhase.Idle:    HandleIdle();    break;
				case SmashPhase.Launch:  HandleLaunch();  break;
				case SmashPhase.Hang:    HandleHang();    break;
				case SmashPhase.Descend: HandleDescend(); break;
			}

			_wasGroundedLastTick = _agent.KCC.IsGrounded;
		}

		// PHASE HANDLERS -----------------------------------------------------

		private void HandleIdle()
		{
			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.Q) == false)
				return;

			if (IsOnCooldown || _agent.KCC.IsGrounded == false)
				return;

			// ExtraJumpImpulse runs on both state authority and input authority,
			// giving the local client responsive predicted movement.
			_agent.ExtraJumpImpulse = _launchImpulse;
			_phase      = SmashPhase.Launch;
			_phaseTimer = TickTimer.CreateFromSeconds(Runner, _launchDuration);
		}

		private void HandleLaunch()
		{
			// Lock horizontal steering during the brief launch window
			_agent.LockMovement = true;

			if (_phaseTimer.Expired(Runner))
			{
				_phase      = SmashPhase.Hang;
				_phaseTimer = TickTimer.CreateFromSeconds(Runner, _hangDuration);
			}
		}

		private void HandleHang()
		{
			// Near-zero gravity = satisfying floaty hang time at the apex
			_agent.GravityOverride = _hangGravity;
			_agent.LockMovement    = true;

			if (_phaseTimer.Expired(Runner))
			{
				_phase      = SmashPhase.Descend;
				_phaseTimer = TickTimer.CreateFromSeconds(Runner, _maxDescendTime);
			}
		}

		private void HandleDescend()
		{
			// High gravity = fast, snappy slam descent
			_agent.GravityOverride = _descendGravity;
			_agent.LockMovement    = true;

			// Safety timeout — just in case the player somehow never lands
			bool timedOut = _phaseTimer.Expired(Runner);

			// Trigger smash on the first tick we become grounded (and weren't last tick)
			bool justLanded = _agent.KCC.IsGrounded && _wasGroundedLastTick == false;

			if (justLanded || timedOut)
			{
				if (justLanded)
					PerformSmash();

				_phase = SmashPhase.Idle;
				StartCooldown();
			}
		}

		// SMASH LOGIC --------------------------------------------------------

		private void PerformSmash()
		{
			if (HasStateAuthority == false)
				return;

			var smashCenter = transform.position;
			var colliders   = Physics.OverlapSphere(smashCenter, _smashRadius, ObjectLayerMask.HitTargets);

			for (int i = 0; i < colliders.Length; i++)
			{
				var col = colliders[i];
				var hit = HitUtility.ProcessHit(this, col, _smashDamage, EHitType.Explosion);

				if (hit.Amount <= 0f)
					continue;

				var agent = col.GetComponentInParent<PlayerAgent>();
				if (agent == null)
					continue;

				// Horizontal knockback — push outward from smash center
				var knockDir = col.transform.position - smashCenter;
				knockDir.y = 0f;

				if (knockDir.sqrMagnitude > 0.001f)
					knockDir.Normalize();
				else
					knockDir = _agent.KCC.TransformRotation * Vector3.forward; // fallback for dead center

				agent.AddKnockbackImpulse(knockDir, _knockbackForce);
				agent.AddBounceImpulse(_verticalKnockback);
			}
		}
	}
}
