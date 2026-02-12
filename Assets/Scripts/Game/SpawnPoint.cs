using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Component used for spawn point lookup in the gameplay scene.
	/// </summary>
	public sealed class SpawnPoint : MonoBehaviour
	{
		[SerializeField]
		private ETeam _team = ETeam.None;

		public ETeam Team => _team;
	}
}
