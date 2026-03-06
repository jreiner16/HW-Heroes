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
	public class GoeddeMovementAbility : ContextBehaviour
	{
		// PUBLIC MEMBERS

		public bool  IsPhased     => _isPhased;
		public bool  IsOnCooldown => _cooldownTimer.ExpiredOrNotRunning(Runner) == false;
		public bool  IsReady      => _cooldownTimer.ExpiredOrNotRunning(Runner) && _isPhased == false;

		public float CooldownRemainingTime => _cooldownTimer.RemainingTime(Runner).GetValueOrDefault();
		public float CooldownTotal         => _cooldown;
		public float DurationRemainingTime => _durationTimer.RemainingTime(Runner).GetValueOrDefault();
		public float DurationTotal         => _duration;

		// PRIVATE MEMBERS

		[Header("Ability Settings")]
		[SerializeField]
		private float _duration = 2f;
		[SerializeField]
		private float _cooldown = 10f;
		[Tooltip("Maximum teleport distance (roughly 2 character heights).")]
		[SerializeField]
		private float _teleportDistance = 4f;
		[Tooltip("How long the smooth glide to the destination takes (must be less than or equal to duration).")]
		[SerializeField]
		private float _slideDuration = 2f;

		[Header("Teleport Wall Detection")]
		[Tooltip("Radius of the capsule used to check for walls during teleport. Should match the character's capsule collider radius.")]
		[SerializeField]
		private float _capsuleRadius = 0.35f;
		[Tooltip("Height of the capsule used to check for walls during teleport. Should match the character's capsule collider height.")]
		[SerializeField]
		private float _capsuleHeight = 1.8f;
		[Tooltip("Layers treated as solid walls for teleport wall detection.")]
		[SerializeField]
		private LayerMask _wallLayers = Physics.DefaultRaycastLayers;

		[Header("References")]
		[SerializeField]
		private GameObject _visual;
		[SerializeField]
		private GameObject _phaseEffect;

		[Networked]
		private NetworkBool _isPhased { get; set; }
		[Networked]
		private TickTimer _durationTimer { get; set; }
		[Networked]
		private TickTimer _cooldownTimer { get; set; }
		[Networked]
		private TickTimer _slideTimer { get; set; }
		[Networked]
		private Vector3 _slideStartPos { get; set; }
		[Networked]
		private Vector3 _slideEndPos { get; set; }

		private PlayerAgent _agent;
		private HitboxRoot  _hitboxRoot;

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_agent     = GetComponent<PlayerAgent>();
			_hitboxRoot = GetComponent<HitboxRoot>();
		}

		// NetworkBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			if (_agent.Owner == null || _agent.Health.IsAlive == false)
			{
				if (_isPhased)
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
				// Smooth glide: drive position toward the destination while the slide timer runs.
				// KCC.SetPosition here overrides whatever PlayerAgent computed this tick.
				if (_slideTimer.ExpiredOrNotRunning(Runner) == false)
				{
					float remaining = _slideTimer.RemainingTime(Runner).GetValueOrDefault();
					float t = Mathf.SmoothStep(0f, 1f, 1f - (remaining / _slideDuration));
					_agent.KCC.SetPosition(Vector3.Lerp(_slideStartPos, _slideEndPos, t));
				}

				// Prevent all movement while phased. This runs after PlayerAgent (execution order
				// 5 vs -5) so it will take effect on the following tick — imperceptible over the
				// ability's duration.
				_agent.MoveSpeedMultiplier = 0f;
				_agent.JumpMultiplier      = 0f;

				// Runs after PlayerBody (execution order 5 vs default 0) so this wins.
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

		// PRIVATE METHODS

		private void TryActivate(GameplayInput input)
		{
			if (_isPhased || IsOnCooldown)
				return;

			Activate(input);
		}

		private void Activate(GameplayInput input)
		{
			_isPhased     = true;
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
			// Determine teleport direction: movement input if held, otherwise straight forward.
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

		// Start the sweep just past the player's own capsule surface so the cast never
		// begins inside the player's own collider. This lets us cast against ALL wall
		// layers without needing to know which layer the player is on, avoiding the bug
		// where player and walls share the same layer (e.g. both on Default).
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
			// Offset back by sweepOffset so travelDistance is relative to the real origin.
			travelDistance = Mathf.Max(0f, sweepOffset + hit.distance - 0.05f);
		}

			// Store start/end for the smooth glide and start the slide timer.
			_slideStartPos = origin;
			_slideEndPos   = origin + teleportDir * travelDistance;
			_slideTimer    = TickTimer.CreateFromSeconds(Runner, _slideDuration);
		}

		private void Deactivate()
		{
			_isPhased     = false;
			_cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);

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
			_isPhased     = false;
			_durationTimer = default;
			_cooldownTimer = default;
			_agent.MoveSpeedMultiplier = 1f;
			_agent.JumpMultiplier      = 1f;
		}
	}
}
