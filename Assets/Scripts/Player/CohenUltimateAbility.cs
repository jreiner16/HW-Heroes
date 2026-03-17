using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Cohen's ultimate ability: throws a LOS-blocking sphere.
	/// Allies heal inside; enemies take damage. Triggered by the X key.
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Cohen Ultimate Ability")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(5)]
	public class CohenUltimateAbility : ContextBehaviour
	{
		public bool  IsOnCooldown => _cooldownTimer.ExpiredOrNotRunning(Runner) == false;
		public bool  IsReady      => _cooldownTimer.ExpiredOrNotRunning(Runner);
		public float CooldownRemainingTime => _cooldownTimer.RemainingTime(Runner).GetValueOrDefault();
		public float CooldownTotal         => _cooldown;

		[Header("Ability Settings")]
		[SerializeField] private float _cooldown = 20f;
		[SerializeField, Tooltip("How many seconds of ultimate cooldown are removed per 1 damage dealt.")]
		private float _cooldownSecondsPerDamage = 0.05f;
		[SerializeField] private float _spawnDistance = 4.0f;
		[SerializeField] private float _spawnHeightOffset = 0.5f;

		[Header("References")]
		[SerializeField] private CohenLoSSphere _spherePrefab;

		[Networked] private TickTimer _cooldownTimer { get; set; }
		[Networked] private NetworkId _activeSphereId { get; set; }

		private PlayerAgent _agent;

		protected void Awake()
		{
			_agent = GetComponent<PlayerAgent>();
		}

		public override void FixedUpdateNetwork()
		{
			if (_agent == null || _agent.Owner == null || _agent.Health.IsAlive == false)
				return;

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.X))
			{
				TryThrowSphere();
			}
		}

		// PUBLIC METHODS

		public void AccelerateCooldownFromDamage(float damageDealt)
		{
			if (HasStateAuthority == false)
				return;
			if (_cooldownSecondsPerDamage <= 0f || damageDealt <= 0f)
				return;

			ReduceCooldownSeconds(damageDealt * _cooldownSecondsPerDamage);
		}

		// PRIVATE METHODS

		private void TryThrowSphere()
		{
			if (IsOnCooldown)
				return;

			if (_spherePrefab == null)
				return;

			if (_agent.Weapons?.FireTransform == null)
				return;

			if (HasStateAuthority == false)
				return;

			DespawnActiveSphereIfAny();

			var forward = _agent.Weapons.AimDirection;
			forward.y = 0f;
			if (forward.sqrMagnitude < 0.0001f)
			{
				forward = transform.forward;
				forward.y = 0f;
			}
			forward.Normalize();

			var spawnPosition = transform.position + forward * _spawnDistance + Vector3.up * _spawnHeightOffset;
			var spawnRotation = Quaternion.identity;

			var sphere = Runner.Spawn(_spherePrefab, spawnPosition, spawnRotation, Object.InputAuthority);
			if (sphere != null)
			{
				sphere.Initialize(_agent.Owner);
				_activeSphereId = sphere.Object.Id;
			}

			_cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);
		}

		private void ReduceCooldownSeconds(float seconds)
		{
			if (seconds <= 0f)
				return;
			if (_cooldownTimer.ExpiredOrNotRunning(Runner))
				return; // already ready

			float remaining = _cooldownTimer.RemainingTime(Runner).GetValueOrDefault();
			float newRemaining = Mathf.Max(0f, remaining - seconds);
			_cooldownTimer = newRemaining > 0f ? TickTimer.CreateFromSeconds(Runner, newRemaining) : default;
		}

		private void DespawnActiveSphereIfAny()
		{
			if (_activeSphereId.IsValid == false)
				return;

			var existingObject = Runner.FindObject(_activeSphereId);
			if (existingObject != null)
			{
				Runner.Despawn(existingObject);
			}

			_activeSphereId = default;
		}
	}
}

