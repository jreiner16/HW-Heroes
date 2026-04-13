using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Cohen's right-click ability: fires a healing ricochet projectile in the current aim direction.
	/// The projectile bounces off walls and heals allies on contact.
	/// </summary>
	[DefaultExecutionOrder(5)]
	public class CohenRicochetAbility : AbilityBase
	{
		public override EAbilitySlot Slot => EAbilitySlot.RightClick;

		[Header("Projectile")]
		[SerializeField] private KinematicProjectile _projectilePrefab;

		private KinematicProjectileBuffer _projectileBuffer;

		protected override void Awake()
		{
			base.Awake();
			_projectileBuffer = GetComponent<KinematicProjectileBuffer>();
		}

		public override void FixedUpdateNetwork()
		{
			if (!ValidateCanAct())
				return;

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.Ability))
			{
				TryFire();
			}
		}

		private void TryFire()
		{
			if (IsOnCooldown)
				return;

			if (HasStateAuthority == false)
				return;

			if (_projectilePrefab == null || _projectileBuffer == null)
				return;

			var weapons = _agent.Weapons;
			if (weapons == null || weapons.FireTransform == null)
				return;

			_projectileBuffer.AddProjectile(_projectilePrefab, weapons.FireTransform.position, weapons.AimDirection);
			StartCooldown();
		}
	}
}
