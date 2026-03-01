using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Theiss right-click ability. Spawns a shield wall that blocks enemy fire.
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Theiss Shield Ability")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(5)]
	public class TheissShieldAbility : ContextBehaviour
	{
		// PUBLIC MEMBERS

		public bool IsOnCooldown => _cooldownTimer.ExpiredOrNotRunning(Runner) == false;
		public bool IsReady => _cooldownTimer.ExpiredOrNotRunning(Runner);
		public float CooldownRemainingTime => _cooldownTimer.RemainingTime(Runner).GetValueOrDefault();
		public float CooldownTotal => _cooldown;

		// PRIVATE MEMBERS

		[Header("Ability Settings")]
		[SerializeField]
		private float _cooldown = 12f;
		[SerializeField]
		private float _spawnDistance = 2.5f;
		[SerializeField]
		private float _spawnHeightOffset = 0f;

		[Header("References")]
		[SerializeField]
		private TheissShieldWall _shieldPrefab;

		[Networked]
		private TickTimer _cooldownTimer { get; set; }
		[Networked]
		private NetworkId _activeShieldId { get; set; }

		private PlayerAgent _agent;

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_agent = GetComponent<PlayerAgent>();
		}

		// NetworkBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			if (_agent == null || _agent.Owner == null || _agent.Health.IsAlive == false)
				return;

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.AltFire))
			{
				TryCastShield();
			}
		}

		// PRIVATE METHODS

		private void TryCastShield()
		{
			if (IsOnCooldown)
				return;
			if (_shieldPrefab == null)
				return;
			if (_agent.Weapons == null || _agent.Weapons.FireTransform == null)
				return;
			if (HasStateAuthority == false)
				return;

			DespawnActiveShieldIfAny();

			var fireTransform = _agent.Weapons.FireTransform;
			var forward = fireTransform.forward;
			forward.y = 0f;
			if (forward.sqrMagnitude < 0.0001f)
			{
				forward = transform.forward;
				forward.y = 0f;
			}
			forward.Normalize();

			var spawnPosition = transform.position + forward * _spawnDistance + Vector3.up * _spawnHeightOffset;
			var spawnRotation = Quaternion.LookRotation(forward, Vector3.up);

			var shield = Runner.Spawn(_shieldPrefab, spawnPosition, spawnRotation, Object.InputAuthority);
			if (shield != null)
			{
				shield.Initialize(_agent.Owner);
				_activeShieldId = shield.Object.Id;
			}

			_cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);
		}

		private void DespawnActiveShieldIfAny()
		{
			if (_activeShieldId.IsValid == false)
				return;

			var existingObject = Runner.FindObject(_activeShieldId);
			if (existingObject != null)
			{
				Runner.Despawn(existingObject);
			}

			_activeShieldId = default;
		}
	}
}
