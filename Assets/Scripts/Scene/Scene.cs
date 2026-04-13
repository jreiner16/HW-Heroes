using UnityEngine;

namespace Projectiles
{
	[DefaultExecutionOrder(-100)]
	public class Scene : MonoBehaviour
	{
		// PUBLIC MEMBERS

		public SceneContext Context => _context;

		// PRIVATE MEMBERS

		[SerializeField]
		private SceneContext _context;

		// MONOBEHAVIOUR

		protected void Update()
		{
			// Validate network related objects before non-network services will try to access it
			ValidateContext();
		}

		// PRIVATE METHODS

		private void ValidateContext()
		{
			var runner = Context.Runner;
			if (runner == null || runner.IsRunning == false)
			{
				Context.LocalAgent = null;
				return;
			}

			var localPlayer = Context.Runner.GetPlayerObject(runner.LocalPlayer);
			var player = localPlayer != null ? localPlayer.GetComponent<Player>() : null;
			var agent = player != null ? player.ActiveAgent : null;

			// Only expose the agent if its NetworkObject is valid (spawned, not despawned).
			// During character switches there can be a brief window where ActiveAgent still
			// references the old despawned agent, which would crash downstream code that
			// tries to read [Networked] properties.
			if (agent != null && (agent.Object == null || agent.Object.IsValid == false))
				agent = null;

			Context.LocalAgent = agent;
		}
	}
}
