using Fusion;
using UnityEngine;
using Fusion.Addons.SimpleKCC;

namespace Projectiles
{
	public enum CharacterClass
	{
		Tank,
		DPS,
		Support,
	}

	/// <summary>
	/// Main script handling player agent. It provides access to common components and handles movement input processing and camera.
	/// </summary>
	[DefaultExecutionOrder(-5)]
	[RequireComponent(typeof(Weapons), typeof(Health), typeof(SimpleKCC))]
	public class PlayerAgent : ContextBehaviour
	{
		// PUBLIC MEMBERS

		[Networked]
		public Player      Owner         { get; set; }
		public Weapons     Weapons       { get; private set; }
		public Health      Health        { get; private set; }
		public SimpleKCC   KCC           { get; private set; }
		public PlayerInput Input         { get; private set; }

		public bool        InputBlocked  => Health.IsAlive == false;

		public CharacterClass Class => _characterClass;

		/// <summary>
		/// Multipliers applied to movement (used by abilities). Default 1. Abilities with execution order before this should set these.
		/// </summary>
		public float MoveSpeedMultiplier { get; set; } = 1f;
		public float JumpMultiplier      { get; set; } = 1f;

		/// <summary>Extra upward impulse added this tick (cleared after use). Set by abilities to trigger a jump.</summary>
		public float ExtraJumpImpulse { get; set; } = 0f;

		/// <summary>When set, overrides the gravity applied this tick. Set by abilities for float/slam phases.</summary>
		public float? GravityOverride { get; set; }

		/// <summary>When true, horizontal move input is ignored this tick (e.g. during jump smash flight).</summary>
		public bool LockMovement { get; set; }

		// PRIVATE MEMBERS

		[Header("Character")]
		[SerializeField]
		private CharacterClass _characterClass;

		[SerializeField]
		private Transform _cameraPivot;
		[SerializeField]
		private Transform _cameraHandle;

		[Header("Movement")]
	[SerializeField]
	private float _moveSpeed = 6f;
		[SerializeField]
		public float _upGravity = 15f;
		[SerializeField]
		public float _downGravity = 25f;
		[SerializeField]
		private float _maxCameraAngle = 75f;
		[SerializeField]
		private float _jumpImpulse = 6f;
		[SerializeField]
		public float _groundAcceleration = 55f;
		[SerializeField]
		public float _groundDeceleration = 25f;
		[SerializeField]
		public float _airAcceleration = 25f;
		[SerializeField]
		public float _airDeceleration = 1.3f;

		[Networked]
		private Vector3 _moveVelocity { get; set; }
		[Networked]
		private float _pendingBounceImpulse { get; set; }
		[Networked]
		private Vector3 _pendingKnockback { get; set; }

		private Vector2 _lastFUNLookRotation;

		/// <summary>
		/// Applies an upward impulse to the player (e.g. from bouncers). Call from OnTriggerEnter/OnCollisionEnter.
		/// </summary>
		public void AddBounceImpulse(float impulse)
		{
			if (HasStateAuthority == false)
				return;
			_pendingBounceImpulse += impulse;
		}

		/// <summary>
		/// Applies a horizontal knockback impulse (e.g. from AoE smash). State authority only.
		/// </summary>
		public void AddKnockbackImpulse(Vector3 direction, float force)
		{
			if (HasStateAuthority == false)
				return;
			_pendingKnockback += direction * force;
		}

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			name = Object.InputAuthority.ToString();

			// Only local player needs networked properties (move velocity).
			// This saves network traffic by not synchronizing networked properties to other clients except local player.
			ReplicateToAll(false);
			ReplicateTo(Object.InputAuthority, true);

		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			Owner = null;
		}

		public override void FixedUpdateNetwork()
		{
			if (Owner != null && Health.IsAlive == true)
			{
				ProcessMovementInput();
			}

			// Setting camera pivot rotation
			var pitchRotation = KCC.GetLookRotation(true, false);
			_cameraPivot.localRotation = Quaternion.Euler(pitchRotation);

			_lastFUNLookRotation = KCC.GetLookRotation();
		}

		// MONOBEHAVIOUR

		protected void Awake()
		{
			KCC = GetComponent<SimpleKCC>();
			Weapons = GetComponent<Weapons>();
			Health = GetComponent<Health>();
			Input = GetComponent<PlayerInput>();
		}

		protected void LateUpdate()
		{
			if (HasInputAuthority == true && Owner != null && Health.IsAlive == true)
			{
				// For responsive look experience we use last FUN look + accumulated look rotation delta
				KCC.SetLookRotation(_lastFUNLookRotation + Input.AccumulatedLook, -_maxCameraAngle, _maxCameraAngle);
			}

			// Update camera pitch
			// Camera pivot influences also weapon rotation so it needs to be set on proxies as well
			var pitchRotation = KCC.GetLookRotation(true, false);
			_cameraPivot.localRotation = Quaternion.Euler(pitchRotation);

			// Only the local player's agent controls the camera - never let remote agents touch it
			if (HasInputAuthority == true && Owner != null && Health.IsAlive == true && Context?.Camera != null)
			{
				var cameraTransform = Context.Camera.transform;
				cameraTransform.position = _cameraHandle.position;
				cameraTransform.rotation = KCC.TransformRotation * Quaternion.Euler(pitchRotation.x, 0f, 0f);
			}
	}

	// PRIVATE METHODS

		private void ProcessMovementInput()
		{
			if (GetInput(out GameplayInput input) == false)
			{
				if (_pendingBounceImpulse > 0f)
				{
					KCC.Move(_moveVelocity, _pendingBounceImpulse);
					_pendingBounceImpulse = 0f;
				}
				return;
			}

			KCC.AddLookRotation(input.LookRotationDelta, -_maxCameraAngle, _maxCameraAngle);

			// Gravity — abilities can override for float/slam phases
			if (GravityOverride.HasValue)
				KCC.SetGravity(GravityOverride.Value);
			else
				KCC.SetGravity(KCC.RealVelocity.y >= 0f ? _upGravity : _downGravity);

			// Calculate input direction based on recently updated look rotation (the change propagates internally also to KCC.TransformRotation)
			var inputDirection = LockMovement
				? Vector3.zero
				: KCC.TransformRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);

			float moveSpeed = _moveSpeed * MoveSpeedMultiplier;
			var desiredMoveVelocity = inputDirection * moveSpeed;
			float acceleration = 1f;

			if (desiredMoveVelocity == Vector3.zero)
			{
				// No desired move velocity - we are stopping.
				acceleration = KCC.IsGrounded == true ? _groundDeceleration : _airDeceleration;
			}
			else
			{
				acceleration = KCC.IsGrounded == true ? _groundAcceleration : _airAcceleration;
			}

			_moveVelocity = Vector3.Lerp(_moveVelocity, desiredMoveVelocity, acceleration * Runner.DeltaTime);

			// Apply pending horizontal knockback (from AoE abilities) directly into move velocity
			if (_pendingKnockback.sqrMagnitude > 0.001f)
			{
				_moveVelocity += _pendingKnockback;
				_pendingKnockback = Vector3.zero;
			}

			float jumpImpulse = input.Buttons.WasPressed(Input.PreviousButtons, EInputButton.Jump) && KCC.IsGrounded ? _jumpImpulse * JumpMultiplier : 0f;
			jumpImpulse += _pendingBounceImpulse + ExtraJumpImpulse;
			_pendingBounceImpulse = 0f;
			ExtraJumpImpulse      = 0f;

			KCC.Move(_moveVelocity, jumpImpulse);

			// Reset per-tick ability overrides after movement is processed
			GravityOverride = null;
			LockMovement    = false;
		}
	}
}
