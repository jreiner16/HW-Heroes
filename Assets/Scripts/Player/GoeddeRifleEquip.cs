using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Goedde-specific cheat/utility: Ctrl+Shift+G toggles the Rifle on and off.
	///
	/// Behavior:
	///  - First press: remembers Goedde's current weapon slot, spawns the Rifle as
	///    a <see cref="NetworkObject"/>, equips it immediately, and locks digit-key
	///    weapon switching so 1-9 cannot swap to or away from the rifle.
	///  - Second press: switches back to the saved weapon slot, despawns the Rifle,
	///    and unlocks digit-key switching.
	///
	/// Ctrl+Shift+G is the ONLY way to toggle the rifle; while active no other input
	/// (digit keys, etc.) can equip or unequip it.
	///
	/// Networking:
	///  - Runs on input authority (client) and state authority (host); proxies skip.
	///    All mutating work happens on the state authority, so non-host clients
	///    will see a ~1 RTT delay on first toggle while the press is forwarded.
	///
	/// Lifetime:
	///  - The spawned Rifle is owned by <see cref="Weapons"/>, which despawns every
	///    held weapon in <see cref="Weapons.Despawned"/>. Death, respawn, and
	///    character switch therefore destroy the rifle; the new agent starts with
	///    the toggle in its default (off) state.
	///
	/// Attach to Goedde_Agent.prefab alongside <see cref="PlayerAgent"/> and
	/// <see cref="Weapons"/>, then assign <see cref="_riflePrefab"/> in the Inspector.
	/// </summary>
	[DefaultExecutionOrder(6)]
	public class GoeddeRifleEquip : ContextBehaviour
	{
		[SerializeField, Tooltip("Rifle weapon prefab (Assets/Prefabs/Weapons/Rifle).")]
		private Weapon _riflePrefab;

		// True while the rifle is currently spawned and equipped via this toggle.
		[Networked, HideInInspector]
		public NetworkBool IsRifleActive { get; private set; }

		// The weapon slot Goedde was on before the rifle was toggled on. -1 means
		// "no saved slot" (i.e. toggle is off).
		[Networked]
		private int _savedSlot { get; set; }

		private PlayerAgent _agent;
		private Weapons _weapons;

		// Caches sibling references. Awake is used instead of Spawned so the
		// refs are valid regardless of Fusion lifecycle ordering.
		protected void Awake()
		{
			_agent = GetComponent<PlayerAgent>();
			_weapons = GetComponent<Weapons>();
		}

		public override void Spawned()
		{
			if (HasStateAuthority)
			{
				_savedSlot = -1;
				IsRifleActive = false;
			}
		}

		// Edge-detects the EquipRifle button each tick and toggles the rifle.
		// Mutations are gated to state authority; input authority just forwards
		// the button press via the normal Fusion input pipeline.
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

			// Only the server toggles; clients wait for replication.
			if (HasStateAuthority == false)
				return;

			if (IsRifleActive)
			{
				ToggleOff();
			}
			else
			{
				ToggleOn();
			}
		}

		// Spawns the Rifle, equips it, and locks digit-key switching.
		private void ToggleOn()
		{
			int rifleSlot = _riflePrefab.WeaponSlot;

			// Remember where to put Goedde back. If she somehow already sits on
			// the rifle's slot (e.g. default weapon loadout shares it), fall back
			// to slot 0 so ToggleOff always has a valid restore target.
			int currentSlot = _weapons.CurrentWeaponSlot;
			_savedSlot = currentSlot == rifleSlot ? 0 : currentSlot;

			_weapons.SpawnAndEquipWeapon(_riflePrefab);
			_weapons.LockInputSwitching = true;
			IsRifleActive = true;
		}

		// Switches back to the saved slot, despawns the rifle, and unlocks
		// digit-key switching.
		private void ToggleOff()
		{
			int rifleSlot = _riflePrefab.WeaponSlot;
			int restoreSlot = _savedSlot;

			// Fall back if the saved weapon is no longer there (death + respawn
			// edge cases, or future runtime weapon removal).
			if (restoreSlot < 0 || _weapons.HasWeaponInSlot(restoreSlot) == false)
			{
				restoreSlot = 0;
			}

			// Unlock FIRST so the SwitchWeapon below is not itself blocked by
			// the lock (ProcessInput checks the lock, but our direct call does
			// not — the order here is defensive for future changes).
			_weapons.LockInputSwitching = false;
			_weapons.SwitchWeapon(restoreSlot, true);
			_weapons.RemoveAndDespawnWeapon(rifleSlot);

			IsRifleActive = false;
			_savedSlot = -1;
		}
	}
}
