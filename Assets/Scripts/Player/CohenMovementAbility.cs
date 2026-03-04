using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Cohen's E ability: fires a healing ricochet projectile in the current aim direction.
	/// The projectile bounces off walls and heals allies on contact.
	/// Triggers a cooldown after each shot.
	///
	/// Setup: assign _projectilePrefab (a CohenRicochetProjectile prefab) in the inspector.
	/// That same prefab must also be registered in KinematicProjectileBuffer._projectilePrefabs
	/// on the Cohen_Agent GameObject.
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Cohen Movement Ability")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(5)]
	public class CohenMovementAbility : ContextBehaviour
	{
		public bool  IsOnCooldown          => _cooldownTimer.ExpiredOrNotRunning(Runner) == false;
		public bool  IsReady               => _cooldownTimer.ExpiredOrNotRunning(Runner);

		public float CooldownRemainingTime => _cooldownTimer.RemainingTime(Runner).GetValueOrDefault();
		public float CooldownTotal         => _cooldown;

		[Header("Ability Settings")]
		[SerializeField] private float _cooldown = 8f;

		[Header("Projectile")]
		[SerializeField] private KinematicProjectile _projectilePrefab;

		[Networked] private TickTimer _cooldownTimer { get; set; }

		private PlayerAgent               _agent;
		private KinematicProjectileBuffer _projectileBuffer;

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_agent            = GetComponent<PlayerAgent>();
			_projectileBuffer = GetComponent<KinematicProjectileBuffer>();
		}

		// NetworkBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			if (_agent == null || _agent.Owner == null || _agent.Health.IsAlive == false)
				return;

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.E))
			{
				TryFire();
			}
		}

		// PRIVATE METHODS

		private void TryFire()
		{
			if (IsOnCooldown)
				return;

			if (HasStateAuthority == false)
				return;

			if (_projectilePrefab == null || _projectileBuffer == null)
				return;

			var fireTransform = _agent.Weapons.FireTransform;
			_projectileBuffer.AddProjectile(_projectilePrefab, fireTransform.position, fireTransform.forward);

			_cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);
		}
	}
}
