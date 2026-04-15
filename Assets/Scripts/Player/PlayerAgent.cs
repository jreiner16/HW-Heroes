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
		/// When set, overrides the first-person camera position/rotation each LateUpdate.
		/// Set by abilities (e.g. Cohen burrow) that need a custom camera angle.
		/// </summary>
		public Transform CameraOverride { get; set; }

		/// <summary>
		/// When true, vertical mouse input is ignored and pitch is locked to 0.
		/// Set by abilities (e.g. Cohen burrow) that need to prevent the camera looking up/down.
		/// </summary>
		public bool BlockPitchInput { get; set; }

		/// <summary>
		/// Multipliers applied to movement (used by abilities). Default 1. Abilities with execution order before this should set these.
		/// </summary>
		public float MoveSpeedMultiplier { get; set; } = 1f;
		public float JumpMultiplier { get; set; } = 1f;

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

			if (BlockPitchInput)
			{
				var look = KCC.GetLookRotation();
				KCC.SetLookRotation(new Vector2(0f, look.y));
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
				var lookToApply = _lastFUNLookRotation + Input.AccumulatedLook;
				if (BlockPitchInput)
					lookToApply.x = 0f;
				KCC.SetLookRotation(lookToApply, -_maxCameraAngle, _maxCameraAngle);
			}

			// Update camera pitch
			// Camera pivot influences also weapon rotation so it needs to be set on proxies as well
			var pitchRotation = KCC.GetLookRotation(true, false);
			_cameraPivot.localRotation = Quaternion.Euler(pitchRotation);

			// Only the local player's agent controls the camera - never let remote agents touch it
			if (HasInputAuthority == true && Owner != null && Health.IsAlive == true && Context?.Camera != null)
			{
				var cameraTransform = Context.Camera.transform;
				if (CameraOverride != null)
				{
					cameraTransform.position = CameraOverride.position;
					cameraTransform.rotation = CameraOverride.rotation;
				}
				else
				{
					cameraTransform.position = _cameraHandle.position;
					cameraTransform.rotation = KCC.TransformRotation * Quaternion.Euler(pitchRotation.x, 0f, 0f);
				}
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

			// It feels better when player falls quicker
			KCC.SetGravity(KCC.RealVelocity.y >= 0f ? _upGravity : _downGravity);

			// Calculate input direction based on recently updated look rotation (the change propagates internally also to KCC.TransformRotation)
			var inputDirection = KCC.TransformRotation * new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y);

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

			float jumpImpulse = input.Buttons.WasPressed(Input.PreviousButtons, EInputButton.Jump) && KCC.IsGrounded && !BlockPitchInput ? _jumpImpulse * JumpMultiplier : 0f;
			jumpImpulse += _pendingBounceImpulse;
			_pendingBounceImpulse = 0f;
			KCC.Move(_moveVelocity, jumpImpulse);
		}
	}
}
