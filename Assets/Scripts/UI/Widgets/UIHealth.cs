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

		[Header("Style")]
		[SerializeField]
		private float _barWidth = 260f;
		[SerializeField]
		private float _barHeight = 20f;
		[SerializeField]
		private int _tickCount = 5;
		[SerializeField]
		private Color _healthColor = new Color(0.3f, 0.85f, 0.35f, 1f);
		[SerializeField]
		private Color _lowHealthColor = new Color(0.9f, 0.25f, 0.2f, 1f);
		[SerializeField]
		private Color _bonusHealthColor = new Color(0.3f, 0.7f, 1f, 0.9f);
		[SerializeField]
		private float _lowHealthThreshold = 0.3f;

		private int _lastCurrent = -1;
		private int _lastMax = -1;
		private float _lastFill = -1f;
		private float _lastBonusFill = -1f;

		private static readonly Color BackgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.7f);
		private static readonly Color BarBgColor = new Color(0.12f, 0.12f, 0.16f, 0.9f);

		private Image _barBackground;
		private Image _baseFill;
		private Image _bonusFill;
		private TextMeshProUGUI _healthText;
		private bool _builtCustomBar;

		// MONOBEHAVIOUR

		protected void Awake()
		{
			UIUtility.AddBackgroundPanel(RectTransform, BackgroundColor);
			BuildHealthBar();
		}

		// PUBLIC METHODS

		public void UpdateHealth(Health health)
		{
			int current = Mathf.RoundToInt(health.CurrentHealth);
			int max = Mathf.RoundToInt(health.MaxHealth);
			float baseMax = health.BaseMaxHealth;
			float bonus = health.MaxHealthBonus;
			float totalMax = health.MaxHealth;

			// Update text
			var textTarget = _healthText != null ? _healthText : _healthValue;
			if (textTarget != null && (current != _lastCurrent || max != _lastMax))
			{
				textTarget.text = $"{current}/{max}";
				_lastCurrent = current;
				_lastMax = max;
			}

			// Fill the base health bar
			float fill = totalMax > 0 ? Mathf.Clamp01(health.CurrentHealth / totalMax) : 0f;

			if (_baseFill != null && Mathf.Approximately(fill, _lastFill) == false)
			{
				_baseFill.fillAmount = fill;

				// Color lerp: green → red at low health (only when no bonus)
				if (bonus <= 0f)
				{
					float healthRatio = totalMax > 0 ? health.CurrentHealth / totalMax : 0f;
					_baseFill.color = healthRatio <= _lowHealthThreshold
						? Color.Lerp(_lowHealthColor, _healthColor, healthRatio / _lowHealthThreshold)
						: _healthColor;
				}
				else
				{
					_baseFill.color = _healthColor;
				}

				_lastFill = fill;
			}
			else if (_fillImage != null && Mathf.Approximately(fill, _lastFill) == false)
			{
				_fillImage.fillAmount = fill;
				_lastFill = fill;
			}

			// Bonus health indicator
			if (_bonusFill != null)
			{
				float bonusFill = bonus > 0 && totalMax > 0 ? Mathf.Clamp01(bonus / totalMax) : 0f;
				if (Mathf.Approximately(bonusFill, _lastBonusFill) == false)
				{
					_bonusFill.fillAmount = bonusFill;
					_bonusFill.gameObject.SetActive(bonusFill > 0f);
					_lastBonusFill = bonusFill;
				}
			}
		}

		// PRIVATE METHODS

		private void BuildHealthBar()
		{
			if (_builtCustomBar) return;
			_builtCustomBar = true;

			// Hide the original fill image if it exists (from prefab)
			if (_fillImage != null)
				_fillImage.gameObject.SetActive(false);
			if (_healthValue != null)
				_healthValue.gameObject.SetActive(false);

			var parent = RectTransform;
			var whiteSprite = CreateWhiteSprite();

			// Bar container
			var containerGo = new GameObject("HealthBarContainer");
			containerGo.transform.SetParent(parent, false);
			var containerRect = containerGo.AddComponent<RectTransform>();
			containerRect.anchorMin = new Vector2(0.5f, 0.5f);
			containerRect.anchorMax = new Vector2(0.5f, 0.5f);
			containerRect.pivot = new Vector2(0.5f, 0.5f);
			containerRect.anchoredPosition = Vector2.zero;
			containerRect.sizeDelta = new Vector2(_barWidth, _barHeight);

			// Bar background
			_barBackground = containerGo.AddComponent<Image>();
			_barBackground.color = BarBgColor;
			if (whiteSprite != null) _barBackground.sprite = whiteSprite;
			_barBackground.raycastTarget = false;

			// Base health fill
			var baseFillGo = new GameObject("BaseFill");
			baseFillGo.transform.SetParent(containerGo.transform, false);
			var baseFillRect = baseFillGo.AddComponent<RectTransform>();
			baseFillRect.anchorMin = Vector2.zero;
			baseFillRect.anchorMax = Vector2.one;
			baseFillRect.offsetMin = Vector2.zero;
			baseFillRect.offsetMax = Vector2.zero;
			_baseFill = baseFillGo.AddComponent<Image>();
			_baseFill.color = _healthColor;
			if (whiteSprite != null) _baseFill.sprite = whiteSprite;
			_baseFill.type = Image.Type.Filled;
			_baseFill.fillMethod = Image.FillMethod.Horizontal;
			_baseFill.fillOrigin = (int)Image.OriginHorizontal.Left;
			_baseFill.raycastTarget = false;

			// Bonus health fill (renders on top, right-aligned portion)
			var bonusFillGo = new GameObject("BonusFill");
			bonusFillGo.transform.SetParent(containerGo.transform, false);
			var bonusFillRect = bonusFillGo.AddComponent<RectTransform>();
			bonusFillRect.anchorMin = Vector2.zero;
			bonusFillRect.anchorMax = Vector2.one;
			bonusFillRect.offsetMin = Vector2.zero;
			bonusFillRect.offsetMax = Vector2.zero;
			_bonusFill = bonusFillGo.AddComponent<Image>();
			_bonusFill.color = _bonusHealthColor;
			if (whiteSprite != null) _bonusFill.sprite = whiteSprite;
			_bonusFill.type = Image.Type.Filled;
			_bonusFill.fillMethod = Image.FillMethod.Horizontal;
			_bonusFill.fillOrigin = (int)Image.OriginHorizontal.Right;
			_bonusFill.raycastTarget = false;
			bonusFillGo.SetActive(false);

			// Tick marks
			if (_tickCount > 1)
			{
				for (int i = 1; i < _tickCount; i++)
				{
					float t = (float)i / _tickCount;
					var tickGo = new GameObject($"Tick{i}");
					tickGo.transform.SetParent(containerGo.transform, false);
					var tickRect = tickGo.AddComponent<RectTransform>();
					tickRect.anchorMin = new Vector2(t, 0f);
					tickRect.anchorMax = new Vector2(t, 1f);
					tickRect.pivot = new Vector2(0.5f, 0.5f);
					tickRect.sizeDelta = new Vector2(1.5f, 0f);
					var tickImg = tickGo.AddComponent<Image>();
					tickImg.color = new Color(0f, 0f, 0f, 0.4f);
					tickImg.raycastTarget = false;
				}
			}

			// Health text to the right of bar
			var textGo = new GameObject("HealthText");
			textGo.transform.SetParent(parent, false);
			var textRect = textGo.AddComponent<RectTransform>();
			textRect.anchorMin = new Vector2(0.5f, 0.5f);
			textRect.anchorMax = new Vector2(0.5f, 0.5f);
			textRect.pivot = new Vector2(0f, 0.5f);
			textRect.anchoredPosition = new Vector2(_barWidth * 0.5f + 8f, 0f);
			textRect.sizeDelta = new Vector2(80f, 24f);
			_healthText = textGo.AddComponent<TextMeshProUGUI>();
			_healthText.fontSize = 16f;
			_healthText.alignment = TextAlignmentOptions.Left;
			_healthText.color = new Color(0.9f, 0.9f, 0.95f, 1f);
			_healthText.raycastTarget = false;
		}

		private static Sprite CreateWhiteSprite()
		{
			var tex = new Texture2D(1, 1);
			tex.SetPixel(0, 0, Color.white);
			tex.Apply();
			return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
		}
	}
}
