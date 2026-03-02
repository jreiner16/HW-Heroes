using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Theiss's ultimate ability: places a debuff field that reduces enemy outgoing damage.
	/// Triggered by the X key.
	/// </summary>
	[DefaultExecutionOrder(5)]
	public class TheissUltimateAbility : ContextBehaviour
	{
		// PUBLIC MEMBERS

		public bool  IsOnCooldown => _cooldownTimer.ExpiredOrNotRunning(Runner) == false;
		public bool  IsReady      => _cooldownTimer.ExpiredOrNotRunning(Runner);

		public float CooldownRemainingTime => _cooldownTimer.RemainingTime(Runner).GetValueOrDefault();
		public float CooldownTotal         => _cooldown;

		// PRIVATE MEMBERS

		[Header("Ability Settings")]
		[SerializeField]
		private float _cooldown = 15f;
		[SerializeField, Tooltip("How many seconds of ultimate cooldown are removed per 1 damage dealt.")]
		private float _cooldownSecondsPerDamage = 0.05f;
		[SerializeField]
		private float _spawnDistance = 2.5f;
		[SerializeField]
		private float _spawnHeightOffset = 0f;

		[Header("References")]
		[SerializeField]
		private TheissDamageDebuffField _fieldPrefab;

		[Networked]
		private TickTimer _cooldownTimer { get; set; }
		[Networked]
		private NetworkId _activeFieldId { get; set; }

		private PlayerAgent _agent;

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_agent = GetComponent<PlayerAgent>();
		}

		// NetworkBehaviour INTERFACE

		public override void FixedUpdateNetwork()
		{
			if (_agent.Owner == null || _agent.Health.IsAlive == false)
				return;

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.X))
			{
				TryCastField();
			}
		}

		// PRIVATE METHODS

		public void AccelerateCooldownFromDamage(float damageDealt)
		{
			if (HasStateAuthority == false)
				return;
			if (_cooldownSecondsPerDamage <= 0f || damageDealt <= 0f)
				return;

			ReduceCooldownSeconds(damageDealt * _cooldownSecondsPerDamage);
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

		private void TryCastField()
		{
			if (IsOnCooldown)
				return;

			if (_fieldPrefab == null)
				return;

			if (_agent.Weapons?.FireTransform == null)
				return;

			if (HasStateAuthority == false)
				return;

			DespawnActiveFieldIfAny();

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
			var spawnRotation = Quaternion.identity;

			var field = Runner.Spawn(_fieldPrefab, spawnPosition, spawnRotation, Object.InputAuthority);
			if (field != null)
			{
				field.Initialize(_agent.Owner);
				_activeFieldId = field.Object.Id;
			}

			_cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);
		}

		private void DespawnActiveFieldIfAny()
		{
			if (_activeFieldId.IsValid == false)
				return;

			var existingObject = Runner.FindObject(_activeFieldId);
			if (existingObject != null)
			{
				Runner.Despawn(existingObject);
			}

			_activeFieldId = default;
		}
	}
}
