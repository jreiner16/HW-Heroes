using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Projectiles.UI
{
	public class UIScreenEffects : UIBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private CanvasGroup _hitGroup;
		[SerializeField]
		private CanvasGroup _debuffGroup;
		[SerializeField]
		private UIBehaviour _deathGroup;

		[Header("Animation")]
		[SerializeField]
		private float _hitFadeInDuration = 0.1f;
		[SerializeField]
		private float _hitFadeOutDuration = 0.7f;

		[Header("Debuff Overlay")]
		[SerializeField]
		private float _debuffTargetAlpha = 0.22f;
		[SerializeField]
		private float _debuffFadeSpeed = 4f;

		[Header("Goedde Phase Overlay")]
		[SerializeField]
		private Color _phaseOverlayColor = new Color(0.42f, 0f, 0.72f, 0.38f);
		[SerializeField]
		private float _phaseFadeSpeed = 5f;

		[Header("Camera Shake on Hit")]
		[SerializeField]
		private ShakeSetup _hitShakePosition = new ShakeSetup
		{
			Duration = 0.25f, Magnitude = 0.04f, Frequency = 18f,
			FadeIn = 0.02f, FadeOut = 0.15f, Axis = new Vector3(1f, 1f, 0f),
			Target = EShakeTarget.Position
		};
		[SerializeField]
		private ShakeSetup _hitShakeRotation = new ShakeSetup
		{
			Duration = 0.2f, Magnitude = 1.5f, Frequency = 14f,
			FadeIn = 0.02f, FadeOut = 0.12f, Axis = new Vector3(1f, 0.6f, 0.3f),
			Target = EShakeTarget.Rotation
		};

		[Header("Audio")]
		[SerializeField]
		private AudioSetup _hitSound;
		[SerializeField]
		private AudioSetup _deathSound;

		[Header("Respawn Countdown")]
		[SerializeField]
		private float _respawnDuration = 3f;

		private CanvasGroup _phaseGroup;
		private PlayerAgent _lastEffectsAgent;
		private IAbility _cachedMovementAbility;

		private TextMeshProUGUI _respawnText;
		private CanvasGroup _respawnGroup;
		private float _deathTime;
		private bool _isDead;

		// PUBLIC METHODS

		public void OnHitTaken(HitData hit)
		{
			if (hit.Amount <= 0)
				return;

			if (hit.Action == EHitAction.Damage)
			{
				float alpha = Mathf.Lerp(0, 1f, hit.Amount / 20f);

				ShowHit(_hitGroup, alpha);
				GameUI.PlaySound(_hitSound, EForceBehaviour.ForceAny);

				PlayHitShake(hit.Amount);

				if (hit.IsFatal == true)
				{
					_deathGroup.SetActive(true);
					_isDead = true;
					_deathTime = Time.time;
					GameUI.PlaySound(_deathSound, EForceBehaviour.ForceAny);
				}
			}
		}

		public void UpdateEffects(PlayerAgent agent)
		{
			if (_debuffGroup != null)
			{
				bool inDebuffField = TheissDamageDebuffField.IsPlayerInsideField(agent.Runner, agent);
				float targetDebuffAlpha = inDebuffField ? _debuffTargetAlpha : 0f;
				_debuffGroup.alpha = Mathf.MoveTowards(_debuffGroup.alpha, targetDebuffAlpha, _debuffFadeSpeed * Time.deltaTime);
			}

			bool dead = agent.Health.IsAlive == false;
			_deathGroup.SetActive(dead);

			// Respawn countdown
			if (dead && _isDead)
			{
				UpdateRespawnCountdown();
			}
			else if (_isDead && !dead)
			{
				_isDead = false;
				if (_respawnGroup != null)
					_respawnGroup.alpha = 0f;
			}

			if (_phaseGroup != null)
			{
				// Refresh cached movement ability when the observed agent changes.
				if (agent != _lastEffectsAgent)
				{
					_lastEffectsAgent = agent;
					_cachedMovementAbility = null;
					if (agent != null)
					{
						var abilities = agent.GetComponents<IAbility>();
						foreach (var a in abilities)
						{
							if (a.Slot == EAbilitySlot.Movement) { _cachedMovementAbility = a; break; }
						}
					}
				}

				float targetAlpha = _cachedMovementAbility != null && _cachedMovementAbility.IsActive ? 1f : 0f;
				_phaseGroup.alpha = Mathf.MoveTowards(_phaseGroup.alpha, targetAlpha, _phaseFadeSpeed * Time.deltaTime);
			}
		}

		// MONOBEHAVIOUR

		protected void OnEnable()
		{
			_hitGroup.SetActive(true);
			_hitGroup.alpha = 0f;

			if (_debuffGroup != null)
			{
				_debuffGroup.SetActive(true);
				_debuffGroup.alpha = 0f;
			}

			_deathGroup.SetActive(false);

			EnsurePhaseOverlay();
			if (_phaseGroup != null)
			{
				_phaseGroup.alpha = 0f;
			}
		}

		// PRIVATE METHODS

		private void EnsurePhaseOverlay()
		{
			if (_phaseGroup != null)
				return;

			var go = new GameObject("GoeddePhaseOverlay");
			go.layer = gameObject.layer;
			go.transform.SetParent(transform, false);

			var rect = go.AddComponent<RectTransform>();
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;

			var img = go.AddComponent<Image>();
			img.color = _phaseOverlayColor;
			img.raycastTarget = false;

			_phaseGroup = go.AddComponent<CanvasGroup>();
			_phaseGroup.alpha = 0f;
			_phaseGroup.blocksRaycasts = false;
			_phaseGroup.interactable = false;

			// Render behind the hit flash and death overlays.
			go.transform.SetAsFirstSibling();
		}

		private void EnsureRespawnOverlay()
		{
			if (_respawnGroup != null)
				return;

			var go = new GameObject("RespawnOverlay");
			go.layer = gameObject.layer;
			go.transform.SetParent(transform, false);

			var rect = go.AddComponent<RectTransform>();
			rect.anchorMin = new Vector2(0.5f, 0.5f);
			rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.anchoredPosition = new Vector2(0f, -40f);
			rect.sizeDelta = new Vector2(400f, 120f);

			_respawnGroup = go.AddComponent<CanvasGroup>();
			_respawnGroup.alpha = 0f;
			_respawnGroup.blocksRaycasts = false;
			_respawnGroup.interactable = false;

			_respawnText = go.AddComponent<TextMeshProUGUI>();
			_respawnText.fontSize = 48f;
			_respawnText.fontStyle = FontStyles.Bold;
			_respawnText.alignment = TextAlignmentOptions.Center;
			_respawnText.color = new Color(0.9f, 0.9f, 1f, 1f);
			_respawnText.raycastTarget = false;
		}

		private void UpdateRespawnCountdown()
		{
			EnsureRespawnOverlay();
			if (_respawnGroup == null)
				return;

			float elapsed = Time.time - _deathTime;
			float remaining = Mathf.Max(0f, _respawnDuration - elapsed);

			_respawnGroup.alpha = 1f;
			_respawnText.text = remaining > 0.1f
				? $"Respawning in {Mathf.CeilToInt(remaining)}"
				: "Respawning...";
		}

		private void PlayHitShake(float damageAmount)
		{
			var camera = GameUI.Context?.Camera;
			if (camera == null)
				return;

			var shake = camera.ShakeEffect;
			if (shake == null)
				return;

			float intensity = Mathf.Clamp01(damageAmount / 40f);

			if (_hitShakePosition != null)
			{
				_hitShakePosition.Magnitude = Mathf.Lerp(0.02f, 0.08f, intensity);
				shake.Play(_hitShakePosition, EShakeForce.ReplaceSame);
			}

			if (_hitShakeRotation != null)
			{
				_hitShakeRotation.Magnitude = Mathf.Lerp(0.5f, 3f, intensity);
				shake.Play(_hitShakeRotation, EShakeForce.ReplaceSame);
			}
		}

		private void ShowHit(CanvasGroup group, float targetAlpha)
		{
			DOTween.Kill(group);

			group.DOFade(targetAlpha, _hitFadeInDuration);
			group.DOFade(0f, _hitFadeOutDuration).SetDelay(_hitFadeInDuration);
		}
	}
}
