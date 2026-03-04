using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Projectiles.UI
{
	public class UIHealth : UIBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private TextMeshProUGUI _healthValue;

		[SerializeField]
		private Image _healthFill;

		private int _lastCurrent = -1;
		private int _lastMax = -1;
		private float _lastFill = -1f;

		// PUBLIC METHODS

		public void UpdateHealth(Health health)
		{
			int current = Mathf.RoundToInt(health.CurrentHealth);
			int max = Mathf.RoundToInt(health.MaxHealth);
			float fill = health.MaxHealth > 0f ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 0f;

			if (current == _lastCurrent && max == _lastMax && Mathf.Approximately(fill, _lastFill))
				return;

			_healthValue.text = $"{current} / {max}";
			if (_healthFill != null)
			{
				_healthFill.fillAmount = fill;
			}
			_lastCurrent = current;
			_lastMax = max;
			_lastFill = fill;
		}
	}
}
