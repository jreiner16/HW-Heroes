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
		private TextMeshProUGUI _keybindText;
		private Image _readyGlowImage;
		private CanvasGroup _readyGlowGroup;

		private void OnEnable()
		{
			EnsureKeybindLabel();
			EnsureReadyGlow();
		}

		private void EnsureReadyGlow()
		{
			if (_readyGlowImage != null)
				return;

			var go = new GameObject("ReadyGlow");
			go.layer = gameObject.layer;
			go.transform.SetParent(transform, false);
			go.transform.SetAsFirstSibling();

			var rect = go.AddComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = Vector2.zero;

			Vector2 baseSize = new Vector2(110f, 110f);
			if (_cooldownFill != null)
			{
				baseSize = _cooldownFill.rectTransform.sizeDelta;
				if (baseSize.sqrMagnitude <= 0.01f)
				{
					var parentRect = transform as RectTransform;
					if (parentRect != null && parentRect.rect.size.sqrMagnitude > 0.01f)
						baseSize = parentRect.rect.size;
				}
			}

			rect.sizeDelta = baseSize + new Vector2(26f, 26f);

			_readyGlowImage = go.AddComponent<Image>();
			_readyGlowImage.raycastTarget = false;
			_readyGlowImage.sprite = _cooldownFill != null ? _cooldownFill.sprite : null;
			_readyGlowImage.type = Image.Type.Simple;
			_readyGlowImage.preserveAspect = true;
			_readyGlowImage.color = new Color(0.15f, 0.65f, 1f, 0.65f);

			_readyGlowGroup = go.AddComponent<CanvasGroup>();
			_readyGlowGroup.alpha = 0f;
			_readyGlowGroup.interactable = false;
			_readyGlowGroup.blocksRaycasts = false;
		}

		private void SetReadyGlow(bool enabled)
		{
			if (_readyGlowGroup == null)
				return;

			_readyGlowGroup.alpha = enabled ? 1f : 0f;
		}

		private void EnsureKeybindLabel()
		{
			if (_keybindText != null)
				return;

			var go = new GameObject("KeybindLabel");
			go.layer = gameObject.layer;
			go.transform.SetParent(transform, false);

			var rect = go.AddComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 0f);
			rect.anchorMax = new Vector2(0.5f, 0f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = new Vector2(0f, -12f);
			rect.sizeDelta = new Vector2(80f, 30f);

			_keybindText = go.AddComponent<TextMeshProUGUI>();
			_keybindText.raycastTarget = false;
			_keybindText.text = "Q";
			_keybindText.fontSize = 28f;
			_keybindText.color = new Color(1f, 1f, 1f, 0.85f);
			_keybindText.alignment = TextAlignmentOptions.Center;
		}

		private void SetStatusText(string text, bool countdownActive)
		{
			if (_statusText == null)
				return;

			_statusText.gameObject.SetActive(true);
			_statusText.text = text;
			var offset = countdownActive ? new Vector2(_countdownTextOffsetX, 0f) : Vector2.zero;
			_statusText.rectTransform.anchoredPosition = offset;
		}

		public void UpdateAbility(GeoddeUltimateAbility ability)
		{
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
				SetReadyGlow(false);
			}
			else
			{
				if (_cooldownFill != null) _cooldownFill.fillAmount = 1f;
				if (_statusText != null) _statusText.gameObject.SetActive(false);
				SetReadyGlow(true);
			}
		}

		public void UpdateAbility(TheissUltimateAbility ability)
		{
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
				SetReadyGlow(false);
			}
			else
			{
				if (_cooldownFill != null) _cooldownFill.fillAmount = 1f;
				if (_statusText != null) _statusText.gameObject.SetActive(false);
				SetReadyGlow(true);
			}
		}
	}
}
