using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Goedde's ultimate ability: fires a projectile in the aim direction. Triggered by the X key.
	/// Assign either a KinematicProjectile (must be in the agent's KinematicProjectileBuffer prefab list)
	/// or a StandaloneProjectile prefab.
	/// </summary>
	[DefaultExecutionOrder(5)]
	public class GoeddeUltimateAbility : AbilityBase, IUltimateAbility
	{
		public override EAbilitySlot Slot => EAbilitySlot.Ultimate;

		[Header("Ultimate Settings")]
		[SerializeField, Tooltip("How many seconds of ultimate cooldown are removed per 1 damage dealt.")]
		private float _cooldownSecondsPerDamage = 0.05f;

		[Header("References")]
		[SerializeField, Tooltip("Use KinematicProjectile (add to buffer's prefab list) or StandaloneProjectile. Kinematic preferred if both set.")]
		private KinematicProjectile _kinematicProjectilePrefab;
		[SerializeField]
		private StandaloneProjectile _standaloneProjectilePrefab;

		public override void Spawned()
		{
			// Start uncharged: when the ability spawns, immediately put it on cooldown.
			if (HasStateAuthority && _cooldown > 0f)
			{
				StartCooldown();
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (!ValidateCanAct())
				return;

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.X))
			{
				TryFire();
			}
		}

		public void AccelerateCooldownFromDamage(float damageDealt)
		{
			if (HasStateAuthority == false)
				return;
			if (_cooldownSecondsPerDamage <= 0f || damageDealt <= 0f)
				return;

			ReduceCooldownSeconds(damageDealt * _cooldownSecondsPerDamage);
		}

		private void TryFire()
		{
			if (IsOnCooldown || HasStateAuthority == false)
				return;

			if (_agent.Weapons?.FireTransform == null)
				return;

			var weapons = _agent.Weapons;
			var firePosition = weapons.FireTransform.position;
			var fireDirection = weapons.AimDirection;

			if (_kinematicProjectilePrefab != null)
			{
				var buffer = GetComponent<KinematicProjectileBuffer>();
				if (buffer != null)
				{
					buffer.AddProjectile(_kinematicProjectilePrefab, firePosition, fireDirection);
					StartCooldown();
				}
			}
			else if (_standaloneProjectilePrefab != null)
			{
				var projectile = Runner.Spawn(_standaloneProjectilePrefab, firePosition, Quaternion.LookRotation(fireDirection), Object.InputAuthority);
				projectile.Fire(firePosition, fireDirection);
				StartCooldown();
			}
		}
	}
}
