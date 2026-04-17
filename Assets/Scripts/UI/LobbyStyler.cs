using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Projectiles.UI
{
	/// <summary>
	/// Styles the existing lobby UI elements to match the game's visual identity.
	/// Does NOT create new elements — only reskins what already exists in the scene.
	/// </summary>
	public class LobbyStyler : MonoBehaviour
	{
		[Header("Style")]
		[SerializeField] private Color _buttonNormalColor = new Color(0.12f, 0.14f, 0.22f, 1f);
		[SerializeField] private Color _buttonHighlightColor = new Color(0.18f, 0.22f, 0.35f, 1f);
		[SerializeField] private Color _buttonPressedColor = new Color(0.08f, 0.10f, 0.16f, 1f);
		[SerializeField] private Color _buttonTextColor = new Color(0.85f, 0.88f, 0.95f, 1f);
		[SerializeField] private Color _inputFieldColor = new Color(0.15f, 0.15f, 0.22f, 0.85f);

		private void Start()
		{
			StyleAllButtons();
			StyleAllInputFields();
		}

		private void StyleAllButtons()
		{
			var buttons = GetComponentsInChildren<Button>(true);
			foreach (var btn in buttons)
			{
				var colors = btn.colors;
				colors.normalColor = _buttonNormalColor;
				colors.highlightedColor = _buttonHighlightColor;
				colors.pressedColor = _buttonPressedColor;
				colors.selectedColor = _buttonHighlightColor;
				colors.disabledColor = new Color(_buttonNormalColor.r, _buttonNormalColor.g, _buttonNormalColor.b, 0.4f);
				colors.fadeDuration = 0.1f;
				btn.colors = colors;

				// Shift buttons down slightly to even out spacing with resized input field
				var btnRect = btn.GetComponent<RectTransform>();
				if (btnRect != null)
				{
					var pos = btnRect.anchoredPosition;
					pos.y -= 7.5f;
					btnRect.anchoredPosition = pos;
				}

				var img = btn.GetComponent<Image>();
				if (img != null)
					img.color = Color.white;

				var text = btn.GetComponentInChildren<TextMeshProUGUI>();
				if (text != null)
					text.color = _buttonTextColor;

				var legacyText = btn.GetComponentInChildren<Text>();
				if (legacyText != null)
					legacyText.color = _buttonTextColor;
			}
		}

		private void StyleAllInputFields()
		{
			var inputs = GetComponentsInChildren<TMP_InputField>(true);
			foreach (var input in inputs)
			{
				var img = input.GetComponent<Image>();
				if (img != null)
					img.color = _inputFieldColor;

				var inputRect = input.GetComponent<RectTransform>();
				if (inputRect != null)
				{
					var size = inputRect.sizeDelta;
					size.y = 65f;
					inputRect.sizeDelta = size;
				}

				if (input.textComponent != null)
					input.textComponent.color = new Color(0.95f, 0.95f, 1f, 1f);

				if (input.placeholder is TextMeshProUGUI placeholder)
				{
					placeholder.color = new Color(0.7f, 0.7f, 0.8f, 0.7f);
					placeholder.text = "Room name...";
				}
			}
		}
	}
}
