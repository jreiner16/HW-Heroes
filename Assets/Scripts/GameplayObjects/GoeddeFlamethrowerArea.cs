using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// A ground-based flamethrower area spawned by Goedde's E ability at a targeted floor point.
	/// Each damage interval it finds all enemies within <see cref="_radius"/> using lag-compensated overlap,
	/// checks line of sight from the area centre to each target, then applies damage.
	/// Automatically despawns after <see cref="_despawnDelay"/> seconds — set this to match the
	/// ability's burn duration in the inspector.
	/// </summary>
	[AddComponentMenu("Projectiles/Goedde Flamethrower Area")]
	public class GoeddeFlamethrowerArea : ContextBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private float _radius = 3f;
		[SerializeField]
		private float _damagePerSecond = 40f;
		[SerializeField, Tooltip("How many damage pulses per second.")]
		private int _hitsPerSecond = 4;
		[SerializeField]
		private LayerMask _targetMask;
		[SerializeField, Tooltip("Layers that block line-of-sight between the area and a target.")]
		private LayerMask _blockingMask;
		[SerializeField, Tooltip("Vertical offset applied to the overlap origin to avoid floor clipping.")]
		private Vector3 _areaCheckOffset = new(0f, 0.5f, 0f);
		[SerializeField, Tooltip("How long the flamethrower stays active. Should match the ability's burn duration.")]
		private float _despawnDelay = 3f;
		[SerializeField, Tooltip("Root of the visual effect. Activated on non-server clients.")]
		private Transform _effectRoot;

		[Networked]
		private TickTimer _damageTimer { get; set; }
		[Networked]
		private TickTimer _despawnTimer { get; set; }

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			ShowEffect();

			if (HasStateAuthority)
			{
				_damageTimer  = TickTimer.CreateFromSeconds(Runner, 1f / Mathf.Max(1, _hitsPerSecond));
				_despawnTimer = TickTimer.CreateFromSeconds(Runner, _despawnDelay);
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (HasStateAuthority == false)
				return;

			if (_despawnTimer.Expired(Runner))
			{
				Runner.Despawn(Object);
				return;
			}

			if (_damageTimer.ExpiredOrNotRunning(Runner))
			{
				DamageTargets();
				_damageTimer = TickTimer.CreateFromSeconds(Runner, 1f / Mathf.Max(1, _hitsPerSecond));
			}
		}

		// PRIVATE METHODS

		private void DamageTargets()
		{
			var position = transform.position + _areaCheckOffset;
			float damage = _damagePerSecond / Mathf.Max(1, _hitsPerSecond);

			var hits     = ListPool.Get<LagCompensatedHit>(16);
			var hitRoots = ListPool.Get<int>(16);

			int count = Runner.LagCompensation.OverlapSphere(
				position, _radius,
				Object.InputAuthority,
				hits, _targetMask,
				HitOptions.IncludePhysX);

			for (int i = 0; i < count; i++)
			{
				var hit       = hits[i];
				var hitTarget = HitUtility.GetHitTarget(hit.Hitbox, hit.Collider);

				if (hitTarget == null)
					continue;

				// Process each root object only once per pulse.
				int hitRootID = hit.Hitbox != null ? hit.Hitbox.Root.GetInstanceID() : 0;
				if (hitRoots.Contains(hitRootID))
					continue;

				// Line-of-sight check from the area centre to the target.
				var   targetPos = hit.GameObject.transform.position;
				var   direction = targetPos - position;
				float distance  = direction.magnitude;

				if (distance > 0f)
					direction /= distance;

				if (Runner.GetPhysicsScene().Raycast(position, direction, distance, _blockingMask))
					continue;

				if (hitRootID != 0)
					hitRoots.Add(hitRootID);

				HitUtility.ProcessHit(Object.InputAuthority, direction, hit, damage, EHitType.Explosion);
			}

			ListPool.Return(hitRoots);
			ListPool.Return(hits);
		}

		private void ShowEffect()
		{
			if (Runner.Mode == SimulationModes.Server)
				return;

			if (_effectRoot != null)
			{
				_effectRoot.SetActive(true);
			}
		}
	}
}
