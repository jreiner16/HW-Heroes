using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Projectiles.UI
{
	/// <summary>
	/// UI widget that displays the movement ability state (ready, active, or on cooldown).
	/// </summary>
	public class UIMovementAbility : UIBehaviour
	{
		[SerializeField] private Image _cooldownFill;
		[SerializeField] private TextMeshProUGUI _statusText;

		public void UpdateAbility(GeoddeMovementAbility ability)
		{
			if (ability == null)
			{
				gameObject.SetActive(false);
				return;
			}

			gameObject.SetActive(true);

			if (ability.IsPhased)
			{
				float ratio = ability.DurationTotal > 0 ? ability.DurationRemainingTime / ability.DurationTotal : 0f;
				if (_cooldownFill != null) _cooldownFill.fillAmount = ratio;
				if (_statusText != null) _statusText.text = $"{ability.DurationRemainingTime:F1}s";
			}
			else if (ability.IsOnCooldown)
			{
				float ratio = ability.CooldownTotal > 0 ? ability.CooldownRemainingTime / ability.CooldownTotal : 0f;
				if (_cooldownFill != null) _cooldownFill.fillAmount = ratio;
				if (_statusText != null) _statusText.text = $"{ability.CooldownRemainingTime:F1}s";
			}
			else
			{
				if (_cooldownFill != null) _cooldownFill.fillAmount = 1f;
				if (_statusText != null) _statusText.text = "[E]";
			}
		}
	}
}
