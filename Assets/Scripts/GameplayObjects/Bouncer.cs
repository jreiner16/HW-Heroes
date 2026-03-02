using UnityEngine;

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
				agent.AddBounceImpulse(_bounceForce);
			}
		}
	}
}
