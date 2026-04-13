using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Goedde's movement ability: on activation, smoothly glide forward (or in the held movement
	/// direction) up to <see cref="_teleportDistance"/>, stopping short of any wall.
	/// While phased, Goedde is invisible, invulnerable, and cannot move.
	/// Triggered by the E key.
	/// </summary>
	[DefaultExecutionOrder(5)]
	public class GoeddeMovementAbility : AbilityBase
	{
		public override EAbilitySlot Slot => EAbilitySlot.Movement;
		public bool IsPhased => _isPhased;
		public override bool IsActive => _isPhased;
		public override bool IsReady => base.IsReady && _isPhased == false;
		public override bool HasDuration => true;
		public override float DurationRemainingTime => _durationTimer.RemainingTime(Runner).GetValueOrDefault();
		public override float DurationTotal => _duration;

		[Header("Phase Settings")]
		[SerializeField] private float _duration = 2f;
		[Tooltip("Maximum teleport distance (roughly 2 character heights).")]
		[SerializeField] private float _teleportDistance = 4f;
		[Tooltip("How long the smooth glide to the destination takes.")]
		[SerializeField] private float _slideDuration = 2f;

		[Header("Teleport Wall Detection")]
		[SerializeField] private float _capsuleRadius = 0.35f;
		[SerializeField] private float _capsuleHeight = 1.8f;
		[SerializeField] private LayerMask _wallLayers = Physics.DefaultRaycastLayers;

		[Header("References")]
		[SerializeField] private GameObject _visual;
		[SerializeField] private GameObject _phaseEffect;

		[Networked] private NetworkBool _isPhased { get; set; }
		[Networked] private TickTimer _durationTimer { get; set; }
		[Networked] private TickTimer _slideTimer { get; set; }
		[Networked] private Vector3 _slideStartPos { get; set; }
		[Networked] private Vector3 _slideEndPos { get; set; }

		private HitboxRoot _hitboxRoot;

		protected override void Awake()
		{
			base.Awake();
			_hitboxRoot = GetComponent<HitboxRoot>();
		}

		public override void FixedUpdateNetwork()
		{
			if (!ValidateCanAct())
			{
				if (_isPhased && HasStateAuthority)
				{
					ForceDeactivate();
				}
				return;
			}

			if (_isPhased && _durationTimer.Expired(Runner))
			{
				Deactivate();
				return;
			}

			if (_isPhased)
			{
				if (_slideTimer.ExpiredOrNotRunning(Runner) == false)
				{
					float remaining = _slideTimer.RemainingTime(Runner).GetValueOrDefault();
					float t = Mathf.SmoothStep(0f, 1f, 1f - (remaining / _slideDuration));
					_agent.KCC.SetPosition(Vector3.Lerp(_slideStartPos, _slideEndPos, t));
				}

				_agent.MoveSpeedMultiplier = 0f;
				_agent.JumpMultiplier      = 0f;

				if (_hitboxRoot != null)
				{
					_hitboxRoot.HitboxRootActive = false;
				}
			}

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.E))
			{
				TryActivate(input);
			}
		}

		public override void Render()
		{
			if (_visual != null)
			{
				_visual.SetActive(!_isPhased);
			}

			if (_phaseEffect != null)
			{
				_phaseEffect.SetActive(_isPhased);
			}
		}

		private void TryActivate(GameplayInput input)
		{
			if (_isPhased || IsOnCooldown)
				return;

			Activate(input);
		}

		private void Activate(GameplayInput input)
		{
			_isPhased      = true;
			_durationTimer = TickTimer.CreateFromSeconds(Runner, _duration);

			_agent.Health.SetImmortality(_duration + 0.5f);

			if (_hitboxRoot != null)
			{
				_hitboxRoot.HitboxRootActive = false;
			}

			PerformTeleport(input);
		}

		private void PerformTeleport(GameplayInput input)
		{
			Vector3 teleportDir;
			if (input.MoveDirection.sqrMagnitude > 0.01f)
			{
				var worldMove = _agent.KCC.TransformRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);
				teleportDir = new Vector3(worldMove.x, 0f, worldMove.z).normalized;
			}
			else
			{
				var forward = _agent.KCC.TransformRotation * Vector3.forward;
				teleportDir = new Vector3(forward.x, 0f, forward.z).normalized;
			}

			float halfHeight   = Mathf.Max(0f, _capsuleHeight * 0.5f - _capsuleRadius);
			Vector3 origin     = transform.position;
			float sweepOffset  = _capsuleRadius + 0.02f;
			Vector3 sweepStart = origin + teleportDir * sweepOffset;
			Vector3 capBottom  = sweepStart + Vector3.up * _capsuleRadius;
			Vector3 capTop     = sweepStart + Vector3.up * (_capsuleRadius + halfHeight * 2f);
			float sweepMaxDist = Mathf.Max(0f, _teleportDistance - sweepOffset);

			float travelDistance = _teleportDistance;
			if (sweepMaxDist > 0f && Physics.CapsuleCast(capBottom, capTop, _capsuleRadius, teleportDir, out RaycastHit hit, sweepMaxDist, _wallLayers, QueryTriggerInteraction.Ignore))
			{
				travelDistance = Mathf.Max(0f, sweepOffset + hit.distance - 0.05f);
			}

			_slideStartPos = origin;
			_slideEndPos   = origin + teleportDir * travelDistance;
			_slideTimer    = TickTimer.CreateFromSeconds(Runner, _slideDuration);
		}

		private void Deactivate()
		{
			_isPhased = false;
			StartCooldown();

			_agent.Health.StopImmortality();
			_agent.MoveSpeedMultiplier = 1f;
			_agent.JumpMultiplier      = 1f;

			if (_hitboxRoot != null)
			{
				_hitboxRoot.HitboxRootActive = true;
			}
		}

		private void ForceDeactivate()
		{
			_isPhased      = false;
			_durationTimer = default;
			_cooldownTimer = default;
			_agent.MoveSpeedMultiplier = 1f;
			_agent.JumpMultiplier      = 1f;
		}
	}
}
