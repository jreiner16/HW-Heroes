using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Component representing joined player. Each player can have a visual representation in the gameplay - player agent.
	/// </summary>
	public class Player : ContextBehaviour
	{
		// PUBLIC MEMBERS

		[Networked]
		public ETeam Team { get; private set; }

		[Networked]
		public PlayerAgent ActiveAgent { get; private set; }
		
		[Networked]
		public int SelectedCharacterIndex { get; private set; }
		
		public PlayerAgent AgentPrefab => GetAgentPrefab();

		// PRIVATE MEMBERS

		[SerializeField]
		private PlayerAgent _agentPrefab; // Kept for backward compatibility
		
		[SerializeField]
		private PlayerAgent[] _agentPrefabs; // Array of character prefabs

		[Networked]
		private TickTimer _switchCooldown { get; set; }

		private PlayerAgent _assignedAgent;
		private int _lastWeaponSlot;

		// PUBLIC METHODS

		public void SetTeam(ETeam team)
		{
			if (HasStateAuthority == false)
				return;

			Team = team;
		}

		public void AssignAgent(PlayerAgent agent)
	{
		ActiveAgent = agent;
		ActiveAgent.Owner = this;

		if (HasStateAuthority == true && _lastWeaponSlot != 0)
		{
			agent.Weapons.SwitchWeapon(_lastWeaponSlot, true);
		}
	}

		public void ClearAgent()
		{
			if (ActiveAgent == null)
				return;

			ActiveAgent.Owner = null;
			ActiveAgent = null;
		}

		/// <summary>
		/// Gets the agent prefab based on selected character index.
		/// Falls back to single prefab if array is not set up.
		/// </summary>
		public PlayerAgent GetAgentPrefab()
		{
			// If array is set up and has prefabs, use selected index
			if (_agentPrefabs != null && _agentPrefabs.Length > 0)
			{
				int index = Mathf.Clamp(SelectedCharacterIndex, 0, _agentPrefabs.Length - 1);
				if (_agentPrefabs[index] != null)
				{
					return _agentPrefabs[index];
				}
			}
			
			// Fallback to single prefab (backward compatibility)
			return _agentPrefab;
		}

	/// <summary>
	/// Gets the number of available character prefabs (counts only non-null entries).
	/// </summary>
	public int GetCharacterCount()
	{
		if (_agentPrefabs != null && _agentPrefabs.Length > 0)
		{
			int count = 0;
			foreach (var p in _agentPrefabs)
				if (p != null) count++;
			return count;
		}
		return _agentPrefab != null ? 1 : 0;
	}

	/// <summary>
	/// Returns the array index of the next (or previous) non-null character after the current selection.
	/// Returns -1 if no valid characters exist.
	/// </summary>
	public int GetNextValidCharacterIndex(bool forward)
	{
		if (_agentPrefabs == null || _agentPrefabs.Length == 0)
			return _agentPrefab != null ? 0 : -1;

		int len = _agentPrefabs.Length;
		int step = forward ? 1 : -1;
		int start = (SelectedCharacterIndex + step + len) % len;

		for (int i = 0; i < len; i++)
		{
			int idx = (start + i * step + len * len) % len;
			if (_agentPrefabs[idx] != null)
				return idx;
		}
		return -1;
	}

		/// <summary>
		/// RPC to request character change. Only callable by input authority, executed on state authority.
		/// </summary>
		[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
		public void RPC_SelectCharacter(int characterIndex)
		{
			if (_agentPrefabs == null || _agentPrefabs.Length == 0)
			{
				Debug.LogWarning("[CharSwitch] No character prefabs array set up in Player prefab. Cannot switch character.");
				return;
			}

			if (characterIndex < 0 || characterIndex >= _agentPrefabs.Length)
			{
				Debug.LogWarning($"[CharSwitch] Invalid character index: {characterIndex}. Must be between 0 and {_agentPrefabs.Length - 1}");
				return;
			}

			// Don't switch to same character
			if (characterIndex == SelectedCharacterIndex)
				return;

			// Don't switch while dead
			if (ActiveAgent != null && ActiveAgent.Health != null && ActiveAgent.Health.IsAlive == false)
				return;

			// Enforce cooldown between switches
			if (_switchCooldown.ExpiredOrNotRunning(Runner) == false)
				return;

			if (_agentPrefabs[characterIndex] == null)
			{
				// Log all slot states to help diagnose which slots are missing
				var slots = new System.Text.StringBuilder();
				for (int i = 0; i < _agentPrefabs.Length; i++)
					slots.Append($"[{i}]={(_agentPrefabs[i] != null ? _agentPrefabs[i].name : "NULL")} ");
				Debug.LogWarning($"[CharSwitch] _agentPrefabs[{characterIndex}] is null. All slots: {slots} — Open Player.prefab in Inspector and reassign the missing slot.");
				return;
			}

			SelectedCharacterIndex = characterIndex;
			_switchCooldown = TickTimer.CreateFromSeconds(Runner, 0.3f);
			Debug.Log($"[CharSwitch] Switching to index {characterIndex} ({_agentPrefabs[characterIndex].name}). ActiveAgent={ActiveAgent != null}");

			// If agent is already spawned, request respawn with new character
			if (ActiveAgent != null && Context.Gameplay != null)
			{
				Context.Gameplay.RequestCharacterSwitch(this);
			}
		}

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			if (Context.Gameplay != null)
			{
				Context.Gameplay.Join(this);
			}
		}

		public override void FixedUpdateNetwork()
		{
			bool agentValid = ActiveAgent != null && ActiveAgent.Object != null;
			if (agentValid == true && HasStateAuthority == true)
			{
				_lastWeaponSlot = ActiveAgent.Weapons.CurrentWeaponSlot;
			}
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			if (hasState == false)
				return;

			if (Context.Gameplay != null)
			{
				Context.Gameplay.Leave(this);
			}

			if (HasStateAuthority == true && ActiveAgent != null)
			{
				Runner.Despawn(ActiveAgent.Object);
			}

			ActiveAgent = null;
		}
	}
}
