using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Goedde-specific: Ctrl+Shift+G spawns and equips the Rifle weapon at runtime.
	/// If the rifle is already equipped, switches to it. Attach to the Goedde_Agent
	/// prefab and assign the Rifle weapon prefab in the Inspector.
	/// </summary>
	[DefaultExecutionOrder(6)]
	public class GoeddeRifleEquip : ContextBehaviour
	{
		[SerializeField, Tooltip("Rifle weapon prefab (Assets/Prefabs/Weapons/Rifle).")]
		private Weapon _riflePrefab;

		private PlayerAgent _agent;
		private Weapons _weapons;

		protected void Awake()
		{
			_agent = GetComponent<PlayerAgent>();
			_weapons = GetComponent<Weapons>();
		}

		public override void FixedUpdateNetwork()
		{
			if (IsProxy)
				return;

			if (_agent == null || _weapons == null || _riflePrefab == null)
				return;

			if (_agent.Health.IsAlive == false)
				return;

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.EquipRifle) == false)
				return;

			int rifleSlot = _riflePrefab.WeaponSlot;

			if (_weapons.HasWeaponInSlot(rifleSlot))
			{
				_weapons.SwitchWeapon(rifleSlot, false);
			}
			else if (HasStateAuthority)
			{
				_weapons.SpawnAndEquipWeapon(_riflePrefab);
			}
		}
	}
}
