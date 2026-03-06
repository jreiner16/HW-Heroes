using DG.Tweening;
using Fusion;
using UnityEngine;
using TMPro;

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
		[SerializeField] private Vector2 _tabHintOffset = new Vector2(0f, 80f);
		[SerializeField] private Vector2 _tabHintSize = new Vector2(600f, 50f);
		[SerializeField] private float _tabHintFontSize = 20f;
		[SerializeField] private Color _tabHintColor = new Color(1f, 1f, 0.6f, 0.9f);
		private TextMeshProUGUI _tabHintText;

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
				return;

		_health.UpdateHealth(_observedAgent.Health);
		_screenEffects.UpdateEffects(_observedAgent);

		UpdateCharacterBlurb();
		UpdateTabHint();

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

	private void UpdateTabHint()
	{
		if (_tabHintText == null)
			return;

		_tabHintText.gameObject.SetActive(DisappearWhenPlayerNotInArea.IsLocalPlayerInside);
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
				agent.Health.HitPerformed += OnHitPerformed;
				agent.Health.HitTaken += OnHitTaken;
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
