using Fusion;
using UnityEngine;

namespace Projectiles
{
	[RequireComponent(typeof(Health))]
	public class TheissShieldWall : ContextBehaviour, IProjectileBlocker
	{
		[SerializeField]
		private float _lifetime = 10f;

		[Networked]
		public PlayerRef OwnerPlayer { get; private set; }
		[Networked]
		public ETeam OwnerTeam { get; private set; }
		[Networked]
		private TickTimer _lifetimeTimer { get; set; }

		private Health _health;

		protected void Awake()
		{
			_health = GetComponent<Health>();
		}

		public override void Spawned()
		{
			if (_health != null)
			{
				_health.FatalHitTaken += OnFatalHitTaken;
			}

			if (HasStateAuthority == true && _lifetime > 0f)
			{
				_lifetimeTimer = TickTimer.CreateFromSeconds(Runner, _lifetime);
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (HasStateAuthority == false)
				return;

			if (_lifetime > 0f && _lifetimeTimer.Expired(Runner) == true)
			{
				Runner.Despawn(Object);
			}
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			if (_health != null)
			{
				_health.FatalHitTaken -= OnFatalHitTaken;
			}
		}

		public void Initialize(Player owner)
		{
			if (owner == null || HasStateAuthority == false)
				return;

			OwnerPlayer = owner.Object.InputAuthority;
			OwnerTeam = owner.Team;
		}

		public bool ShouldBlockProjectile(NetworkRunner runner, PlayerRef projectileOwner)
		{
			// Fail safe: if owner team is not known, block.
			if (runner == null || OwnerTeam == ETeam.None)
				return true;

			var shooterObject = runner.GetPlayerObject(projectileOwner);
			var shooter = shooterObject != null ? shooterObject.GetComponent<Player>() : null;
			if (shooter == null || shooter.Team == ETeam.None)
				return true;

			// Block enemy projectiles only.
			return shooter.Team != OwnerTeam;
		}

		private void OnFatalHitTaken(HitData hitData)
		{
			if (HasStateAuthority == false || Object == null)
				return;

			Runner.Despawn(Object);
		}
	}
}
