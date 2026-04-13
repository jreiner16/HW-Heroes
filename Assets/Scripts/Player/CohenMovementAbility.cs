using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Cohen's movement ability: burrow underground for a short duration.
	/// While burrowed, Cohen is invulnerable and his hitbox is disabled.
	/// Triggered by the E key.
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Cohen Movement Ability")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(5)]
	public class CohenMovementAbility : AbilityBase
	{
		public override EAbilitySlot Slot => EAbilitySlot.Movement;
		public bool IsBurrowed => _isBurrowed;
		public override bool IsActive => _isBurrowed;
		public override bool IsReady => base.IsReady && _isBurrowed == false;
		public override bool HasDuration => true;
		public override float DurationRemainingTime => _durationTimer.RemainingTime(Runner).GetValueOrDefault();
		public override float DurationTotal => _duration;

		[Header("Burrow Settings")]
		[SerializeField] private float _duration = 3.0f;

		[Header("References")]
		[SerializeField] private Transform _burrowCameraAnchor;
		[SerializeField] private GameObject _groundIndicator;

		[Networked] private NetworkBool _isBurrowed { get; set; }
		[Networked] private TickTimer _durationTimer { get; set; }

		private HitboxRoot _hitboxRoot;
		private PlayerBody _playerBody;

		protected override void Awake()
		{
			base.Awake();
			_hitboxRoot = GetComponent<HitboxRoot>();
			_playerBody = GetComponent<PlayerBody>();
		}

		public override void FixedUpdateNetwork()
		{
			if (!ValidateCanAct())
			{
				if (_isBurrowed && HasStateAuthority)
				{
					ForceDeactivate();
				}
				return;
			}

			if (_isBurrowed && _durationTimer.Expired(Runner))
			{
				Deactivate();
				return;
			}

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.E))
			{
				TryActivate();
			}
		}

		public override void Render()
		{
			_playerBody?.SetVisible(!_isBurrowed);

			if (_groundIndicator != null)
			{
				_groundIndicator.SetActive(_isBurrowed);
			}

			// Camera override is local-player only; set every render frame so it stays in sync with networked state.
			if (HasInputAuthority)
			{
				_agent.CameraOverride = _isBurrowed ? _burrowCameraAnchor : null;
			}
		}

		private void TryActivate()
		{
			if (_isBurrowed || IsOnCooldown || HasStateAuthority == false)
				return;

			_isBurrowed = true;
			_durationTimer = TickTimer.CreateFromSeconds(Runner, _duration);

			_agent.Health.SetImmortality(_duration + 0.5f);

			if (_hitboxRoot != null)
			{
				_hitboxRoot.HitboxRootActive = false;
			}
		}

		private void Deactivate()
		{
			if (HasStateAuthority == false)
				return;

			_isBurrowed = false;
			StartCooldown();

			_agent.Health.StopImmortality();

			if (_hitboxRoot != null)
			{
				_hitboxRoot.HitboxRootActive = true;
			}
		}

		private void ForceDeactivate()
		{
			_isBurrowed = false;
			_durationTimer = default;
			_cooldownTimer = default;

			if (_hitboxRoot != null)
			{
				_hitboxRoot.HitboxRootActive = true;
			}
		}
	}
}
