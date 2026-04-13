using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Projectiles.UI
{
	/// <summary>
	/// Styles the lobby UI to match the game's visual identity.
	/// Attach to the same GameObject as FusionBootstrapUI.
	/// Creates a styled background, title, and upgrades button visuals.
	/// </summary>
	public class LobbyStyler : MonoBehaviour
	{
		[Header("Style")]
		[SerializeField] private Color _backgroundColor = new Color(0.04f, 0.04f, 0.07f, 1f);
		[SerializeField] private Color _titleColor = new Color(0.95f, 0.95f, 1f, 1f);
		[SerializeField] private Color _subtitleColor = new Color(0.6f, 0.8f, 1f, 0.8f);
		[SerializeField] private Color _buttonNormalColor = new Color(0.12f, 0.14f, 0.22f, 1f);
		[SerializeField] private Color _buttonHighlightColor = new Color(0.18f, 0.22f, 0.35f, 1f);
		[SerializeField] private Color _buttonPressedColor = new Color(0.08f, 0.10f, 0.16f, 1f);
		[SerializeField] private Color _buttonTextColor = new Color(0.85f, 0.88f, 0.95f, 1f);
		[SerializeField] private Color _inputFieldColor = new Color(0.08f, 0.08f, 0.12f, 0.9f);
		[SerializeField] private Color _panelColor = new Color(0.06f, 0.06f, 0.10f, 0.85f);

		private void Start()
		{
			var canvas = GetComponentInParent<Canvas>();
			if (canvas == null) return;

			StyleBackground(canvas);
			StyleTitle(canvas);
			StyleAllButtons();
			StyleAllInputFields();
			StylePanels();
		}

		private void StyleBackground(Canvas canvas)
		{
			// Add a full-screen background behind everything
			var bgGo = new GameObject("LobbyBackground");
			bgGo.transform.SetParent(canvas.transform, false);
			bgGo.transform.SetAsFirstSibling();

			var rect = bgGo.AddComponent<RectTransform>();
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;

			var img = bgGo.AddComponent<Image>();
			img.color = _backgroundColor;
			img.raycastTarget = false;
		}

		private void StyleTitle(Canvas canvas)
		{
			// Add game title at the top
			var titleGo = new GameObject("LobbyTitle");
			titleGo.transform.SetParent(canvas.transform, false);
			titleGo.transform.SetSiblingIndex(1);

			var rect = titleGo.AddComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 1f);
			rect.anchorMax = new Vector2(0.5f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = new Vector2(0f, -30f);
			rect.sizeDelta = new Vector2(800f, 120f);

			var title = titleGo.AddComponent<TextMeshProUGUI>();
			title.text = "HW HEROES";
			title.fontSize = 72f;
			title.fontStyle = FontStyles.Bold;
			title.alignment = TextAlignmentOptions.Center;
			title.color = _titleColor;
			title.raycastTarget = false;

			// Subtitle
			var subGo = new GameObject("LobbySubtitle");
			subGo.transform.SetParent(canvas.transform, false);
			subGo.transform.SetSiblingIndex(2);

			var subRect = subGo.AddComponent<RectTransform>();
			subRect.anchorMin = new Vector2(0.5f, 1f);
			subRect.anchorMax = new Vector2(0.5f, 1f);
			subRect.pivot = new Vector2(0.5f, 1f);
			subRect.anchoredPosition = new Vector2(0f, -140f);
			subRect.sizeDelta = new Vector2(800f, 40f);

			var subtitle = subGo.AddComponent<TextMeshProUGUI>();
			subtitle.text = "TEAM ARENA SHOOTER";
			subtitle.fontSize = 22f;
			subtitle.fontStyle = FontStyles.Normal;
			subtitle.alignment = TextAlignmentOptions.Center;
			subtitle.color = _subtitleColor;
			subtitle.characterSpacing = 8f;
			subtitle.raycastTarget = false;
		}

		private void StyleAllButtons()
		{
			var buttons = GetComponentsInChildren<Button>(true);
			foreach (var btn in buttons)
			{
				StyleButton(btn);
			}
		}

		private void StyleButton(Button btn)
		{
			var colors = btn.colors;
			colors.normalColor = _buttonNormalColor;
			colors.highlightedColor = _buttonHighlightColor;
			colors.pressedColor = _buttonPressedColor;
			colors.selectedColor = _buttonHighlightColor;
			colors.disabledColor = new Color(_buttonNormalColor.r, _buttonNormalColor.g, _buttonNormalColor.b, 0.4f);
			colors.fadeDuration = 0.1f;
			btn.colors = colors;

			// Make the button image use the color tint
			var img = btn.GetComponent<Image>();
			if (img != null)
			{
				img.color = Color.white;
			}

			// Enlarge small buttons so text fits comfortably
			var btnRect = btn.GetComponent<RectTransform>();
			if (btnRect != null && btnRect.sizeDelta.x < 200f)
			{
				btnRect.sizeDelta = new Vector2(200f, Mathf.Max(btnRect.sizeDelta.y, 36f));
			}

			// Style button text — keep original fontStyle to avoid overflow
			var text = btn.GetComponentInChildren<TextMeshProUGUI>();
			if (text != null)
			{
				text.color = _buttonTextColor;
				text.enableAutoSizing = true;
				text.fontSizeMin = 10f;
				text.fontSizeMax = text.fontSize > 0 ? text.fontSize : 18f;
			}

			// Also check legacy Text
			var legacyText = btn.GetComponentInChildren<Text>();
			if (legacyText != null)
			{
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

				// Resize to match button width
				var inputRect = input.GetComponent<RectTransform>();
				if (inputRect != null && inputRect.sizeDelta.x < 200f)
				{
					inputRect.sizeDelta = new Vector2(200f, Mathf.Max(inputRect.sizeDelta.y, 36f));
				}

				if (input.textComponent != null)
					input.textComponent.color = new Color(0.9f, 0.9f, 0.95f, 1f);

				if (input.placeholder is TextMeshProUGUI placeholder)
				{
					placeholder.color = new Color(0.5f, 0.5f, 0.6f, 0.6f);
					placeholder.text = "Room name...";
				}

				// Add a label above the input field
				AddInputLabel(input.transform, "ROOM NAME");
			}
		}

		private void AddInputLabel(Transform inputTransform, string labelText)
		{
			var labelGo = new GameObject("InputLabel");
			labelGo.transform.SetParent(inputTransform.parent, false);

			var labelRect = labelGo.AddComponent<RectTransform>();
			var inputRect = inputTransform.GetComponent<RectTransform>();

			labelRect.anchorMin = inputRect.anchorMin;
			labelRect.anchorMax = inputRect.anchorMax;
			labelRect.pivot = new Vector2(0.5f, 0f);
			labelRect.anchoredPosition = new Vector2(
				inputRect.anchoredPosition.x,
				inputRect.anchoredPosition.y + inputRect.sizeDelta.y * 0.5f + 2f);
			labelRect.sizeDelta = new Vector2(inputRect.sizeDelta.x, 22f);

			var label = labelGo.AddComponent<TextMeshProUGUI>();
			label.text = labelText;
			label.fontSize = 14f;
			label.alignment = TextAlignmentOptions.Center;
			label.color = _subtitleColor;
			label.characterSpacing = 4f;
			label.raycastTarget = false;

			// Make sure label renders before the input
			labelGo.transform.SetSiblingIndex(inputTransform.GetSiblingIndex());
		}

		private void StylePanels()
		{
			// Style any panel backgrounds that exist
			var images = GetComponentsInChildren<Image>(true);
			foreach (var img in images)
			{
				// Skip buttons, input fields, and our created elements
				if (img.GetComponent<Button>() != null) continue;
				if (img.GetComponentInParent<Button>() != null) continue;
				if (img.GetComponent<TMP_InputField>() != null) continue;
				if (img.gameObject.name == "LobbyBackground") continue;

				// Only restyle large panel-like images (not small icons/fills)
				if (img.rectTransform.rect.width > 100f && img.rectTransform.rect.height > 100f)
				{
					img.color = _panelColor;
				}
			}
		}
	}
}
