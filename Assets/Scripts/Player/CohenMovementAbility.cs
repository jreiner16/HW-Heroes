using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Cohen's movement ability: shrink for a short duration.
	/// Triggered by the E key.
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Cohen Movement Ability")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(5)]
	public class CohenMovementAbility : AbilityBase
	{
		public override EAbilitySlot Slot => EAbilitySlot.Movement;
		public bool IsShrunk => _isShrunk;
		public override bool IsActive => _isShrunk;
		public override bool IsReady => base.IsReady && _isShrunk == false;
		public override bool HasDuration => true;
		public override float DurationRemainingTime => _durationTimer.RemainingTime(Runner).GetValueOrDefault();
		public override float DurationTotal => _duration;

		[Header("Shrink Settings")]
		[SerializeField] private float _duration = 3.0f;
		[SerializeField, Range(0.2f, 1f)] private float _shrinkScale = 0.55f;

		[Header("References")]
		[SerializeField] private Transform _visualRoot;

		[Networked] private NetworkBool _isShrunk { get; set; }
		[Networked] private TickTimer _durationTimer { get; set; }

		private Vector3 _visualOriginalScale = Vector3.one;
		private bool _visualOriginalScaleCached;

		protected override void Awake()
		{
			base.Awake();

			if (_visualRoot != null)
			{
				_visualOriginalScale = _visualRoot.localScale;
				_visualOriginalScaleCached = true;
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (!ValidateCanAct())
			{
				if (_isShrunk && HasStateAuthority)
				{
					ForceDeactivate();
				}
				return;
			}

			if (_isShrunk && _durationTimer.Expired(Runner))
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
			if (_visualRoot == null)
				return;

			if (_visualOriginalScaleCached == false)
			{
				_visualOriginalScale = _visualRoot.localScale;
				_visualOriginalScaleCached = true;
			}

			_visualRoot.localScale = _isShrunk ? _visualOriginalScale * _shrinkScale : _visualOriginalScale;
		}

		private void TryActivate()
		{
			if (_isShrunk || IsOnCooldown || HasStateAuthority == false)
				return;

			_isShrunk = true;
			_durationTimer = TickTimer.CreateFromSeconds(Runner, _duration);
		}

		private void Deactivate()
		{
			if (HasStateAuthority == false)
				return;

			_isShrunk = false;
			StartCooldown();
		}

		private void ForceDeactivate()
		{
			_isShrunk = false;
			_durationTimer = default;
			_cooldownTimer = default;
		}
	}
}
