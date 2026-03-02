using Fusion;
using UnityEngine;
using UnityEngine.UI;

namespace Projectiles.UI
{
	/// <summary>
	/// Displays a health bar above an agent (player or enemy) in world space.
	/// Attach to any object with a Health component (e.g. PlayerAgent, DummyTarget).
	/// </summary>
	[RequireComponent(typeof(Health))]
	public class WorldSpaceHealthBar : ContextBehaviour
	{
		[Header("Layout")]
		[SerializeField] private float _heightOffset = 2.2f;
		[SerializeField] private float _barWidth = 1.5f;
		[SerializeField] private float _barHeight = 0.15f;
		[SerializeField] private float _canvasScale = 0.01f;

		[Header("Colors")]
		[SerializeField] private Color _allyColor = new Color(0f, 0.63f, 1f, 0.9f);
		[SerializeField] private Color _enemyColor = new Color(1f, 0.2f, 0.2f, 0.9f);
		[SerializeField] private Color _neutralColor = new Color(0.5f, 0.5f, 0.5f, 0.9f);
		[SerializeField] private Color _backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);

		private Health _health;
		private PlayerAgent _agent;
		private Canvas _canvas;
		private Image _fillImage;
		private Image _backgroundImage;

		protected void Awake()
		{
			_health = GetComponent<Health>();
			_agent = GetComponent<PlayerAgent>();
		}

		public override void Spawned()
		{
			CreateBar();
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			if (_canvas != null && _canvas.gameObject != null)
				Destroy(_canvas.gameObject);
		}

		public override void Render()
		{
			if (_fillImage == null || _health == null)
				return;

			_fillImage.fillAmount = _health.MaxHealth > 0
				? Mathf.Clamp01(_health.CurrentHealth / _health.MaxHealth)
				: 0f;

			// Hide bar for local player (we have HUD) and when dead
			bool visible = _health.IsAlive && (_agent == null || HasInputAuthority == false);
			if (_canvas != null && _canvas.gameObject.activeSelf != visible)
				_canvas.gameObject.SetActive(visible);
		}

		private void LateUpdate()
		{
			if (_canvas == null)
				return;

			// Billboard: face the camera
			Transform camTransform = Context?.Camera?.transform;
			if (camTransform == null)
				camTransform = Camera.main?.transform;
			if (camTransform != null)
			{
				_canvas.transform.position = transform.position + Vector3.up * _heightOffset;
				_canvas.transform.rotation = Quaternion.LookRotation(_canvas.transform.position - camTransform.position);
			}
		}

		private void CreateBar()
		{
			var go = new GameObject("HealthBar");
			go.layer = gameObject.layer;
			go.transform.SetParent(transform);
			go.transform.localPosition = Vector3.up * _heightOffset;

			_canvas = go.AddComponent<Canvas>();
			_canvas.renderMode = RenderMode.WorldSpace;

			var scaler = go.AddComponent<CanvasScaler>();
			scaler.dynamicPixelsPerUnit = 100;
			scaler.referencePixelsPerUnit = 100;
			scaler.scaleFactor = 1;
			scaler.referenceResolution = new Vector2(800, 600);

			go.AddComponent<GraphicRaycaster>();

			var rect = go.GetComponent<RectTransform>();
			rect.sizeDelta = new Vector2(_barWidth / _canvasScale, _barHeight / _canvasScale);
			rect.localScale = Vector3.one * _canvasScale;

			Sprite defaultSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
			if (defaultSprite == null)
				defaultSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");

			// Background
			var bgGo = new GameObject("Background");
			bgGo.transform.SetParent(go.transform, false);
			_backgroundImage = bgGo.AddComponent<Image>();
			_backgroundImage.sprite = defaultSprite;
			_backgroundImage.color = _backgroundColor;
			_backgroundImage.raycastTarget = false;
			var bgRect = bgGo.GetComponent<RectTransform>();
			bgRect.anchorMin = Vector2.zero;
			bgRect.anchorMax = Vector2.one;
			bgRect.offsetMin = Vector2.zero;
			bgRect.offsetMax = Vector2.zero;

			// Fill
			var fillGo = new GameObject("Fill");
			fillGo.transform.SetParent(bgGo.transform, false);
			_fillImage = fillGo.AddComponent<Image>();
			_fillImage.sprite = defaultSprite;
			_fillImage.color = GetBarColor();
			_fillImage.raycastTarget = false;
			_fillImage.type = Image.Type.Filled;
			_fillImage.fillMethod = Image.FillMethod.Horizontal;
			_fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
			_fillImage.fillAmount = 1f;
			var fillRect = fillGo.GetComponent<RectTransform>();
			fillRect.anchorMin = Vector2.zero;
			fillRect.anchorMax = Vector2.one;
			fillRect.offsetMin = Vector2.zero;
			fillRect.offsetMax = Vector2.zero;

			_canvas.gameObject.SetActive(_health.IsAlive);
		}

		private Color GetBarColor()
		{
			if (_agent == null || Context?.Gameplay == null)
				return _neutralColor;

			var localPlayer = Context.Gameplay.GetLocalPlayer();
			if (localPlayer == null)
				return _neutralColor;

			ETeam localTeam = localPlayer.Team;
			ETeam targetTeam = ETeam.None;

			if (Context.Gameplay.Players.TryGet(_agent.Object.InputAuthority, out Player targetPlayer) && targetPlayer != null)
				targetTeam = targetPlayer.Team;
			else if (_agent.Owner != null)
				targetTeam = _agent.Owner.Team;

			if (localTeam == ETeam.None || targetTeam == ETeam.None)
				return _neutralColor;

			return localTeam == targetTeam ? _allyColor : _enemyColor;
		}
	}

}
