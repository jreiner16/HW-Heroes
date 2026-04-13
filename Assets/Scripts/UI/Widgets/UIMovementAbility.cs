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
		[SerializeField] private float _countdownTextOffsetX = 14f;
		private string _readyText;
		private bool _readyTextCached;
		private Vector2 _readyAnchoredPosition;
		private bool _readyPositionCached;

		private static readonly Color BackgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.7f);
		private static readonly Color ReadyColor = new Color(0.2f, 0.9f, 0.3f, 0.9f);
		private static readonly Color ActiveColor = new Color(1f, 0.8f, 0.2f, 0.9f);
		private static readonly Color CooldownColor = new Color(0.8f, 0.2f, 0.2f, 0.9f);

		protected void Awake()
		{
			UIUtility.AddBackgroundPanel(RectTransform, BackgroundColor);

			if (_cooldownFill != null)
			{
				_cooldownFill.type = Image.Type.Filled;
				_cooldownFill.fillMethod = Image.FillMethod.Radial360;
				_cooldownFill.fillOrigin = (int)Image.Origin360.Top;
				_cooldownFill.fillClockwise = false;
			}
		}

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

		public void UpdateAbility(IAbility ability)
		{
			CacheReadyText();

			if (ability == null)
			{
				gameObject.SetActive(false);
				return;
			}

			gameObject.SetActive(true);

			Color fillColor;

			if (ability.IsActive && ability.HasDuration)
			{
				float ratio = ability.DurationTotal > 0 ? ability.DurationRemainingTime / ability.DurationTotal : 0f;
				if (_cooldownFill != null) _cooldownFill.fillAmount = ratio;
				SetStatusText($"{ability.DurationRemainingTime:F1}s", true);
				fillColor = ActiveColor;
			}
			else if (ability.IsOnCooldown)
			{
				float ratio = ability.CooldownTotal > 0 ? ability.CooldownRemainingTime / ability.CooldownTotal : 0f;
				if (_cooldownFill != null) _cooldownFill.fillAmount = ratio;
				SetStatusText($"{ability.CooldownRemainingTime:F1}s", true);
				fillColor = CooldownColor;
			}
			else
			{
				if (_cooldownFill != null) _cooldownFill.fillAmount = 1f;
				SetStatusText(_readyText, false);
				fillColor = ReadyColor;
			}

			if (_cooldownFill != null) _cooldownFill.color = fillColor;
		}

		/// <summary>
		/// Fallback: show the right-click ability slot based on the current weapon's secondary action.
		/// </summary>
		public void UpdateSecondaryAction(Weapon weapon)
		{
			CacheReadyText();

			if (weapon == null)
			{
				gameObject.SetActive(false);
				return;
			}

			// Only show this slot if the current weapon actually has a secondary action.
			if (weapon.SecondaryActionDescription.HasValue() == false)
			{
				gameObject.SetActive(false);
				return;
			}

			gameObject.SetActive(true);
			if (_cooldownFill != null) _cooldownFill.fillAmount = 1f;
			SetStatusText(_readyText, false);
		}
	}
}
