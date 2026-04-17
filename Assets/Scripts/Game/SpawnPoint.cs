using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Component used for spawn point lookup in the gameplay scene.
	/// Adds a subtle colored point light to make spawn areas visually distinct.
	/// </summary>
	public sealed class SpawnPoint : MonoBehaviour
	{
		[SerializeField]
		private ETeam _team = ETeam.None;

		[Header("Ambient Light")]
		[SerializeField]
		private float _lightIntensity = 1.5f;
		[SerializeField]
		private float _lightRange = 8f;
		[SerializeField]
		private float _lightHeight = 3f;

		public ETeam Team => _team;

		private void Awake()
		{
			if (_team == ETeam.None) return;

			Color c = _team == ETeam.Team1
				? new Color(0.3f, 0.5f, 1f)
				: new Color(1f, 0.4f, 0.3f);

			var lightGo = new GameObject("SpawnLight");
			lightGo.transform.SetParent(transform, false);
			lightGo.transform.localPosition = Vector3.up * _lightHeight;

			var light = lightGo.AddComponent<Light>();
			light.type = LightType.Point;
			light.color = c;
			light.intensity = _lightIntensity;
			light.range = _lightRange;
			light.shadows = LightShadows.None;
		}
	}
}
