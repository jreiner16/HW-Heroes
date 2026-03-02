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
	public class CohenMovementAbility : ContextBehaviour
	{
		public bool  IsShrunk    => _isShrunk;
		public bool  IsOnCooldown => _cooldownTimer.ExpiredOrNotRunning(Runner) == false;
		public bool  IsReady      => _cooldownTimer.ExpiredOrNotRunning(Runner) && _isShrunk == false;

		public float CooldownRemainingTime => _cooldownTimer.RemainingTime(Runner).GetValueOrDefault();
		public float CooldownTotal         => _cooldown;
		public float DurationRemainingTime => _durationTimer.RemainingTime(Runner).GetValueOrDefault();
		public float DurationTotal         => _duration;

		[Header("Ability Settings")]
		[SerializeField] private float _duration = 3.0f;
		[SerializeField] private float _cooldown = 10.0f;
		[SerializeField, Range(0.2f, 1f)] private float _shrinkScale = 0.55f;

		[Header("References")]
		[SerializeField] private Transform _visualRoot;

		[Networked] private NetworkBool _isShrunk { get; set; }
		[Networked] private TickTimer _durationTimer { get; set; }
		[Networked] private TickTimer _cooldownTimer { get; set; }

		private PlayerAgent _agent;
		private Vector3 _visualOriginalScale = Vector3.one;
		private bool _visualOriginalScaleCached;

		protected void Awake()
		{
			_agent = GetComponent<PlayerAgent>();

			if (_visualRoot != null)
			{
				_visualOriginalScale = _visualRoot.localScale;
				_visualOriginalScaleCached = true;
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (_agent == null || _agent.Owner == null || _agent.Health.IsAlive == false)
			{
				if (_isShrunk == true)
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
			if (_isShrunk || IsOnCooldown)
				return;

			if (HasStateAuthority == false)
				return;

			_isShrunk = true;
			_durationTimer = TickTimer.CreateFromSeconds(Runner, _duration);
		}

		private void Deactivate()
		{
			if (HasStateAuthority == false)
				return;

			_isShrunk = false;
			_cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);
		}

		private void ForceDeactivate()
		{
			if (HasStateAuthority == false)
				return;

			_isShrunk = false;
			_durationTimer = default;
			_cooldownTimer = default;
		}
	}
}

