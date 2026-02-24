using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Geodde's movement ability: phase out of reality, becoming invisible and invulnerable
	/// for a short duration, then reappear. Triggered by the E key.
	/// </summary>
	[DefaultExecutionOrder(5)]
	public class GeoddeMovementAbility : ContextBehaviour
	{
		// PUBLIC MEMBERS

		public bool  IsPhased    => _isPhased;
		public bool  IsOnCooldown => _cooldownTimer.ExpiredOrNotRunning(Runner) == false;
		public bool  IsReady     => _cooldownTimer.ExpiredOrNotRunning(Runner) && _isPhased == false;

		public float CooldownRemainingTime => _cooldownTimer.RemainingTime(Runner).GetValueOrDefault();
		public float CooldownTotal         => _cooldown;
		public float DurationRemainingTime => _durationTimer.RemainingTime(Runner).GetValueOrDefault();
		public float DurationTotal         => _duration;

		// PRIVATE MEMBERS

		[Header("Ability Settings")]
		[SerializeField]
		private float _duration = 2.5f;
		[SerializeField]
		private float _cooldown = 10f;

		[Header("References")]
		[SerializeField]
		private GameObject _visual;
		[SerializeField]
		private GameObject _phaseEffect;

		[Networked]
		private NetworkBool _isPhased { get; set; }
		[Networked]
		private TickTimer _durationTimer { get; set; }
		[Networked]
		private TickTimer _cooldownTimer { get; set; }

		private PlayerAgent _agent;
		private HitboxRoot _hitboxRoot;

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_agent = GetComponent<PlayerAgent>();
			_hitboxRoot = GetComponent<HitboxRoot>();
		}

		// NetworkBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			if (_agent.Owner == null || _agent.Health.IsAlive == false)
			{
				if (_isPhased)
				{
					ForceDeactivate();
				}

				return;
			}

			if (_isPhased && _durationTimer.Expired(Runner))
			{
				Deactivate();
				return;
			}

			// Runs after PlayerBody (execution order 5 vs default 0) so this wins
			if (_isPhased && _hitboxRoot != null)
			{
				_hitboxRoot.HitboxRootActive = false;
			}

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.E))
			{
				TryActivate();
			}
		}

		public override void Render()
		{
			if (_visual != null)
			{
				_visual.SetActive(!_isPhased);
			}

			if (_phaseEffect != null)
			{
				_phaseEffect.SetActive(_isPhased);
			}
		}

		// PRIVATE METHODS

		private void TryActivate()
		{
			if (_isPhased || IsOnCooldown)
				return;

			Activate();
		}

		private void Activate()
		{
			_isPhased = true;
			_durationTimer = TickTimer.CreateFromSeconds(Runner, _duration);

			_agent.Health.SetImmortality(_duration + 0.5f);

			if (_hitboxRoot != null)
			{
				_hitboxRoot.HitboxRootActive = false;
			}
		}

		private void Deactivate()
		{
			_isPhased = false;
			_cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);

			_agent.Health.StopImmortality();

			if (_hitboxRoot != null)
			{
				_hitboxRoot.HitboxRootActive = true;
			}
		}

		private void ForceDeactivate()
		{
			_isPhased = false;
			_durationTimer = default;
			_cooldownTimer = default;
		}
	}
}
