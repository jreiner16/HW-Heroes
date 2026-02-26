using TMPro;
using UnityEngine;

namespace Projectiles.UI
{
	public class UIHealth : UIBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private TextMeshProUGUI _healthValue;

		private int _lastCurrent = -1;
		private int _lastMax = -1;

		// PUBLIC METHODS

		public void UpdateHealth(Health health)
		{
			int current = Mathf.RoundToInt(health.CurrentHealth);
			int max = Mathf.RoundToInt(health.MaxHealth);
			if (current == _lastCurrent && max == _lastMax)
				return;

			_healthValue.text = $"{current} / {max}";
			_lastCurrent = current;
			_lastMax = max;
		}
	}
}
