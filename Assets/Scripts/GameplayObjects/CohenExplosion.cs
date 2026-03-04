using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Cohen primary-shot explosion: spawned at the projectile's first impact point.
	/// - Enemies within radius take damage (60 max at center, steep falloff to edges).
	/// - Allies within radius are healed  (75 max at center, gentler falloff to edges).
	///
	/// Set _impactObjectPrefab on CohenPrimaryProjectile to this prefab's NetworkObject.
	/// Input authority on this spawned object identifies the shooting player.
	/// </summary>
	[AddComponentMenu("Projectiles/Cohen Explosion")]
	public sealed class CohenExplosion : ContextBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private LayerMask _targetMask;
		[SerializeField]
		private LayerMask _blockingMask;
		[SerializeField, Tooltip("Offset applied to the overlap origin to avoid terrain edge-cases")]
		private Vector3 _explosionCheckOffset = new(0f, 0.5f, 0f);

		[Header("Damage (Enemies)")]
		[SerializeField, Tooltip("Radius of full damage (no falloff inside this)")]
		private float _innerDamageRadius = 1f;
		[SerializeField, Tooltip("Outer edge of the damage sphere (no damage beyond this)")]
		private float _outerDamageRadius = 5f;
		[SerializeField]
		private float _centerDamage = 60f;
		[SerializeField]
		private float _outerDamage = 0f;

		[Header("Healing (Allies)")]
		[SerializeField, Tooltip("Radius of full healing (no falloff inside this)")]
		private float _innerHealRadius = 2.5f;
		[SerializeField, Tooltip("Outer edge of the heal sphere (no healing beyond this)")]
		private float _outerHealRadius = 5f;
		[SerializeField]
		private float _centerHeal = 75f;
		[SerializeField, Tooltip("Heal amount at the very edge of the heal radius (less falloff than damage)")]
		private float _outerHeal = 40f;

		[Header("Misc")]
		[SerializeField]
		private float _despawnDelay = 3f;
		[SerializeField]
		private Transform _effectRoot;

		private TickTimer _despawnTimer;

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			ShowEffect();

			if (HasStateAuthority == true)
			{
				Explode();
			}

			_despawnTimer = TickTimer.CreateFromSeconds(Runner, _despawnDelay);
		}

		public override void FixedUpdateNetwork()
		{
			if (HasStateAuthority == false)
				return;
			if (_despawnTimer.Expired(Runner) == false)
				return;

			Runner.Despawn(Object);
		}

		// PRIVATE METHODS

		private void Explode()
		{
			// Resolve the shooter's team so we can differentiate allies from enemies.
			var instigatorPlayerObject = Runner.GetPlayerObject(Object.InputAuthority);
			var instigatorPlayer       = instigatorPlayerObject != null ? instigatorPlayerObject.GetComponent<Player>() : null;
			var instigatorTeam         = instigatorPlayer != null ? instigatorPlayer.Team : ETeam.None;

			float maxRadius = Mathf.Max(_outerDamageRadius, _outerHealRadius);
			var   position  = transform.position + _explosionCheckOffset;

			var hits     = ListPool.Get<LagCompensatedHit>(16);
			var hitRoots = ListPool.Get<int>(16);

			int count = Runner.LagCompensation.OverlapSphere(
				position, maxRadius,
				Object.InputAuthority,
				hits, _targetMask,
				HitOptions.IncludePhysX);

			for (int i = 0; i < count; i++)
			{
				var hit       = hits[i];
				var hitTarget = HitUtility.GetHitTarget(hit.Hitbox, hit.Collider);

				if (hitTarget == null)
					continue;

				// Each root object should only be processed once.
				int hitRootID = hit.Hitbox != null ? hit.Hitbox.Root.GetInstanceID() : 0;
				if (hitRoots.Contains(hitRootID) == true)
					continue;

				var targetHealth = hitTarget as Health;
				if (targetHealth == null)
					continue;

				// Determine whether this target is an ally.
				var targetPlayerObject = Runner.GetPlayerObject(targetHealth.Object.InputAuthority);
				var targetPlayer       = targetPlayerObject != null ? targetPlayerObject.GetComponent<Player>() : null;
				var targetTeam         = targetPlayer != null ? targetPlayer.Team : ETeam.None;

				bool isSelf  = Object.InputAuthority == targetHealth.Object.InputAuthority;
				bool isAlly  = isSelf || (instigatorTeam != ETeam.None && targetTeam != ETeam.None && instigatorTeam == targetTeam);

				// Direction and distance from explosion centre to target.
				var   direction = hit.GameObject.transform.position - position;
				float distance  = direction.magnitude;
				direction /= distance;

				// Line-of-sight check: skip targets behind solid geometry.
				if (Runner.GetPhysicsScene().Raycast(position, direction, distance, _blockingMask) == true)
					continue;

				if (hitRootID != 0)
					hitRoots.Add(hitRootID);

				hit.Point  = hit.GameObject.transform.position;
				hit.Normal = -direction;

				if (isAlly == true)
				{
					if (distance > _outerHealRadius)
						continue;

					float heal = _centerHeal;

					if (_innerHealRadius < _outerHealRadius && _centerHeal != _outerHeal && distance > _innerHealRadius)
					{
						heal = MathUtility.Map(_innerHealRadius, _outerHealRadius, _centerHeal, _outerHeal, distance);
					}

					HitData healData = new HitData
					{
						Action        = EHitAction.Heal,
						Amount        = heal,
						Position      = hit.Point,
						Normal        = hit.Normal,
						Direction     = direction,
						InstigatorRef = Object.InputAuthority,
						Target        = hitTarget,
						HitType       = EHitType.Explosion,
					};

					HitUtility.ProcessHit(ref healData);
				}
				else
				{
					if (distance > _outerDamageRadius)
						continue;

					float damage = _centerDamage;

					if (_innerDamageRadius < _outerDamageRadius && _centerDamage != _outerDamage && distance > _innerDamageRadius)
					{
						damage = MathUtility.Map(_innerDamageRadius, _outerDamageRadius, _centerDamage, _outerDamage, distance);
					}

					HitUtility.ProcessHit(Object.InputAuthority, direction, hit, damage, EHitType.Explosion);
				}
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
				_effectRoot.localScale = Vector3.one * Mathf.Max(_outerDamageRadius, _outerHealRadius) * 2f;
			}
		}
	}
}
