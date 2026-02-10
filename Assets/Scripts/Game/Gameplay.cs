using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Represents the actual gameplay loop. Handles PlayerAgent spawning and despawning for each Player that joins gameplay.
	/// </summary>
	public class Gameplay : ContextBehaviour
	{
		// PUBLIC MEMBERS

		[Networked, Capacity(200)]
		public NetworkDictionary<PlayerRef, Player> Players { get; }

		// PRIVATE METHODS

		private SpawnPoint[] _spawnPoints;
		private int _lastSpawnPoint = -1;

	private List<SpawnRequest> _spawnRequests = new();
	private List<CharacterSwitchRequest> _characterSwitchRequests = new();

	// PUBLIC METHODS

		public void Join(Player player)
		{
			if (HasStateAuthority == false)
				return;

			var playerRef = player.Object.InputAuthority;

			if (Players.ContainsKey(playerRef) == true)
			{
				Debug.LogError($"Player {playerRef} already joined");
				return;
			}

			Players.Add(playerRef, player);

			OnPlayerJoined(player);
		}

	public void Leave(Player player)
	{
		if (HasStateAuthority == false)
			return;

		if (Players.ContainsKey(player.Object.InputAuthority) == false)
			return;

		Players.Remove(player.Object.InputAuthority);

		OnPlayerLeft(player);
	}

	/// <summary>
	/// Gets the local player's Player component. Returns null if not available.
	/// </summary>
	public Player GetLocalPlayer()
	{
		if (Context == null || Context.Runner == null || Context.Runner.IsRunning == false)
			return null;

		var localPlayerObject = Context.Runner.GetPlayerObject(Context.Runner.LocalPlayer);
		if (localPlayerObject == null)
			return null;

		return localPlayerObject.GetComponent<Player>();
	}

	/// <summary>
	/// Public method to switch character for the local player. Can be called from UI buttons.
	/// </summary>
	public void SwitchLocalPlayerCharacter(int characterIndex)
	{
		var player = GetLocalPlayer();
		if (player == null)
			return;

		// Call RPC to request character change
		player.RPC_SelectCharacter(characterIndex);
	}

	/// <summary>
	/// Cycles to the next character for the local player. Can be called from UI buttons.
	/// </summary>
	public void SwitchLocalPlayerToNextCharacter()
	{
		var player = GetLocalPlayer();
		if (player == null)
			return;

		int currentIndex = player.SelectedCharacterIndex;
		int characterCount = player.GetCharacterCount();
		if (characterCount > 0)
		{
			int newIndex = (currentIndex + 1) % characterCount;
			player.RPC_SelectCharacter(newIndex);
		}
	}

	/// <summary>
	/// Cycles to the previous character for the local player. Can be called from UI buttons.
	/// </summary>
	public void SwitchLocalPlayerToPreviousCharacter()
	{
		var player = GetLocalPlayer();
		if (player == null)
			return;

		int currentIndex = player.SelectedCharacterIndex;
		int characterCount = player.GetCharacterCount();
		if (characterCount > 0)
		{
			int newIndex = (currentIndex - 1 + characterCount) % characterCount;
			player.RPC_SelectCharacter(newIndex);
		}
	}

	// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			// Register to context
			Context.Gameplay = this;
		}

		public override void FixedUpdateNetwork()
		{
			if (HasStateAuthority == false)
				return;

			int currentTick = Runner.Tick;

			for (int i = _spawnRequests.Count - 1; i >= 0; i--)
			{
				var request = _spawnRequests[i];

				if (request.Tick > currentTick)
					continue;

				_spawnRequests.RemoveAt(i);

				if (request.Player == null || request.Player.Object == null)
					continue; // Player no longer valid

				if (Players.ContainsKey(request.Player.Object.InputAuthority) == false)
					continue; // Player left gameplay

				SpawnPlayerAgent(request.Player);
			}

			// Process character switch requests
			for (int i = _characterSwitchRequests.Count - 1; i >= 0; i--)
			{
				var request = _characterSwitchRequests[i];

				if (request.Tick > currentTick)
					continue;

				_characterSwitchRequests.RemoveAt(i);

				if (request.Player == null || request.Player.Object == null)
					continue; // Player no longer valid

				if (Players.ContainsKey(request.Player.Object.InputAuthority) == false)
					continue; // Player left gameplay

				// Spawn new agent at stored position
				PlayerAgent agentPrefab = request.Player.AgentPrefab;
				PlayerAgent newAgent;

				if (request.HasPosition)
				{
					newAgent = Runner.Spawn(agentPrefab, request.Position, request.Rotation, request.Player.Object.InputAuthority) as PlayerAgent;
				}
				else
				{
					newAgent = SpawnAgent(request.Player.Object.InputAuthority, agentPrefab) as PlayerAgent;
				}

				request.Player.AssignAgent(newAgent);
				newAgent.Health.FatalHitTaken += OnFatalHitTaken;
				OnPlayerAgentSpawned(newAgent);
			}
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			// Clear from context
			Context.Gameplay = null;
		}

		// PROTECTED METHODS

		protected virtual void OnPlayerJoined(Player player)
		{
			SpawnPlayerAgent(player);
		}

		protected virtual void OnPlayerLeft(Player player)
		{
			DespawnPlayerAgent(player);
		}

		protected virtual void OnPlayerDeath(Player player)
		{
			AddSpawnRequest(player, 3f);
		}

		protected virtual void OnPlayerAgentSpawned(PlayerAgent agent)
		{
			agent.Health.SetImmortality(3f);
		}

		protected virtual void OnPlayerAgentDespawned(PlayerAgent agent)
		{
		}

	protected void SpawnPlayerAgent(Player player)
	{
		DespawnPlayerAgent(player);

		var agent = SpawnAgent(player.Object.InputAuthority, player.AgentPrefab) as PlayerAgent;
		player.AssignAgent(agent);

		agent.Health.FatalHitTaken += OnFatalHitTaken;

		OnPlayerAgentSpawned(agent);
	}

	/// <summary>
	/// Requests a character switch for a player. Uses delayed spawning to ensure proper network synchronization.
	/// </summary>
	public void RequestCharacterSwitch(Player player)
	{
		if (HasStateAuthority == false)
			return;

		if (player == null || player.Object == null)
			return;

		// Store current position and rotation if agent exists
		Vector3 position = Vector3.zero;
		Quaternion rotation = Quaternion.identity;
		bool hasPosition = false;

		if (player.ActiveAgent != null && player.ActiveAgent.Object != null)
		{
			position = player.ActiveAgent.transform.position;
			rotation = player.ActiveAgent.transform.rotation;
			hasPosition = true;
		}

		// Despawn current agent first
		DespawnPlayerAgent(player);

		// Add character switch request with small delay (0.15 seconds) to ensure despawn syncs across network
		float switchDelay = 0.15f;
		int delayTicks = Mathf.RoundToInt(Runner.TickRate * switchDelay);

		_characterSwitchRequests.Add(new CharacterSwitchRequest()
		{
			Player = player,
			Position = position,
			Rotation = rotation,
			HasPosition = hasPosition,
			Tick = Runner.Tick + delayTicks,
		});
	}

		protected void DespawnPlayerAgent(Player player)
		{
			if (player.ActiveAgent == null)
				return;

			player.ActiveAgent.Health.FatalHitTaken -= OnFatalHitTaken;

			OnPlayerAgentDespawned(player.ActiveAgent);

			DespawnAgent(player.ActiveAgent);
			player.ClearAgent();
		}

		protected void AddSpawnRequest(Player player, float spawnDelay)
		{
			int delayTicks = Mathf.RoundToInt(Runner.TickRate * spawnDelay);

			_spawnRequests.Add(new SpawnRequest()
			{
				Player = player,
				Tick = Runner.Tick + delayTicks,
			});
		}

		// PRIVATE METHODS

		private void OnFatalHitTaken(HitData hitData)
		{
			var health = hitData.Target as Health;

			if (health == null)
				return;

			if (Players.TryGet(health.Object.InputAuthority, out Player player) == true)
			{
				OnPlayerDeath(player);
			}
		}

		private PlayerAgent SpawnAgent(PlayerRef inputAuthority, PlayerAgent agentPrefab)
		{
			if (_spawnPoints == null)
			{
				_spawnPoints = Runner.SimulationUnityScene.FindObjectsOfTypeInOrder<SpawnPoint>(false);
			}

			_lastSpawnPoint = (_lastSpawnPoint + 1) % _spawnPoints.Length;
			var spawnPoint = _spawnPoints[_lastSpawnPoint].transform;

			var agent = Runner.Spawn(agentPrefab, spawnPoint.position, spawnPoint.rotation, inputAuthority);
			return agent;
		}

		private void DespawnAgent(PlayerAgent agent)
		{
			if (agent == null)
				return;

			Runner.Despawn(agent.Object);
		}

	// HELPERS

	public struct SpawnRequest
	{
		public Player Player;
		public int Tick;
	}

	public struct CharacterSwitchRequest
	{
		public Player Player;
		public Vector3 Position;
		public Quaternion Rotation;
		public bool HasPosition;
		public int Tick;
	}
}
}
