using DG.Tweening;
using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Projectiles;

namespace Projectiles.UI
{
	/// <summary>
	/// Shows all gameplay related information.
	/// </summary>
	public class UIGameplayView : UIBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private GameObject _observedAgentRoot;
		[SerializeField]
		private CanvasGroup _aliveGroup;
		[SerializeField]
		private float _aliveGroupFadeIn = 0.2f;
		[SerializeField]
		private float _aliveGroupFadeOut = 0.5f;

		private UICrosshair _crosshair;
		private UIHitNumbers _hitNumbers;
		private UIHealth _health;
		private UIWeapons _weapons;
		private UIScreenEffects _screenEffects;
		[SerializeField]
		private UIMovementAbility _movementAbility;
	[SerializeField]
	private UIMovementAbility _rightClickAbility;
		[SerializeField]
		private UIMovementAbility[] _movementWidgets;
		[SerializeField]
		private UIUltimateAbility _ultimateAbility;
		
		[Header("Score Header (Capture Objective)")]
		[SerializeField] private bool _showScoreHeader = true;
		[SerializeField] private UIScoreHeader _scoreHeader;

		[Header("Character Blurb (HUD)")]
		[SerializeField] private bool _showCharacterBlurb = true;
		[SerializeField] private Vector2 _characterBlurbOffset = new Vector2(18f, 18f);
		[SerializeField] private Vector2 _characterBlurbSize = new Vector2(520f, 90f);
		[SerializeField] private float _characterBlurbFontSize = 22f;
		[SerializeField] private Color _characterBlurbColor = new Color(1f, 1f, 1f, 0.9f);
		private TextMeshProUGUI _characterBlurbText;

		[Header("Spawn Room TAB Hint")]
		[SerializeField] private string _tabHintMessage = "Press TAB to swap teacher";
		[SerializeField] private Vector2 _tabHintOffset = new Vector2(0f, 110f);
		[SerializeField] private Vector2 _tabHintSize = new Vector2(600f, 50f);
		[SerializeField] private float _tabHintFontSize = 20f;
		[SerializeField] private Color _tabHintColor = new Color(1f, 1f, 0.6f, 0.9f);
		private TextMeshProUGUI _tabHintText;

		[Header("Round Start Countdown")]
		[SerializeField] private bool _showRoundCountdown = true;
		[SerializeField] private Vector2 _roundCountdownOffset = new Vector2(0f, 70f);
		[SerializeField] private Vector2 _roundCountdownSize = new Vector2(600f, 40f);
		[SerializeField] private float _roundCountdownFontSize = 26f;
		[SerializeField] private Color _roundCountdownColor = new Color(1f, 0.85f, 0.4f, 0.95f);
		private TextMeshProUGUI _roundCountdownText;

		[Header("Hero health bar (bottom center)")]
		private GameObject _playerHpRoot;
		private RectTransform _playerHpRootRt;
		private Image _playerHpGlow;
		private Image _playerHpFill;
		private Image _playerHpTrail;
		private RectTransform _playerHpFillClipRt;
		private RectTransform _playerHpTrackRt;
		private float _playerHpSmooth;
		private float _playerHpTrailFill;
		private float _playerHpLastHit;
		private float _playerHpGlowFlashUntil;
		private bool _playerHpHudFirstSync;
		private const float _playerHpBarInnerWidth = 504f;
		private const float _playerHpBarHeight = 18f;

		private SceneContext _context;
		private PlayerAgent _observedAgent;
		private NetworkBehaviourId _observedAgentId;

		private bool _aliveGroupVisible;

		// MONOBEHAVIOUR

		protected void Awake()
		{
			ClearObservedAgent(true);

			_context = GameUI.Context;

			_crosshair = GetComponentInChildren<UICrosshair>(true);
			_hitNumbers = GetComponentInChildren<UIHitNumbers>(true);
			_health = GetComponentInChildren<UIHealth>(true);
			_weapons = GetComponentInChildren<UIWeapons>(true);
			_screenEffects = GetComponentInChildren<UIScreenEffects>(true);

			// Old numeric "250 / 250" health widget — replaced by bottom hero bar.
			if (_health != null)
				_health.gameObject.SetActive(false);

			// Remove gun/weapon info UI entirely.
			if (_weapons != null)
			{
				_weapons.gameObject.SetActive(false);
			}

			if (_showScoreHeader == true)
			{
				EnsureScoreHeaderUI();
			}

		    if (_showCharacterBlurb == true)
		    {
			    EnsureCharacterBlurbUI();
		    }

		    EnsureTabHintUI();

		    if (_showRoundCountdown)
		    {
			    EnsureRoundCountdownUI();
		    }

		    EnsurePlayerHealthHud();

		if (_movementAbility == null && _movementWidgets != null && _movementWidgets.Length > 0)
		{
			_movementAbility = _movementWidgets[0];
		}
		if (_rightClickAbility == null && _movementWidgets != null && _movementWidgets.Length > 1)
		{
			_rightClickAbility = _movementWidgets[1];
		}

		if (_ultimateAbility == null)
		{
			_ultimateAbility = GetComponentInChildren<UIUltimateAbility>(true);
		}

		_aliveGroup.alpha = 0f;
	}

		protected void Update()
		{
			if (_context.Runner == null || _context.Runner.IsRunning == false)
				return;

			SetObservedAgent(_context.LocalAgent);

			if (_observedAgent == null)
			{
				if (_playerHpRoot != null)
					_playerHpRoot.SetActive(false);
				return;
			}

		_screenEffects.UpdateEffects(_observedAgent);
		UpdatePlayerHealthHud();

		UpdateCharacterBlurb();
			UpdateTabHint();

			if (_showRoundCountdown)
			{
				UpdateRoundCountdown();
			}

			if (_movementAbility != null)
			{
				var goeddeMovement = _observedAgent.GetComponent<GoeddeMovementAbility>();
				var cohenMovement = _observedAgent.GetComponent<CohenMovementAbility>();
				var theissBuff = _observedAgent.GetComponent<TheissBuffAbility>();

				if (goeddeMovement != null)
				{
					_movementAbility.UpdateAbility(goeddeMovement);
				}
				else if (cohenMovement != null)
				{
					_movementAbility.UpdateAbility(cohenMovement);
				}
				else
				{
					_movementAbility.UpdateAbility(theissBuff);
				}
			}

			if (_rightClickAbility != null)
			{
				var goeddeFlamethrower = _observedAgent.GetComponent<GoeddeFlamethrowerAbility>();
				var cohenRicochet      = _observedAgent.GetComponent<CohenRicochetAbility>();
				var theissShield       = _observedAgent.GetComponent<TheissShieldAbility>();

				if (goeddeFlamethrower != null)
				{
					_rightClickAbility.UpdateAbility(goeddeFlamethrower);
				}
				else if (cohenRicochet != null)
				{
					_rightClickAbility.UpdateAbility(cohenRicochet);
				}
				else if (theissShield != null)
				{
					_rightClickAbility.UpdateAbility(theissShield);
				}
				else
				{
					_rightClickAbility.UpdateSecondaryAction(_observedAgent.Weapons != null ? _observedAgent.Weapons.CurrentWeapon : null);
				}
			}

			if (_ultimateAbility != null)
			{
				var goeddeUltimate = _observedAgent.GetComponent<GoeddeUltimateAbility>();
				var cohenUltimate = _observedAgent.GetComponent<CohenUltimateAbility>();
				var theissUltimate = _observedAgent.GetComponent<TheissUltimateAbility>();

				if (goeddeUltimate != null)
				{
					_ultimateAbility.UpdateAbility(goeddeUltimate);
				}
				else if (cohenUltimate != null)
				{
					_ultimateAbility.UpdateAbility(cohenUltimate);
				}
				else
				{
					_ultimateAbility.UpdateAbility(theissUltimate);
				}
			}

			ShowAliveGroup(_observedAgent.Health.IsAlive);
		}

		// PRIVATE METHODS

		private void EnsureScoreHeaderUI()
		{
			if (_scoreHeader != null)
				return;

			var go = new GameObject("ScoreHeader");
			go.layer = gameObject.layer;
			go.transform.SetParent(transform, false);

			var rect = go.AddComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 1f);
			rect.anchorMax = new Vector2(0.5f, 1f);
			rect.pivot = new Vector2(0.5f, 1f);
			rect.anchoredPosition = new Vector2(0f, -10f);
			rect.sizeDelta = new Vector2(480f, 80f);

			_scoreHeader = go.AddComponent<UIScoreHeader>();
		}

		private void EnsureCharacterBlurbUI()
		{
			if (_characterBlurbText != null)
				return;

			// Create a simple corner text element at runtime to avoid fragile prefab edits.
			var go = new GameObject("CharacterBlurb");
			go.layer = gameObject.layer;
			go.transform.SetParent(transform, false);

			var rect = go.AddComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 0f);
			rect.anchorMax = new Vector2(0f, 0f);
			rect.pivot = new Vector2(0f, 0f);
			rect.anchoredPosition = _characterBlurbOffset;
			rect.sizeDelta = _characterBlurbSize;

			_characterBlurbText = go.AddComponent<TextMeshProUGUI>();
			_characterBlurbText.raycastTarget = false;
			_characterBlurbText.fontSize = _characterBlurbFontSize;
			_characterBlurbText.color = _characterBlurbColor;
			_characterBlurbText.alignment = TextAlignmentOptions.BottomLeft;
			_characterBlurbText.enableWordWrapping = true;
			_characterBlurbText.text = string.Empty;
			go.SetActive(false);
		}

		private void UpdateCharacterBlurb()
		{
			if (_showCharacterBlurb == false || _characterBlurbText == null || _observedAgent == null)
				return;

			var blurb = _observedAgent.GetComponent<Projectiles.CharacterBlurb>();
			var text = blurb != null ? blurb.Blurb : null;

			bool hasText = string.IsNullOrWhiteSpace(text) == false;
			_characterBlurbText.gameObject.SetActive(hasText);
			if (hasText)
			{
				_characterBlurbText.text = text;
			}
		}

		private void EnsureTabHintUI()
		{
			if (_tabHintText != null)
				return;

			var go = new GameObject("TabSwapHint");
			go.layer = gameObject.layer;
			go.transform.SetParent(transform, false);

			var rect = go.AddComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 0f);
			rect.anchorMax = new Vector2(0.5f, 0f);
			rect.pivot = new Vector2(0.5f, 0f);
			rect.anchoredPosition = _tabHintOffset;
			rect.sizeDelta = _tabHintSize;

			_tabHintText = go.AddComponent<TextMeshProUGUI>();
			_tabHintText.raycastTarget = false;
			_tabHintText.fontSize = _tabHintFontSize;
			_tabHintText.color = _tabHintColor;
			_tabHintText.alignment = TextAlignmentOptions.Center;
			_tabHintText.enableWordWrapping = false;
			_tabHintText.text = _tabHintMessage;
			go.SetActive(false);
		}

		private void EnsureRoundCountdownUI()
		{
			if (_roundCountdownText != null)
				return;

			var go = new GameObject("RoundStartCountdown");
			go.layer = gameObject.layer;
			go.transform.SetParent(transform, false);

			var rect = go.AddComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 0f);
			rect.anchorMax = new Vector2(0.5f, 0f);
			rect.pivot = new Vector2(0.5f, 0f);
			rect.anchoredPosition = _roundCountdownOffset;
			rect.sizeDelta = _roundCountdownSize;

			_roundCountdownText = go.AddComponent<TextMeshProUGUI>();
			_roundCountdownText.raycastTarget = false;
			_roundCountdownText.fontSize = _roundCountdownFontSize;
			_roundCountdownText.color = _roundCountdownColor;
			_roundCountdownText.alignment = TextAlignmentOptions.Center;
			_roundCountdownText.enableWordWrapping = false;
			_roundCountdownText.text = string.Empty;
			go.SetActive(false);
		}

	private void UpdateTabHint()
	{
		if (_tabHintText == null)
			return;

		_tabHintText.gameObject.SetActive(DisappearWhenPlayerNotInArea.IsLocalPlayerInside);
	}

	private void UpdateRoundCountdown()
	{
		if (_roundCountdownText == null || _context == null || _context.Gameplay == null)
			return;

		var gameplay = _context.Gameplay;

		// Once the round has started, hide the countdown.
		if (gameplay.RoundStarted)
		{
			_roundCountdownText.gameObject.SetActive(false);
			return;
		}

		// If the server hasn't configured a start tick, don't show anything.
		if (gameplay.RoundStartTick <= 0 || _context.Runner == null || _context.Runner.IsRunning == false)
		{
			_roundCountdownText.gameObject.SetActive(false);
			return;
		}

		int ticksRemaining = gameplay.RoundStartTick - _context.Runner.Tick;

		// If negative but RoundStarted is still false (due to replication delay), just hide.
		if (ticksRemaining <= 0)
		{
			_roundCountdownText.gameObject.SetActive(false);
			return;
		}

		float secondsRemaining = ticksRemaining / (float)_context.Runner.TickRate;

		int wholeSeconds = Mathf.CeilToInt(secondsRemaining);
		int minutes = Mathf.Max(0, wholeSeconds / 60);
		int seconds = Mathf.Max(0, wholeSeconds % 60);

		string label;
		if (minutes > 0)
		{
			label = $"Round starts in {minutes}:{seconds:00}";
		}
		else
		{
			label = $"Round starts in {seconds}...";
		}

		_roundCountdownText.text = label;
		_roundCountdownText.gameObject.SetActive(true);
	}

		private void EnsurePlayerHealthHud()
		{
			if (_playerHpRoot != null)
				return;

			var sp = HealthBarSpriteUtil.WhiteSprite;

			_playerHpRoot = new GameObject("PlayerHealthHUD");
			_playerHpRoot.layer = gameObject.layer;
			_playerHpRoot.transform.SetParent(transform, false);
			_playerHpRootRt = _playerHpRoot.AddComponent<RectTransform>();
			_playerHpRootRt.anchorMin = new Vector2(0.5f, 0f);
			_playerHpRootRt.anchorMax = new Vector2(0.5f, 0f);
			_playerHpRootRt.pivot = new Vector2(0.5f, 0f);
		// Bottom center, slightly above the bottom edge but below the round countdown.
		_playerHpRootRt.anchoredPosition = new Vector2(0f, 20f);
		_playerHpRootRt.sizeDelta = new Vector2(548f, 28f);
		// Put this behind other HUD elements (so it doesn't cover text).
		_playerHpRoot.transform.SetSiblingIndex(0);

			void StretchFull(RectTransform rt, Vector2 min, Vector2 max)
			{
				rt.anchorMin = min;
				rt.anchorMax = max;
				rt.offsetMin = Vector2.zero;
				rt.offsetMax = Vector2.zero;
			}

			// Soft bloom behind bar (hero-style)
			var glowGo = new GameObject("Glow");
			glowGo.transform.SetParent(_playerHpRoot.transform, false);
			var glowRt = glowGo.AddComponent<RectTransform>();
			StretchFull(glowRt, Vector2.zero, Vector2.one);
			glowRt.offsetMin = new Vector2(-18f, -14f);
			glowRt.offsetMax = new Vector2(18f, 14f);
			_playerHpGlow = glowGo.AddComponent<Image>();
			_playerHpGlow.sprite = sp;
			_playerHpGlow.color = new Color(0.25f, 0.95f, 0.82f, 0.03f);
			_playerHpGlow.raycastTarget = false;
			glowGo.transform.SetAsFirstSibling();

			var plate = new GameObject("Plate");
			plate.transform.SetParent(_playerHpRoot.transform, false);
			var plateRt = plate.AddComponent<RectTransform>();
			StretchFull(plateRt, Vector2.zero, Vector2.one);
			plateRt.offsetMin = new Vector2(0f, 4f);
			plateRt.offsetMax = new Vector2(0f, 0f);
			var plateImg = plate.AddComponent<Image>();
			plateImg.sprite = sp;
			// Outer plate kept transparent; the visible border is the shrinking track itself.
			plateImg.color = new Color(0.04f, 0.06f, 0.1f, 0f);
			plateImg.raycastTarget = false;

			var track = new GameObject("Track");
			track.transform.SetParent(plate.transform, false);
			var trackRt = track.AddComponent<RectTransform>();
			// Keep track centered horizontally so the full-width frame lines up cleanly.
			trackRt.anchorMin = new Vector2(0.5f, 0.5f);
			trackRt.anchorMax = new Vector2(0.5f, 0.5f);
			trackRt.pivot = new Vector2(0.5f, 0.5f);
			trackRt.anchoredPosition = Vector2.zero;
			trackRt.sizeDelta = new Vector2(_playerHpBarInnerWidth + 8f, _playerHpBarHeight + 8f);
			var trackImg = track.AddComponent<Image>();
			trackImg.sprite = sp;
			trackImg.color = new Color(0.02f, 0.03f, 0.06f, 1f);
			trackImg.raycastTarget = false;
			_playerHpTrackRt = trackRt;

			var barArea = new GameObject("BarArea");
			barArea.transform.SetParent(track.transform, false);
			var barRt = barArea.AddComponent<RectTransform>();
			barRt.anchorMin = Vector2.zero;
			barRt.anchorMax = Vector2.one;
			barRt.offsetMin = new Vector2(4f, 4f);
			barRt.offsetMax = new Vector2(-4f, -4f);

			var trailGo = new GameObject("DamageTrail");
			trailGo.transform.SetParent(barArea.transform, false);
			var trailRt = trailGo.AddComponent<RectTransform>();
			StretchFull(trailRt, Vector2.zero, Vector2.one);
			_playerHpTrail = trailGo.AddComponent<Image>();
			_playerHpTrail.sprite = sp;
			_playerHpTrail.color = new Color(1f, 0.97f, 0.88f, 0.62f);
			_playerHpTrail.type = Image.Type.Filled;
			_playerHpTrail.fillMethod = Image.FillMethod.Horizontal;
			_playerHpTrail.fillOrigin = (int)Image.OriginHorizontal.Left;
			_playerHpTrail.fillAmount = 1f;
			_playerHpTrail.raycastTarget = false;

			var fillClip = new GameObject("FillClip");
			fillClip.transform.SetParent(barArea.transform, false);
			_playerHpFillClipRt = fillClip.AddComponent<RectTransform>();
			_playerHpFillClipRt.anchorMin = new Vector2(0f, 0f);
			_playerHpFillClipRt.anchorMax = new Vector2(0f, 1f);
			_playerHpFillClipRt.pivot = new Vector2(0f, 0.5f);
			_playerHpFillClipRt.anchoredPosition = Vector2.zero;
			_playerHpFillClipRt.sizeDelta = new Vector2(_playerHpBarInnerWidth, _playerHpBarHeight);
			fillClip.AddComponent<RectMask2D>();

			var fillGo = new GameObject("Fill");
			fillGo.transform.SetParent(fillClip.transform, false);
			var fillRt = fillGo.AddComponent<RectTransform>();
			StretchFull(fillRt, Vector2.zero, Vector2.one);
			_playerHpFill = fillGo.AddComponent<Image>();
			_playerHpFill.sprite = sp;
			_playerHpFill.color = new Color(0.12f, 0.82f, 0.52f, 1f);
			_playerHpFill.raycastTarget = false;

			fillClip.transform.SetAsLastSibling();

			// Position bottom-center HUD texts relative to the health bar,
			// so they never overlap no matter what the serialized offsets were.
			if (_roundCountdownText != null)
			{
				var rt = _roundCountdownText.rectTransform;
				rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, _playerHpRootRt.anchoredPosition.y + _playerHpRootRt.sizeDelta.y + 18f);
			}

			if (_tabHintText != null)
			{
				var rt = _tabHintText.rectTransform;
				rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, _playerHpRootRt.anchoredPosition.y + _playerHpRootRt.sizeDelta.y + 44f);
			}

			_playerHpRoot.SetActive(false);
		}

		private void UpdatePlayerHealthHud()
		{
			if (_playerHpRoot == null || _observedAgent == null)
			{
				if (_playerHpRoot != null)
					_playerHpRoot.SetActive(false);
				return;
			}

			var h = _observedAgent.Health;
			if (h.IsAlive == false)
			{
				_playerHpRoot.SetActive(false);
				return;
			}

			_playerHpRoot.SetActive(true);

			float max = h.MaxHealth;
			float target = max > 0f ? Mathf.Clamp01(h.CurrentHealth / max) : 0f;
			if (_playerHpHudFirstSync)
			{
				_playerHpSmooth = target;
				_playerHpTrailFill = target;
				_playerHpHudFirstSync = false;

				if (_playerHpFillClipRt != null)
					_playerHpFillClipRt.sizeDelta = new Vector2(Mathf.Max(2f, _playerHpBarInnerWidth * _playerHpSmooth), _playerHpBarHeight);
				if (_playerHpTrackRt != null)
					_playerHpTrackRt.sizeDelta = new Vector2(Mathf.Max(2f, (_playerHpBarInnerWidth * _playerHpSmooth) + 8f), _playerHpBarHeight + 8f);
				if (_playerHpTrail != null)
					_playerHpTrail.fillAmount = _playerHpTrailFill;
			}
			else
			{
				_playerHpSmooth = Mathf.MoveTowards(_playerHpSmooth, target, 14f * Time.deltaTime);
				if (_playerHpFillClipRt != null)
					_playerHpFillClipRt.sizeDelta = new Vector2(Mathf.Max(2f, _playerHpBarInnerWidth * _playerHpSmooth), _playerHpBarHeight);
				if (_playerHpTrackRt != null)
					_playerHpTrackRt.sizeDelta = new Vector2(Mathf.Max(2f, (_playerHpBarInnerWidth * _playerHpSmooth) + 8f), _playerHpBarHeight + 8f);
			}

			if (Time.time - _playerHpLastHit > 0.12f)
				_playerHpTrailFill = Mathf.MoveTowards(_playerHpTrailFill, _playerHpSmooth, 5.5f * Time.deltaTime);
			if (_playerHpTrailFill < _playerHpSmooth)
				_playerHpTrailFill = _playerHpSmooth;
			if (_playerHpTrail != null)
				_playerHpTrail.fillAmount = _playerHpTrailFill;

			Color core;
			if (target < 0.32f)
				core = Color.Lerp(new Color(0.95f, 0.18f, 0.22f, 1f), new Color(1f, 0.45f, 0.15f, 1f), target / 0.32f);
			else if (target < 0.55f)
				core = Color.Lerp(new Color(1f, 0.55f, 0.12f, 1f), new Color(0.2f, 0.88f, 0.48f, 1f), (target - 0.32f) / 0.23f);
			else
				core = Color.Lerp(new Color(0.15f, 0.78f, 0.95f, 1f), new Color(0.1f, 0.92f, 0.55f, 1f), (target - 0.55f) / 0.45f);

			_playerHpFill.color = core;

			// Idle glow (subtle); hit-flash tween in OnHitTaken is not overwritten here while active
			if (_playerHpGlow != null && Time.time >= _playerHpGlowFlashUntil)
			{
				float a = 0.055f + Mathf.Sin(Time.time * 1.4f) * 0.018f;
				if (target < 0.35f)
					_playerHpGlow.color = new Color(1f, 0.2f, 0.15f, a * 1.8f);
				else
					_playerHpGlow.color = new Color(0.3f, 0.95f, 0.85f, a);
			}
		}

	private void ClearObservedAgent(bool hideElements)
		{
			if (_observedAgent != null)
			{
				_observedAgent.Health.HitPerformed -= OnHitPerformed;
				_observedAgent.Health.HitTaken -= OnHitTaken;

				_observedAgent = null;
				_observedAgentId = default;
			}

			if (hideElements == true)
			{
				_observedAgentRoot.SetActive(false);
			}
		}

		private void SetObservedAgent(PlayerAgent agent, bool force = false)
		{
			if (agent == _observedAgent && agent.Id == _observedAgentId && force == false)
				return;

			ClearObservedAgent(false);

			// Same object can be reused from cache so storing NB Id is needed to detect
			// that object was despawned and immediately spawned again
			_observedAgentId = agent.Id;
			_observedAgent = agent;

			if (agent != null)
			{
				_playerHpHudFirstSync = true;
				agent.Health.HitPerformed += OnHitPerformed;
				agent.Health.HitTaken += OnHitTaken;
				float max = agent.Health.MaxHealth;
				float t = max > 0f ? Mathf.Clamp01(agent.Health.CurrentHealth / max) : 0f;
				_playerHpSmooth = _playerHpTrailFill = t;

				// Set clip width immediately so the bar starts full (no initial empty frame).
				if (_playerHpFillClipRt != null)
					_playerHpFillClipRt.sizeDelta = new Vector2(Mathf.Max(2f, _playerHpBarInnerWidth * t), _playerHpBarHeight);
				if (_playerHpTrackRt != null)
					_playerHpTrackRt.sizeDelta = new Vector2(Mathf.Max(2f, (_playerHpBarInnerWidth * t) + 8f), _playerHpBarHeight + 8f);
				if (_playerHpTrail != null)
					_playerHpTrail.fillAmount = _playerHpTrailFill;
			}

			_observedAgentRoot.SetActive(true);
		}

		private void OnHitPerformed(HitData hitData)
		{
			_crosshair.HitPerformed(hitData);
			_hitNumbers.HitPerformed(hitData);
		}

		private void OnHitTaken(HitData hitData)
		{
			_screenEffects.OnHitTaken(hitData);

			if (_observedAgent == null || hitData.Target != (IHitTarget)_observedAgent.Health)
				return;
			if (hitData.Action != EHitAction.Damage || hitData.Amount <= 0f)
				return;

			float max = _observedAgent.Health.MaxHealth;
			if (max > 0f)
			{
				float prev = Mathf.Clamp01((_observedAgent.Health.CurrentHealth + hitData.Amount) / max);
				_playerHpTrailFill = Mathf.Max(_playerHpTrailFill, prev);
				_playerHpLastHit = Time.time;
			}

			if (_playerHpRootRt != null)
			{
				_playerHpRootRt.DOKill();
				_playerHpRootRt.localScale = Vector3.one;
				// Free DOTween has no DOPunchScale on Transform — use a quick scale punch instead.
				Sequence hpHit = DOTween.Sequence();
				hpHit.Append(_playerHpRootRt.DOScale(new Vector3(1.045f, 1.1f, 1f), 0.07f).SetEase(Ease.OutQuad));
				hpHit.Append(_playerHpRootRt.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack));
			}

			if (_playerHpGlow != null)
			{
				_playerHpGlowFlashUntil = Time.time + 0.55f;
				_playerHpGlow.DOKill();
				var c = _playerHpGlow.color;
				_playerHpGlow.color = new Color(c.r, c.g, c.b, 0.18f);
				_playerHpGlow.DOFade(0.03f, 0.35f).SetEase(Ease.OutQuad);
			}
		}

		private void ShowAliveGroup(bool value, bool force = false)
		{
			if (value == _aliveGroupVisible && force == false)
				return;

			_aliveGroupVisible = value;

			DOTween.Kill(_aliveGroup);

			if (value == true)
			{
				_aliveGroup.DOFade(1f, _aliveGroupFadeIn);
			}
			else
			{
				_aliveGroup.DOFade(0f, _aliveGroupFadeOut);
			}
		}

	}
}
