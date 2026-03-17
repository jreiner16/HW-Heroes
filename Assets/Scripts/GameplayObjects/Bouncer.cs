using UnityEngine;
using System.Collections.Generic;

namespace Projectiles
{
	/// <summary>
	/// Launches the player upward when they touch this object. Attach to a GameObject with a Collider (set as Trigger) or use regular collision.
	/// </summary>
	[RequireComponent(typeof(Collider))]
	public class Bouncer : MonoBehaviour
	{
		[SerializeField, Tooltip("Upward force applied to the player when they touch the bouncer")]
		private float _bounceForce = 12f;
		[SerializeField, Tooltip("Ignore duplicate bounce events for the same player within this many frames")]
		private int _duplicateBounceFrameWindow = 1;

		private readonly Dictionary<int, int> _lastBounceFrameByAgent = new Dictionary<int, int>();

		private void OnTriggerEnter(Collider other)
		{
			TryBounce(other);
		}

		private void OnCollisionEnter(Collision collision)
		{
			TryBounce(collision.collider);
		}

		private void TryBounce(Collider other)
		{
			var agent = other.GetComponentInParent<PlayerAgent>();
			if (agent != null && agent.Health.IsAlive)
			{
				int agentId = agent.GetInstanceID();
				if (_lastBounceFrameByAgent.TryGetValue(agentId, out int lastBounceFrame))
				{
					if (Time.frameCount - lastBounceFrame <= _duplicateBounceFrameWindow)
						return;
				}

				_lastBounceFrameByAgent[agentId] = Time.frameCount;
				agent.AddBounceImpulse(_bounceForce);
			}
		}
	}
}
