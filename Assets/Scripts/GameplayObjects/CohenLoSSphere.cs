using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Cohen ultimate: a sphere that blocks line of sight (visual) and applies team-based effects.
	/// - Allies heal while inside
	/// - Enemies take damage while inside
	/// The collider should be a trigger so it doesn't physically collide.
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Cohen LOS Sphere")]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(SphereCollider))]
	public sealed class CohenLoSSphere : ContextBehaviour
	{
		[SerializeField] private float _lifetime = 8f;

		[Header("Heal / Damage")]
		[SerializeField] private float _allyHealPerSecond = 10f;
		[SerializeField] private float _enemyDamagePerSecond = 20f;
		[SerializeField] private int _ticksPerSecond = 5;

		[Networked] public PlayerRef OwnerPlayer { get; private set; }
		[Networked] public ETeam OwnerTeam { get; private set; }
		[Networked] private TickTimer _lifetimeTimer { get; set; }
		[Networked] private TickTimer _tickTimer { get; set; }

		private readonly HashSet<Health> _insideAllies = new();
		private readonly HashSet<Health> _insideEnemies = new();

		public void Initialize(Player owner)
		{
			if (HasStateAuthority == false || owner == null)
				return;

			OwnerPlayer = owner.Object.InputAuthority;
			OwnerTeam = owner.Team;
		}

		public override void Spawned()
		{
			if (HasStateAuthority && _lifetime > 0f)
			{
				_lifetimeTimer = TickTimer.CreateFromSeconds(Runner, _lifetime);
			}

			// Ensure we don't physically collide.
			var col = GetComponent<SphereCollider>();
			if (col != null)
			{
				col.isTrigger = true;
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (HasStateAuthority == false)
				return;

			if (_ticksPerSecond > 0 && _tickTimer.ExpiredOrNotRunning(Runner))
			{
				ApplyTick();
			}

			if (_lifetime > 0f && _lifetimeTimer.Expired(Runner))
			{
				Runner.Despawn(Object);
			}
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			_insideAllies.Clear();
			_insideEnemies.Clear();
		}

		private void OnTriggerEnter(Collider other)
		{
			if (HasStateAuthority == false)
				return;

			var agent = other.GetComponentInParent<PlayerAgent>();
			if (agent == null || agent.Owner == null || agent.Health == null)
				return;

			if (OwnerTeam == ETeam.None || agent.Owner.Team == ETeam.None)
				return;

			if (agent.Owner.Team == OwnerTeam)
			{
				_insideAllies.Add(agent.Health);
			}
			else
			{
				_insideEnemies.Add(agent.Health);
			}
		}

		private void OnTriggerExit(Collider other)
		{
			if (HasStateAuthority == false)
				return;

			var agent = other.GetComponentInParent<PlayerAgent>();
			if (agent == null || agent.Health == null)
				return;

			_insideAllies.Remove(agent.Health);
			_insideEnemies.Remove(agent.Health);
		}

		private void ApplyTick()
		{
			_tickTimer = TickTimer.CreateFromSeconds(Runner, 1f / Mathf.Max(1, _ticksPerSecond));

			_insideAllies.RemoveWhere(h => h == null || h.IsAlive == false);
			_insideEnemies.RemoveWhere(h => h == null || h.IsAlive == false);

			float healAmount = _allyHealPerSecond > 0f ? _allyHealPerSecond / Mathf.Max(1, _ticksPerSecond) : 0f;
			float damageAmount = _enemyDamagePerSecond > 0f ? _enemyDamagePerSecond / Mathf.Max(1, _ticksPerSecond) : 0f;

			if (healAmount > 0f)
			{
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

			if (damageAmount > 0f)
			{
				foreach (var health in _insideEnemies)
				{
					HitData hitData = new HitData
					{
						Action = EHitAction.Damage,
						Amount = damageAmount,
						Position = health.transform.position,
						Direction = (health.transform.position - transform.position).normalized,
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
}

