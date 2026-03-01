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
	public class TheissBuffAbility : ContextBehaviour
	{
		// PUBLIC MEMBERS

		public bool  IsActive      => _isActive;
		public bool  IsOnCooldown => _cooldownTimer.ExpiredOrNotRunning(Runner) == false;
		public bool  IsReady      => _cooldownTimer.ExpiredOrNotRunning(Runner) && _isActive == false;

		public float CooldownRemainingTime => _cooldownTimer.RemainingTime(Runner).GetValueOrDefault();
		public float CooldownTotal         => _cooldown;
		public float DurationRemainingTime => _durationTimer.RemainingTime(Runner).GetValueOrDefault();
		public float DurationTotal         => _duration;

		// PRIVATE MEMBERS

		[Header("Ability Settings")]
		[SerializeField]
		private float _duration = 5f;
		[SerializeField]
		private float _cooldown = 15f;
		[SerializeField, Tooltip("Move speed multiplier while buff is active")]
		private float _speedMultiplier = 1.5f;
		[SerializeField, Tooltip("Jump height multiplier while buff is active")]
		private float _jumpMultiplier = 1.4f;
		[SerializeField, Tooltip("Temporary max health bonus while buff is active")]
		private float _maxHealthBonus = 50f;
		[SerializeField, Tooltip("Immediate heal applied when buff activates")]
		private float _activationHeal = 50f;

		[Networked]
		private NetworkBool _isActive { get; set; }
		[Networked]
		private TickTimer _durationTimer { get; set; }
		[Networked]
		private TickTimer _cooldownTimer { get; set; }
		[Networked]
		private NetworkBool _healthBuffApplied { get; set; }

		private PlayerAgent _agent;

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_agent = GetComponent<PlayerAgent>();
		}

		// NetworkBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			// Reset multipliers each tick - we set them below when active
			if (_agent != null)
			{
				_agent.MoveSpeedMultiplier = 1f;
				_agent.JumpMultiplier = 1f;
			}

			if (_agent == null || _agent.Owner == null || _agent.Health.IsAlive == false)
			{
				if (_isActive)
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

		// PRIVATE METHODS

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
			_cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);
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
