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
		private Image _fillImage;

		private int _lastCurrent = -1;
		private int _lastMax = -1;
		private float _lastFill = -1f;

		// MONOBEHAVIOUR

		protected void Awake()
		{
			if (_fillImage == null)
				_fillImage = GetComponentInChildren<Image>();
		}

		// PUBLIC METHODS

		public void UpdateHealth(Health health)
		{
			int current = Mathf.RoundToInt(health.CurrentHealth);
			int max = Mathf.RoundToInt(health.MaxHealth);
			float fill = max > 0 ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 0f;

			if (_healthValue != null && (current != _lastCurrent || max != _lastMax))
			{
				_healthValue.text = $"{current} / {max}";
				_lastCurrent = current;
				_lastMax = max;
			}

			if (_fillImage != null && Mathf.Approximately(fill, _lastFill) == false)
			{
				_fillImage.fillAmount = fill;
				_lastFill = fill;
			}
		}
	}
}
