using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Geodde's ultimate ability: fires a projectile in the aim direction. Triggered by the X key.
	/// Assign either a KinematicProjectile (must be in the agent's KinematicProjectileBuffer prefab list)
	/// or a StandaloneProjectile prefab.
	/// </summary>
	[DefaultExecutionOrder(5)]
	public class GeoddeUltimateAbility : ContextBehaviour
	{
		// PUBLIC MEMBERS

		public bool  IsOnCooldown => _cooldownTimer.ExpiredOrNotRunning(Runner) == false;
		public bool  IsReady     => _cooldownTimer.ExpiredOrNotRunning(Runner);

		public float CooldownRemainingTime => _cooldownTimer.RemainingTime(Runner).GetValueOrDefault();
		public float CooldownTotal         => _cooldown;

		// PRIVATE MEMBERS

		[Header("Ability Settings")]
		[SerializeField]
		private float _cooldown = 15f;

		[Header("References")]
		[SerializeField, Tooltip("Use KinematicProjectile (add to buffer's prefab list) or StandaloneProjectile. Kinematic preferred if both set.")]
		private KinematicProjectile _kinematicProjectilePrefab;
		[SerializeField]
		private StandaloneProjectile _standaloneProjectilePrefab;

		[Networked]
		private TickTimer _cooldownTimer { get; set; }

		private PlayerAgent _agent;

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_agent = GetComponent<PlayerAgent>();
		}

		// NetworkBehaviour INTERFACE
		
		public override void Spawned()
		{
			// Start uncharged: when the ability spawns, immediately put it on cooldown.
			// Only state authority should set networked state.
			if (HasStateAuthority == true && _cooldown > 0f)
			{
				_cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (_agent.Owner == null || _agent.Health.IsAlive == false)
				return;

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.X))
			{
				TryFire();
			}
		}

		// PRIVATE METHODS

		private void TryFire()
		{
			if (IsOnCooldown)
				return;

			if (_agent.Weapons?.FireTransform == null)
				return;

			if (HasStateAuthority == false)
				return;

			var fireTransform = _agent.Weapons.FireTransform;
			var firePosition = fireTransform.position;
			var fireDirection = fireTransform.forward;

			if (_kinematicProjectilePrefab != null)
			{
				var buffer = GetComponent<KinematicProjectileBuffer>();
				if (buffer != null)
				{
					buffer.AddProjectile(_kinematicProjectilePrefab, firePosition, fireDirection);
					_cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);
				}
			}
			else if (_standaloneProjectilePrefab != null)
			{
				var projectile = Runner.Spawn(_standaloneProjectilePrefab, firePosition, Quaternion.LookRotation(fireDirection), Object.InputAuthority);
				projectile.Fire(firePosition, fireDirection);
				_cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);
			}
		}
	}
}
