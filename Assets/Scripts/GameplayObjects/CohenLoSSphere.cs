using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Cohen ultimate: a large inky sphere that blocks line-of-sight from outside to inside
	/// and heals allies who stand inside it over time.
	///
	/// LOS blocking: any projectile whose owner is outside the sphere radius is stopped at
	/// the sphere surface. Shots fired from inside pass through freely. This is implemented
	/// via <see cref="IProjectileBlocker"/> — the sphere's LOS-blocker child collider
	/// (a non-trigger SphereCollider on the hitscan hit layer) will be picked up by
	/// <see cref="ProjectileUtility.ProjectileCast"/> and routed here.
	///
	/// Prefab setup required:
	///   • This GameObject: trigger SphereCollider for the heal zone.
	///   • Child "LOSBlocker": non-trigger SphereCollider on the hitscan layer. Radius is
	///     synced at spawn to match the trigger collider's radius. Assign to _losBlockerRoot.
	///   • Child "Visual": inky sphere mesh + pages particle system. Assign to _visualRoot.
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Cohen LOS Sphere")]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(SphereCollider))]
	public sealed class CohenLoSSphere : ContextBehaviour, IProjectileBlocker
	{
		// PRIVATE MEMBERS

		[SerializeField] private float _lifetime = 8f;

		[Header("Healing")]
		[Tooltip("HP healed per second to all allies standing inside the sphere. " +
		         "High enough to sustain through most damage ultimates, but not survive burst kills.")]
		[SerializeField] private float _allyHealPerSecond = 60f;
		[SerializeField] private int _ticksPerSecond = 5;

		[Header("LOS Blocking")]
		[Tooltip("Child GameObject with a non-trigger SphereCollider on the hitscan hit layer. " +
		         "Radius is synced at spawn. Without this, hitscan will not be blocked.")]
		[SerializeField] private Transform _losBlockerRoot;

		[Header("Visuals")]
		[Tooltip("Root of the inky sphere mesh and pages particle system. " +
		         "Disabled on the dedicated server.")]
		[SerializeField] private Transform _visualRoot;

		[Networked] public PlayerRef OwnerPlayer { get; private set; }
		[Networked] public ETeam OwnerTeam { get; private set; }
		[Networked] private TickTimer _lifetimeTimer { get; set; }
		[Networked] private TickTimer _tickTimer { get; set; }

		private SphereCollider _triggerCollider;
		private readonly HashSet<Health> _insideAllies = new();

		// PUBLIC METHODS

		public void Initialize(Player owner)
		{
			if (HasStateAuthority == false || owner == null)
				return;

			OwnerPlayer = owner.Object.InputAuthority;
			OwnerTeam = owner.Team;
		}

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			_triggerCollider = GetComponent<SphereCollider>();
			if (_triggerCollider != null)
				_triggerCollider.isTrigger = true;

			// Sync the LOS-blocker child to the same radius as the trigger zone.
			if (_losBlockerRoot != null)
			{
				var losCol = _losBlockerRoot.GetComponent<SphereCollider>();
				if (losCol != null && _triggerCollider != null)
					losCol.radius = _triggerCollider.radius;
			}

			// Server doesn't need the visual.
			if (_visualRoot != null && Runner.Mode == SimulationModes.Server)
				_visualRoot.gameObject.SetActive(false);

			if (HasStateAuthority && _lifetime > 0f)
				_lifetimeTimer = TickTimer.CreateFromSeconds(Runner, _lifetime);
		}

		public override void FixedUpdateNetwork()
		{
			if (HasStateAuthority == false)
				return;

			if (_ticksPerSecond > 0 && _tickTimer.ExpiredOrNotRunning(Runner))
				ApplyHealTick();

			if (_lifetime > 0f && _lifetimeTimer.Expired(Runner))
				Runner.Despawn(Object);
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			_insideAllies.Clear();
		}

		// IProjectileBlocker INTERFACE

		/// <summary>
		/// Blocks projectiles whose owner is standing OUTSIDE the sphere.
		/// Shots fired from inside pass through so allies can still engage.
		/// </summary>
		public bool ShouldBlockProjectile(NetworkRunner runner, PlayerRef projectileOwner)
		{
			var playerObject = runner.GetPlayerObject(projectileOwner);
			if (playerObject == null)
				return true;

			var agent = playerObject.GetComponentInChildren<PlayerAgent>();
			if (agent == null)
				return true;

			float radius = _triggerCollider != null ? _triggerCollider.radius : 5f;
			float distSqr = (agent.transform.position - transform.position).sqrMagnitude;

			// Block only if the shooter is outside.
			return distSqr > radius * radius;
		}

		// PRIVATE METHODS

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
				_insideAllies.Add(agent.Health);
		}

		private void OnTriggerExit(Collider other)
		{
			if (HasStateAuthority == false)
				return;

			var agent = other.GetComponentInParent<PlayerAgent>();
			if (agent == null || agent.Health == null)
				return;

			_insideAllies.Remove(agent.Health);
		}

		private void ApplyHealTick()
		{
			_tickTimer = TickTimer.CreateFromSeconds(Runner, 1f / Mathf.Max(1, _ticksPerSecond));

			_insideAllies.RemoveWhere(h => h == null || h.IsAlive == false);

			float healAmount = _allyHealPerSecond / Mathf.Max(1, _ticksPerSecond);
			if (healAmount <= 0f)
				return;

			foreach (var health in _insideAllies)
			{
				HitData hitData = new HitData
				{
					Action        = EHitAction.Heal,
					Amount        = healAmount,
					Position      = health.transform.position,
					Direction     = Vector3.up,
					Normal        = Vector3.up,
					InstigatorRef = OwnerPlayer,
					Target        = health,
					HitType       = EHitType.None,
				};

				HitUtility.ProcessHit(ref hitData);
			}
		}
	}
}
