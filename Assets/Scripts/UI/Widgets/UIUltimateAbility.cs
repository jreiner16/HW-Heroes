using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Projectiles.UI
{
	/// <summary>
	/// UI widget that displays the ultimate ability state (ready or on cooldown).
	/// </summary>
	public class UIUltimateAbility : UIBehaviour
	{
		[SerializeField] private Image _cooldownFill;
		[SerializeField] private TextMeshProUGUI _statusText;
		[SerializeField] private float _countdownTextOffsetX = 14f;
		private string _readyText;
		private bool _readyTextCached;
		private Vector2 _readyAnchoredPosition;
		private bool _readyPositionCached;

		private void CacheReadyText()
		{
			if (_readyTextCached || _statusText == null)
				return;

			_readyText = _statusText.text;
			_readyTextCached = true;

			var textRect = _statusText.rectTransform;
			_readyAnchoredPosition = textRect.anchoredPosition;
			_readyPositionCached = true;
		}

		private void SetStatusText(string text, bool countdownActive)
		{
			if (_statusText == null)
				return;

			_statusText.text = text;

			if (_readyPositionCached == false)
				return;

			var offset = countdownActive ? new Vector2(_countdownTextOffsetX, 0f) : Vector2.zero;
			_statusText.rectTransform.anchoredPosition = _readyAnchoredPosition + offset;
		}

		public void UpdateAbility(GeoddeUltimateAbility ability)
		{
			CacheReadyText();

			if (ability == null)
			{
				gameObject.SetActive(false);
				return;
			}

			gameObject.SetActive(true);

			if (ability.IsOnCooldown)
			{
				float ratio = ability.CooldownTotal > 0 ? ability.CooldownRemainingTime / ability.CooldownTotal : 0f;
				if (_cooldownFill != null) _cooldownFill.fillAmount = ratio;
				SetStatusText($"{ability.CooldownRemainingTime:F1}s", true);
			}
			else
			{
				if (_cooldownFill != null) _cooldownFill.fillAmount = 1f;
				SetStatusText(_readyText, false);
			}
		}

		public void UpdateAbility(TheissUltimateAbility ability)
		{
			CacheReadyText();

			if (ability == null)
			{
				gameObject.SetActive(false);
				return;
			}

			gameObject.SetActive(true);

			if (ability.IsOnCooldown)
			{
				float ratio = ability.CooldownTotal > 0 ? ability.CooldownRemainingTime / ability.CooldownTotal : 0f;
				if (_cooldownFill != null) _cooldownFill.fillAmount = ratio;
				SetStatusText($"{ability.CooldownRemainingTime:F1}s", true);
			}
			else
			{
				if (_cooldownFill != null) _cooldownFill.fillAmount = 1f;
				SetStatusText(_readyText, false);
			}
		}

		public void UpdateAbility(CohenUltimateAbility ability)
		{
			CacheReadyText();

			if (ability == null)
			{
				gameObject.SetActive(false);
				return;
			}

			gameObject.SetActive(true);

			if (ability.IsOnCooldown)
			{
				float ratio = ability.CooldownTotal > 0 ? ability.CooldownRemainingTime / ability.CooldownTotal : 0f;
				if (_cooldownFill != null) _cooldownFill.fillAmount = ratio;
				SetStatusText($"{ability.CooldownRemainingTime:F1}s", true);
			}
			else
			{
				if (_cooldownFill != null) _cooldownFill.fillAmount = 1f;
				SetStatusText(_readyText, false);
			}
		}
	}
}
