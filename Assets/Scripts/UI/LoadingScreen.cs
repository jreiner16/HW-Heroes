using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Projectiles.UI
{
	/// <summary>
	/// Full-screen loading overlay shown while the gameplay scene is loading and
	/// until the local player has a spawned agent. Builds its UI programmatically
	/// so no prefab setup is required.
	/// </summary>
	public class LoadingScreen : MonoBehaviour
	{
		// PUBLIC MEMBERS

		public static LoadingScreen Instance { get; private set; }

		// PRIVATE MEMBERS

		private Canvas _canvas;
		private CanvasGroup _canvasGroup;
		private TextMeshProUGUI _messageText;
		private TextMeshProUGUI _spinnerText;
		private float _spinnerTime;
		private bool _fadingOut;
		private float _fadeOutElapsed;
		private const float FadeOutDuration = 0.35f;

		private static readonly string[] SpinnerFrames = { "|", "/", "-", "\\" };

		// MONOBEHAVIOUR

		protected void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}

			Instance = this;
			DontDestroyOnLoad(gameObject);
			BuildUI();
			HideImmediate();
		}

		protected void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		protected void Update()
		{
			if (_canvas == null || _canvas.enabled == false)
				return;

			if (_spinnerText != null && _fadingOut == false)
			{
				_spinnerTime += Time.deltaTime;
				int frame = Mathf.FloorToInt(_spinnerTime * 6f) % SpinnerFrames.Length;
				_spinnerText.text = SpinnerFrames[frame];
			}

			if (_fadingOut == true)
			{
				_fadeOutElapsed += Time.unscaledDeltaTime;
				float t = Mathf.Clamp01(_fadeOutElapsed / FadeOutDuration);
				_canvasGroup.alpha = 1f - t;

				if (t >= 1f)
				{
					_fadingOut = false;
					_canvas.enabled = false;
					_canvasGroup.blocksRaycasts = false;
				}
			}
		}

		// PUBLIC METHODS

		public void Show(string message = "Loading...")
		{
			if (_canvas == null)
				BuildUI();

			if (_messageText != null)
				_messageText.text = message;

			_canvas.enabled = true;
			_canvasGroup.alpha = 1f;
			_canvasGroup.blocksRaycasts = true;
			_spinnerTime = 0f;
			_fadingOut = false;
			_fadeOutElapsed = 0f;
		}

		public void Hide()
		{
			if (_canvas == null)
				return;

			// Fade out smoothly so there's no abrupt flash of whatever is underneath.
			if (_canvas.enabled && _fadingOut == false)
			{
				_fadingOut = true;
				_fadeOutElapsed = 0f;
			}
		}

		public void HideImmediate()
		{
			if (_canvas == null)
				return;

			_fadingOut = false;
			_canvas.enabled = false;
			_canvasGroup.alpha = 0f;
			_canvasGroup.blocksRaycasts = false;
		}

		public void SetMessage(string message)
		{
			if (_messageText != null)
				_messageText.text = message;
		}

		public bool IsVisible => _canvas != null && _canvas.enabled;

		/// <summary>
		/// Gets or creates the singleton instance, building UI on first access.
		/// </summary>
		public static LoadingScreen GetOrCreate()
		{
			if (Instance != null)
				return Instance;

			var go = new GameObject("LoadingScreen");
			return go.AddComponent<LoadingScreen>();
		}

		// PRIVATE METHODS

		private void BuildUI()
		{
			// Root canvas — renders above everything at a high sorting order
			_canvas = gameObject.GetComponent<Canvas>();
			if (_canvas == null)
				_canvas = gameObject.AddComponent<Canvas>();
			_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			_canvas.sortingOrder = 32000;

			if (gameObject.GetComponent<CanvasScaler>() == null)
			{
				var scaler = gameObject.AddComponent<CanvasScaler>();
				scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
				scaler.referenceResolution = new Vector2(1920f, 1080f);
				scaler.matchWidthOrHeight = 0.5f;
			}

			if (gameObject.GetComponent<GraphicRaycaster>() == null)
				gameObject.AddComponent<GraphicRaycaster>();

			_canvasGroup = gameObject.GetComponent<CanvasGroup>();
			if (_canvasGroup == null)
				_canvasGroup = gameObject.AddComponent<CanvasGroup>();

			// Full-screen black background
			var bgGo = new GameObject("Background");
			bgGo.transform.SetParent(transform, false);
			var bgRect = bgGo.AddComponent<RectTransform>();
			bgRect.anchorMin = Vector2.zero;
			bgRect.anchorMax = Vector2.one;
			bgRect.offsetMin = Vector2.zero;
			bgRect.offsetMax = Vector2.zero;
			var bgImage = bgGo.AddComponent<Image>();
			bgImage.color = new Color(0.04f, 0.04f, 0.07f, 1f);
			bgImage.raycastTarget = true;

			// Title text
			var titleGo = new GameObject("Title");
			titleGo.transform.SetParent(transform, false);
			var titleRect = titleGo.AddComponent<RectTransform>();
			titleRect.anchorMin = new Vector2(0.5f, 0.5f);
			titleRect.anchorMax = new Vector2(0.5f, 0.5f);
			titleRect.pivot = new Vector2(0.5f, 0.5f);
			titleRect.anchoredPosition = new Vector2(0f, 60f);
			titleRect.sizeDelta = new Vector2(1200f, 120f);
			var titleText = titleGo.AddComponent<TextMeshProUGUI>();
			titleText.text = "HW HEROES";
			titleText.fontSize = 96f;
			titleText.fontStyle = FontStyles.Bold;
			titleText.alignment = TextAlignmentOptions.Center;
			titleText.color = new Color(0.95f, 0.95f, 1f, 1f);
			titleText.raycastTarget = false;

			// Loading message
			var msgGo = new GameObject("Message");
			msgGo.transform.SetParent(transform, false);
			var msgRect = msgGo.AddComponent<RectTransform>();
			msgRect.anchorMin = new Vector2(0.5f, 0.5f);
			msgRect.anchorMax = new Vector2(0.5f, 0.5f);
			msgRect.pivot = new Vector2(0.5f, 0.5f);
			msgRect.anchoredPosition = new Vector2(0f, -40f);
			msgRect.sizeDelta = new Vector2(1200f, 60f);
			_messageText = msgGo.AddComponent<TextMeshProUGUI>();
			_messageText.text = "Loading...";
			_messageText.fontSize = 36f;
			_messageText.alignment = TextAlignmentOptions.Center;
			_messageText.color = new Color(0.8f, 0.8f, 0.9f, 1f);
			_messageText.raycastTarget = false;

			// Spinner
			var spinnerGo = new GameObject("Spinner");
			spinnerGo.transform.SetParent(transform, false);
			var spinnerRect = spinnerGo.AddComponent<RectTransform>();
			spinnerRect.anchorMin = new Vector2(0.5f, 0.5f);
			spinnerRect.anchorMax = new Vector2(0.5f, 0.5f);
			spinnerRect.pivot = new Vector2(0.5f, 0.5f);
			spinnerRect.anchoredPosition = new Vector2(0f, -120f);
			spinnerRect.sizeDelta = new Vector2(200f, 60f);
			_spinnerText = spinnerGo.AddComponent<TextMeshProUGUI>();
			_spinnerText.text = "|";
			_spinnerText.fontSize = 48f;
			_spinnerText.fontStyle = FontStyles.Bold;
			_spinnerText.alignment = TextAlignmentOptions.Center;
			_spinnerText.color = new Color(0.6f, 0.8f, 1f, 1f);
			_spinnerText.raycastTarget = false;
		}
	}
}
