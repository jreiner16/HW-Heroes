using Fusion;
using UnityEngine;
using UnityEngine.UI;
using Projectiles;

namespace Projectiles.UI
{
	/// <summary>
	/// Displays a health bar above an agent (player or enemy) in world space.
	/// Attach to any object with a Health component (e.g. PlayerAgent, DummyTarget).
	/// </summary>
	[RequireComponent(typeof(Health))]
	public class WorldSpaceHealthBar : MonoBehaviour
	{
		[Header("Layout")]
		[SerializeField] private float _heightOffset = 2.2f;
		[SerializeField] private float _barWidth = 1.5f;
		[SerializeField] private float _barHeight = 0.15f;
		[SerializeField] private float _canvasScale = 0.028f;

		[Header("Colors")]
		[SerializeField] private Color _allyColor = new Color(0f, 0.63f, 1f, 0.9f);
		[SerializeField] private Color _enemyColor = new Color(1f, 0.2f, 0.2f, 0.9f);
		[SerializeField] private Color _neutralColor = new Color(0.5f, 0.5f, 0.5f, 0.9f);
		[SerializeField] private Color _backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);

		[Header("Behaviour")]
		[SerializeField, Tooltip("Hide health bar for the local player (since they usually have a HUD bar).")]
		private bool _hideForLocalPlayer = false;
		[SerializeField, Tooltip("How quickly the main bar catches up to the true health value.")]
		private float _smoothFillSpeed = 10f;
		[SerializeField, Tooltip("Delay before the trailing (damage) bar starts shrinking, in seconds.")]
		private float _damageTrailDelay = 0.15f;
		[SerializeField, Tooltip("How quickly the trailing (damage) bar shrinks towards the main bar.")]
		private float _damageTrailSpeed = 4f;
		[SerializeField, Tooltip("Amount of scale punch when taking damage.")]
		private float _hitPulseScale = 1.15f;
		[SerializeField, Tooltip("How fast the hit pulse relaxes back to 1.0.")]
		private float _hitPulseDamp = 8f;

		private Health _health;
		private PlayerAgent _agent;
		private Canvas _canvas;
		private RectTransform _rootRect;
		private Image _fillImage;
		private Image _damageTrailImage;
		private Image _backgroundImage;

		private float _targetFill = 1f;
		private float _currentFill = 1f;
		private float _damageTrailFill = 1f;
		private float _lastDamageTime = -999f;
		private float _hitPulse = 0f;

		protected void Awake()
		{
			_health = GetComponent<Health>();
			_agent = GetComponent<PlayerAgent>();

			CreateBar();

			if (_health != null)
			{
				_health.HitTaken += OnHitTaken;
			}
		}

		private void OnDestroy()
		{
			if (_health != null)
			{
				_health.HitTaken -= OnHitTaken;
			}

			if (_canvas != null && _canvas.gameObject != null)
				Destroy(_canvas.gameObject);
		}

		private void Update()
		{
			if (_fillImage == null || _health == null)
				return;

			// Compute target fill from current health.
			_targetFill = _health.MaxHealth > 0
				? Mathf.Clamp01(_health.CurrentHealth / _health.MaxHealth)
				: 0f;

			// Smooth main bar towards target.
			_currentFill = Mathf.MoveTowards(_currentFill, _targetFill, _smoothFillSpeed * Time.deltaTime);
			_fillImage.fillAmount = _currentFill;
			_fillImage.color = GetBarColor();

			// Damage trail: only applies when losing health.
			if (_damageTrailImage != null)
			{
				// If we've just taken a hit (target dropped), freeze the trail at the pre‑hit value.
				if (_damageTrailFill < _currentFill)
				{
					// Healing: snap trail up so we don't leave ghost damage.
					_damageTrailFill = _currentFill;
				}

				// After a short delay, shrink the trail towards the main bar.
				if (Time.time - _lastDamageTime > _damageTrailDelay)
				{
					_damageTrailFill = Mathf.MoveTowards(_damageTrailFill, _currentFill, _damageTrailSpeed * Time.deltaTime);
				}

				_damageTrailImage.fillAmount = _damageTrailFill;
			}

			// Visibility rules: hide when dead; optionally hide for local player.
			bool isLocalPlayer = false;
			if (_agent != null && _agent.Owner != null && _agent.Owner.Object != null)
			{
				NetworkRunner runner = _agent.Runner;
				if (runner != null)
				{
					isLocalPlayer = _agent.Owner.Object.InputAuthority == runner.LocalPlayer;
				}
			}
			bool visible = _health.IsAlive && (!isLocalPlayer || _hideForLocalPlayer == false);
			if (_canvas != null && _canvas.gameObject.activeSelf != visible)
				_canvas.gameObject.SetActive(visible);
		}

		private void LateUpdate()
		{
			if (_canvas == null)
				return;

			Camera cam = null;
			if (_health != null && _health.Context != null && _health.Context.Camera != null)
				cam = _health.Context.Camera.Camera;
			if (cam == null)
				cam = GetActiveViewCamera();
			Transform camTransform = cam != null ? cam.transform : null;
			if (camTransform != null)
			{
				_canvas.transform.position = transform.position + Vector3.up * _heightOffset;
				_canvas.transform.rotation = Quaternion.LookRotation(_canvas.transform.position - camTransform.position);
			}

			// Subtle hit pulse on scale.
			if (_rootRect != null)
			{
				_hitPulse = Mathf.MoveTowards(_hitPulse, 0f, _hitPulseDamp * Time.deltaTime);
				float scale = 1f + _hitPulse;
				_rootRect.localScale = Vector3.one * (_canvasScale * scale);
			}
		}

		private void CreateBar()
		{
			var go = new GameObject("HealthBar");
			go.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : gameObject.layer;
			go.transform.SetParent(transform);
			go.transform.localPosition = Vector3.up * _heightOffset;

			_canvas = go.AddComponent<Canvas>();
			_canvas.renderMode = RenderMode.WorldSpace;
			_canvas.overrideSorting = true;
			_canvas.sortingOrder = 2000;
			var cam = GetActiveViewCamera();
			if (cam != null)
				_canvas.worldCamera = cam;

			var scaler = go.AddComponent<CanvasScaler>();
			scaler.dynamicPixelsPerUnit = 100;
			scaler.referencePixelsPerUnit = 100;
			scaler.scaleFactor = 1;
			scaler.referenceResolution = new Vector2(800, 600);

			go.AddComponent<GraphicRaycaster>();

			_rootRect = go.GetComponent<RectTransform>();
			_rootRect.sizeDelta = new Vector2(_barWidth / _canvasScale, _barHeight / _canvasScale);
			_rootRect.localScale = Vector3.one * _canvasScale;

			Sprite defaultSprite = HealthBarSpriteUtil.WhiteSprite;

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

			// Fill (main bar) — drawn under the white damage trail
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
			fillRect.offsetMin = new Vector2(2f, 2f);
			fillRect.offsetMax = new Vector2(-2f, -2f);

			// Damage trail (white) on top of fill
			var trailGo = new GameObject("DamageTrail");
			trailGo.transform.SetParent(bgGo.transform, false);
			_damageTrailImage = trailGo.AddComponent<Image>();
			_damageTrailImage.sprite = defaultSprite;
			_damageTrailImage.color = new Color(1f, 1f, 1f, 0.75f);
			_damageTrailImage.raycastTarget = false;
			_damageTrailImage.type = Image.Type.Filled;
			_damageTrailImage.fillMethod = Image.FillMethod.Horizontal;
			_damageTrailImage.fillOrigin = (int)Image.OriginHorizontal.Left;
			_damageTrailImage.fillAmount = 1f;
			var trailRect = trailGo.GetComponent<RectTransform>();
			trailRect.anchorMin = Vector2.zero;
			trailRect.anchorMax = Vector2.one;
			trailRect.offsetMin = new Vector2(2f, 2f);
			trailRect.offsetMax = new Vector2(-2f, -2f);

			// Initialize fill state from current health.
			_targetFill = _currentFill = _damageTrailFill = _health.MaxHealth > 0
				? Mathf.Clamp01(_health.CurrentHealth / _health.MaxHealth)
				: 0f;
			_fillImage.fillAmount = _currentFill;
			if (_damageTrailImage != null)
				_damageTrailImage.fillAmount = _damageTrailFill;

			_canvas.gameObject.SetActive(_health == null || _health.IsAlive);
		}

		private static Camera GetActiveViewCamera()
		{
			if (Camera.main != null)
				return Camera.main;
#if UNITY_2023_1_OR_NEWER
			return UnityEngine.Object.FindFirstObjectByType<Camera>();
#else
			return UnityEngine.Object.FindObjectOfType<Camera>();
#endif
		}

		private Color GetBarColor()
		{
			var context = _agent != null ? _agent.Context : _health != null ? _health.Context : null;
			if (context?.Gameplay == null || _agent == null)
				return _neutralColor;

			var gameplay = context.Gameplay;
			var localPlayer = gameplay.GetLocalPlayer();
			if (localPlayer == null)
				return _neutralColor;

			ETeam localTeam = localPlayer.Team;
			ETeam targetTeam = ETeam.None;

			if (gameplay.Players.TryGet(_agent.Object.InputAuthority, out Player targetPlayer) && targetPlayer != null)
				targetTeam = targetPlayer.Team;
			else if (_agent.Owner != null)
				targetTeam = _agent.Owner.Team;

			if (localTeam == ETeam.None || targetTeam == ETeam.None)
				return _neutralColor;

			return localTeam == targetTeam ? _allyColor : _enemyColor;
		}

		private void OnHitTaken(HitData hitData)
		{
			// Only react to actual damage on this health component.
			if (hitData.Target != (IHitTarget)_health)
				return;

			if (hitData.Amount <= 0f || hitData.Action != EHitAction.Damage)
				return;

			_lastDamageTime = Time.time;

			float max = _health.MaxHealth;
			if (max > 0f)
			{
				float prevFill = Mathf.Clamp01((_health.CurrentHealth + hitData.Amount) / max);
				_damageTrailFill = Mathf.Max(_damageTrailFill, prevFill);
			}

			_hitPulse = 0.12f * _hitPulseScale;
		}
	}

}
