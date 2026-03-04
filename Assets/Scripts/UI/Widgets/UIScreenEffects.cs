using DG.Tweening;
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

		[Header("Audio")]
		[SerializeField]
		private AudioSetup _hitSound;
		[SerializeField]
		private AudioSetup _deathSound;

		private CanvasGroup _phaseGroup;
		private PlayerAgent _lastEffectsAgent;
		private GoeddeMovementAbility _cachedGoedde;

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

				if (hit.IsFatal == true)
				{
					_deathGroup.SetActive(true);
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

			_deathGroup.SetActive(agent.Health.IsAlive == false);

			if (_phaseGroup != null)
			{
				// Refresh the cached GoeddeMovementAbility when the observed agent changes.
				if (agent != _lastEffectsAgent)
				{
					_lastEffectsAgent = agent;
					_cachedGoedde = agent != null ? agent.GetComponent<GoeddeMovementAbility>() : null;
				}

				float targetAlpha = _cachedGoedde != null && _cachedGoedde.IsPhased ? 1f : 0f;
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

		private void ShowHit(CanvasGroup group, float targetAlpha)
		{
			DOTween.Kill(group);

			group.DOFade(targetAlpha, _hitFadeInDuration);
			group.DOFade(0f, _hitFadeOutDuration).SetDelay(_hitFadeInDuration);
		}
	}
}
