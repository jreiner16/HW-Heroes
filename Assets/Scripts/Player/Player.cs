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
		public PlayerAgent ActiveAgent { get; private set; }
		
		[Networked]
		public int SelectedCharacterIndex { get; private set; }
		
		public PlayerAgent AgentPrefab => GetAgentPrefab();

		// PRIVATE MEMBERS

		[SerializeField]
		private PlayerAgent _agentPrefab; // Kept for backward compatibility
		
		[SerializeField]
		private PlayerAgent[] _agentPrefabs; // Array of character prefabs

		private PlayerAgent _assignedAgent;
		private int _lastWeaponSlot;

		// PUBLIC METHODS

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
		/// Gets the number of available character prefabs.
		/// </summary>
		public int GetCharacterCount()
		{
			if (_agentPrefabs != null && _agentPrefabs.Length > 0)
			{
				return _agentPrefabs.Length;
			}
			// If no array set up, return 1 (single prefab)
			return _agentPrefab != null ? 1 : 0;
		}

		/// <summary>
		/// RPC to request character change. Only callable by input authority, executed on state authority.
		/// </summary>
		[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
		public void RPC_SelectCharacter(int characterIndex)
		{
			if (_agentPrefabs == null || _agentPrefabs.Length == 0)
			{
				Debug.LogWarning("No character prefabs array set up. Cannot switch character.");
				return;
			}

			if (characterIndex < 0 || characterIndex >= _agentPrefabs.Length)
			{
				Debug.LogWarning($"Invalid character index: {characterIndex}. Must be between 0 and {_agentPrefabs.Length - 1}");
				return;
			}

			if (_agentPrefabs[characterIndex] == null)
			{
				Debug.LogWarning($"Character prefab at index {characterIndex} is null.");
				return;
			}

			SelectedCharacterIndex = characterIndex;

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
