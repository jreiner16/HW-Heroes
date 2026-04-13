using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Area ultimate that weakens enemy outgoing damage while they stay inside the field.
	/// </summary>
	[RequireComponent(typeof(Collider))]
	public class TheissDamageDebuffField : ContextBehaviour
	{
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			_activeFields.Clear();
		}

		[SerializeField]
		private float _lifetime = 8f;
		[SerializeField, Range(0.1f, 1f)]
		private float _enemyDamageMultiplier = 0.7f;
		[SerializeField]
		private float _allyHealPerSecond = 5f;
		[SerializeField]
		private int _healTicksPerSecond = 5;

		[Networked]
		public PlayerRef OwnerPlayer { get; private set; }
		[Networked]
		public ETeam OwnerTeam { get; private set; }
		[Networked]
		private TickTimer _lifetimeTimer { get; set; }
		[Networked]
		private TickTimer _healTickTimer { get; set; }

		private readonly HashSet<PlayerRef> _insideEnemies = new();
		private readonly HashSet<Health> _insideAllies = new();
		private static readonly HashSet<TheissDamageDebuffField> _activeFields = new();
		private Collider _fieldCollider;

		public static float GetOutgoingDamageMultiplier(NetworkRunner runner, PlayerRef instigatorRef)
		{
			if (runner == null || instigatorRef == default)
				return 1f;

			float multiplier = 1f;

			foreach (var field in _activeFields)
			{
				if (field == null || field.Object == null || field.Runner != runner)
					continue;
				if (field._insideEnemies.Contains(instigatorRef) == false)
					continue;

				multiplier = Mathf.Min(multiplier, field._enemyDamageMultiplier);
			}

			return multiplier;
		}

		public static bool IsPlayerInsideField(NetworkRunner runner, PlayerAgent playerAgent)
		{
			if (runner == null || playerAgent == null || playerAgent.Owner == null || playerAgent.Health.IsAlive == false)
				return false;

			var playerPosition = playerAgent.transform.position;

			foreach (var field in _activeFields)
			{
				if (field == null || field.Object == null || field.Runner != runner)
					continue;
				if (field._fieldCollider == null)
					continue;
				if (field._fieldCollider.bounds.Contains(playerPosition) == false)
					continue;

				return true;
			}

			return false;
		}

		public void Initialize(Player owner)
		{
			if (HasStateAuthority == false || owner == null)
				return;

			OwnerPlayer = owner.Object.InputAuthority;
			OwnerTeam = owner.Team;
		}

		protected void Awake()
		{
			_fieldCollider = GetComponent<Collider>();
		}

		public override void Spawned()
		{
			_activeFields.Add(this);

			if (HasStateAuthority && _lifetime > 0f)
			{
				_lifetimeTimer = TickTimer.CreateFromSeconds(Runner, _lifetime);
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (HasStateAuthority == false)
				return;

			if (_allyHealPerSecond > 0f && _healTicksPerSecond > 0 && _healTickTimer.ExpiredOrNotRunning(Runner))
			{
				HealAlliesInField();
			}

			if (_lifetime > 0f && _lifetimeTimer.Expired(Runner))
			{
				Runner.Despawn(Object);
			}
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			_activeFields.Remove(this);
			_insideEnemies.Clear();
			_insideAllies.Clear();
		}

		private void OnTriggerEnter(Collider other)
		{
			if (HasStateAuthority == false)
				return;

			var agent = other.GetComponentInParent<PlayerAgent>();
			if (agent == null || agent.Owner == null || agent.Object == null)
				return;
			if (OwnerTeam == ETeam.None || agent.Owner.Team == ETeam.None)
				return;

			if (agent.Owner.Team == OwnerTeam)
			{
				if (agent.Health != null)
				{
					_insideAllies.Add(agent.Health);
				}
			}
			else
			{
				_insideEnemies.Add(agent.Object.InputAuthority);
			}
		}

		private void OnTriggerExit(Collider other)
		{
			if (HasStateAuthority == false)
				return;

			var agent = other.GetComponentInParent<PlayerAgent>();
			if (agent == null || agent.Object == null)
				return;

			_insideEnemies.Remove(agent.Object.InputAuthority);
			if (agent.Health != null)
			{
				_insideAllies.Remove(agent.Health);
			}
		}

		private void HealAlliesInField()
		{
			_healTickTimer = TickTimer.CreateFromSeconds(Runner, 1f / _healTicksPerSecond);

			if (_insideAllies.Count == 0)
				return;

			_insideAllies.RemoveWhere(health => health == null || health.IsAlive == false);
			if (_insideAllies.Count == 0)
				return;

			float healAmount = _allyHealPerSecond / _healTicksPerSecond;
			foreach (var health in _insideAllies)
			{
				HitData hitData = new HitData
				{
					Action = EHitAction.Heal,
					Amount = healAmount,
					Position = health.transform.position,
					Direction = Vector3.up,
					Normal = Vector3.up,
					InstigatorRef = OwnerPlayer,
					Target = health,
					HitType = EHitType.None,
				};

				HitUtility.ProcessHit(ref hitData);
			}
		}
	}
}
