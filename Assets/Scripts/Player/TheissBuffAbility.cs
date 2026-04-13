using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Theiss ability 2 (E key): temporary buff granting increased speed and jump height.
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Theiss Buff Ability")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(-10)]
	public class TheissBuffAbility : AbilityBase
	{
		public override EAbilitySlot Slot => EAbilitySlot.Movement;
		public override bool IsActive => _isActive;
		public override bool IsReady => base.IsReady && _isActive == false;
		public override bool HasDuration => true;
		public override float DurationRemainingTime => _durationTimer.RemainingTime(Runner).GetValueOrDefault();
		public override float DurationTotal => _duration;

		[Header("Buff Settings")]
		[SerializeField] private float _duration = 5f;
		[SerializeField, Tooltip("Move speed multiplier while buff is active")]
		private float _speedMultiplier = 1.5f;
		[SerializeField, Tooltip("Jump height multiplier while buff is active")]
		private float _jumpMultiplier = 1.4f;
		[SerializeField, Tooltip("Temporary max health bonus while buff is active")]
		private float _maxHealthBonus = 50f;
		[SerializeField, Tooltip("Immediate heal applied when buff activates")]
		private float _activationHeal = 50f;

		[Networked] private NetworkBool _isActive { get; set; }
		[Networked] private TickTimer _durationTimer { get; set; }
		[Networked] private NetworkBool _healthBuffApplied { get; set; }

		public override void FixedUpdateNetwork()
		{
			// Reset multipliers each tick - we set them below when active
			if (_agent != null)
			{
				_agent.MoveSpeedMultiplier = 1f;
				_agent.JumpMultiplier = 1f;
			}

			if (!ValidateCanAct())
			{
				if (_isActive && HasStateAuthority)
				{
					ForceDeactivate();
				}
				return;
			}

			if (_isActive && _durationTimer.Expired(Runner))
			{
				Deactivate();
				return;
			}

			if (_isActive)
			{
				_agent.MoveSpeedMultiplier = _speedMultiplier;
				_agent.JumpMultiplier = _jumpMultiplier;
			}

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.E))
			{
				TryActivate();
			}
		}

		private void TryActivate()
		{
			if (_isActive || IsOnCooldown)
				return;

			Activate();
		}

		private void Activate()
		{
			_isActive = true;
			_durationTimer = TickTimer.CreateFromSeconds(Runner, _duration);
			ApplyHealthBuff();
		}

		private void Deactivate()
		{
			RemoveHealthBuff();
			_isActive = false;
			StartCooldown();
		}

		private void ForceDeactivate()
		{
			RemoveHealthBuff();
			_isActive = false;
			_durationTimer = default;
			_cooldownTimer = default;
		}

		private void ApplyHealthBuff()
		{
			if (_healthBuffApplied || _agent?.Health == null)
				return;

			_agent.Health.ChangeMaxHealthBonus(_maxHealthBonus);
			_agent.Health.Heal(_activationHeal);
			_healthBuffApplied = true;
		}

		private void RemoveHealthBuff()
		{
			if (_healthBuffApplied == false || _agent?.Health == null)
				return;

			_agent.Health.ChangeMaxHealthBonus(-_maxHealthBonus);
			_healthBuffApplied = false;
		}
	}
}
