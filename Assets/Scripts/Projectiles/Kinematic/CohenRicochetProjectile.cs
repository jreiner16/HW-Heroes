using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Cohen's E-ability healing ricochet projectile.
	/// Bounces off wall geometry and heals allies on contact without stopping.
	/// Passes through enemies without effect. No gravity.
	///
	/// Setup:
	///  - _hitMask: include both player hitbox layers and wall/environment layers.
	///  - _bounceMask: include only the wall/environment layers that should cause a bounce.
	///  - Add this prefab to the KinematicProjectileBuffer._projectilePrefabs array on Cohen_Agent.
	///  - Assign this prefab to CohenMovementAbility._projectilePrefab on Cohen_Agent.
	/// </summary>
	public class CohenRicochetProjectile : KinematicProjectile
	{
		[SerializeField] private float     _healAmount                  = 30f;
		[SerializeField] private LayerMask _hitMask;
		[SerializeField] private LayerMask _bounceMask;
		[SerializeField] private float     _bounceObjectRadius          = 0.1f;
		[SerializeField] private float     _bounceVelocityMultiplierStart = 1f;
		[SerializeField] private float     _bounceVelocityMultiplierEnd   = 0.9f;
		[SerializeField, Tooltip("Number of bounces over which velocity scales from start to end")]
		private int _bounceVelocityScale = 10;
		[SerializeField] private float     _stopSpeed  = 1f;
		[SerializeField] private AudioEffect _bounceSound;

		private float _maxBounceVolume;
		private int   _visibleBounceCount;

		// KinematicProjectile INTERFACE

		public override void OnFixedUpdate(ref KinematicData data)
		{
			base.OnFixedUpdate(ref data);

			var runner = Context.Runner;

			if (data.IsFinished || data.HasStopped)
				return;

			var previousPosition = GetMovePosition(ref data, runner.Tick - 1, runner.DeltaTime);
			var nextPosition     = GetMovePosition(ref data, runner.Tick,     runner.DeltaTime);

			var   direction = nextPosition - previousPosition;
			float distance  = direction.magnitude;

			if (distance <= 0f)
				return;

			direction /= distance;

			AdjustForProjectileLength(ref previousPosition, ref distance, direction, data.Position);

			// Ignore self only right after fire
			bool ignoreInputAuthority = runner.Tick < data.FireTick + 10;

			if (ProjectileUtility.ProjectileCast(runner, Context.Owner,
				    previousPosition - direction * _bounceObjectRadius,
				    direction, distance + 2f * _bounceObjectRadius,
				    _hitMask, out LagCompensatedHit hit, ignoreInputAuthority))
			{
				var hitTarget = HitUtility.GetHitTarget(hit.Hitbox, hit.Collider);

				if (hitTarget != null)
				{
					// Player collision: heal allies and continue flying without stopping or bouncing.
					TryHealAlly(hitTarget, hit, direction);
				}
				else
				{
					// Geometry collision: bounce if on the bounce mask, otherwise stop.
					bool doBounce = hit.GameObject != null && ((1 << hit.GameObject.layer) & _bounceMask) != 0;

					if (doBounce)
					{
						ProcessBounce(ref data, hit, direction, distance);
					}
					else
					{
						data.ImpactPosition = hit.Point;
						data.ImpactNormal   = (hit.Normal - direction) * 0.5f;
						data.IsFinished     = true;
						SpawnImpact(data.ImpactPosition, data.ImpactNormal);
					}
				}
			}
		}

		public override void Activate(ref KinematicData data)
		{
			base.Activate(ref data);
			_visibleBounceCount = data.Advanced.BounceCount;
		}

		public override void Render(ref KinematicData data, ref KinematicData fromData, float alpha)
		{
			base.Render(ref data, ref fromData, alpha);

			if (data.Advanced.BounceCount != _visibleBounceCount)
			{
				OnBounceRender(ref data);
				_visibleBounceCount = data.Advanced.BounceCount;
			}
		}

		protected override Vector3 GetRenderPosition(ref KinematicData data, ref KinematicData fromData, float alpha)
		{
			var   runner     = Context.Runner;
			float renderTime = Context.Owner == runner.LocalPlayer ? runner.LocalRenderTime : runner.RemoteRenderTime;
			float floatTick  = renderTime / runner.DeltaTime;

			if (data.HasStopped && data.Advanced.MoveStartTick <= floatTick)
				return data.ImpactPosition;

			// Use the correct data set depending on whether we are before or after the last bounce tick.
			ref var moveData = ref floatTick < data.Advanced.MoveStartTick ? ref fromData : ref data;
			return GetMovePosition(ref moveData, floatTick, runner.DeltaTime);
		}

		// MONOBEHAVIOUR

		protected override void Awake()
		{
			base.Awake();
			_maxBounceVolume = _bounceSound != null ? _bounceSound.DefaultSetup.Volume : 0f;
		}

		// PRIVATE METHODS

		private Vector3 GetMovePosition(ref KinematicData data, float currentTick, float deltaTime)
		{
			int   startTick = data.Advanced.MoveStartTick > 0 ? data.Advanced.MoveStartTick : data.FireTick;
			float time      = (currentTick - startTick) * deltaTime;

			if (time <= 0f)
				return data.Position;

			// No gravity — straight-line ricochet.
			return data.Position + (Vector3)data.Velocity * time;
		}

		private void TryHealAlly(IHitTarget target, LagCompensatedHit hit, Vector3 direction)
		{
			var targetHealth = target as Health;
			if (targetHealth == null)
				return;

			var runner = Context.Runner;

			var instigatorPlayerObject = runner.GetPlayerObject(Context.Owner);
			var instigatorPlayer       = instigatorPlayerObject != null ? instigatorPlayerObject.GetComponent<Player>() : null;
			var instigatorTeam         = instigatorPlayer != null ? instigatorPlayer.Team : ETeam.None;

			var targetPlayerObject = runner.GetPlayerObject(targetHealth.Object.InputAuthority);
			var targetPlayer       = targetPlayerObject != null ? targetPlayerObject.GetComponent<Player>() : null;
			var targetTeam         = targetPlayer != null ? targetPlayer.Team : ETeam.None;

			bool isSelf = Context.Owner == targetHealth.Object.InputAuthority;
			bool isAlly = isSelf || (instigatorTeam != ETeam.None && targetTeam != ETeam.None && instigatorTeam == targetTeam);

			if (!isAlly)
				return;

			HitData healData = new HitData
			{
				Action        = EHitAction.Heal,
				Amount        = _healAmount,
				Position      = hit.Point,
				Normal        = hit.Normal,
				Direction     = direction,
				InstigatorRef = Context.Owner,
				Target        = target,
				HitType       = EHitType.Projectile,
			};

			HitUtility.ProcessHit(ref healData);
		}

		private void ProcessBounce(ref KinematicData data, LagCompensatedHit hit, Vector3 direction, float distance)
		{
			var runner             = Context.Runner;
			var reflectedDirection = Vector3.Reflect(direction, hit.Normal);

			// Stop when the projectile has slowed below the threshold.
			if (distance < _stopSpeed * runner.DeltaTime)
			{
				data.HasStopped             = true;
				data.Advanced.MoveStartTick = runner.Tick;
				data.ImpactPosition         = hit.Point + Vector3.Project(hit.Normal * _bounceObjectRadius, reflectedDirection);
				return;
			}

			float bounceMultiplier = _bounceVelocityMultiplierStart;

			if (_bounceVelocityMultiplierStart != _bounceVelocityMultiplierEnd)
			{
				bounceMultiplier = Mathf.Lerp(
					_bounceVelocityMultiplierStart,
					_bounceVelocityMultiplierEnd,
					data.Advanced.BounceCount / (float)_bounceVelocityScale);
			}

			float distanceToHit = Vector3.Distance(hit.Point, transform.position);
			float progressToHit = distanceToHit / distance;

			data.Position               = hit.Point + reflectedDirection * _bounceObjectRadius;
			data.Velocity               = reflectedDirection * ((Vector3)data.Velocity).magnitude * bounceMultiplier;
			data.Advanced.MoveStartTick = progressToHit > 0.5f ? runner.Tick : runner.Tick - 1;
			data.Advanced.BounceCount++;
		}

		private void OnBounceRender(ref KinematicData data)
		{
			if (_bounceSound == null)
				return;

			var soundSetup = _bounceSound.DefaultSetup;
			soundSetup.Volume = Mathf.Lerp(0f, _maxBounceVolume, ((Vector3)data.Velocity).magnitude / 10f);
			_bounceSound.Play(soundSetup, EForceBehaviour.ForceAny);
		}
	}
}
