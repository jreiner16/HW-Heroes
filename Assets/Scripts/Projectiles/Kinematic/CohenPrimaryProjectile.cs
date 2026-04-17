	using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Cohen's primary projectile: travels in a straight line and stops on the first target
	/// it hits. Spawns an impact object (CohenExplosion) at the hit point.
	/// Does no direct damage — all damage and healing is handled by the explosion.
	/// Uses a wide circle cast (multiple rays in a ring) to simulate a large hitbox,
	/// matching the ink-blob design spec ("large hitbox").
	/// </summary>
	public class CohenPrimaryProjectile : KinematicProjectile
	{
		[SerializeField]
		private LayerMask _hitMask;
		[SerializeField, Tooltip("Radius of the circle cast — gives the projectile a large hitbox matching the ink blob visual.")]
		private float _castRadius = 1.5f;
		[SerializeField, Tooltip("Number of rays fired around the cast radius (1 center + N perimeter). More rays = more reliable hit detection.")]
		private int _castRays = 7;

		// KinematicProjectile INTERFACE

		public override void OnFixedUpdate(ref KinematicData data)
		{
			var runner = Context.Runner;

			var previousPosition = GetMovePosition(runner, ref data, runner.Tick - 1);
			var nextPosition     = GetMovePosition(runner, ref data, runner.Tick);

			var direction = nextPosition - previousPosition;
			float distance = direction.magnitude;

			if (distance <= 0f)
				return;

			direction /= distance;

			AdjustForProjectileLength(ref previousPosition, ref distance, direction, data.Position);

			if (ProjectileUtility.CircleCast(runner, Context.Owner, previousPosition, direction, distance, _castRadius, _castRays, _hitMask, out LagCompensatedHit hit) == true)
			{
				data.ImpactPosition = hit.Point;
				data.ImpactNormal   = (hit.Normal - direction) * 0.5f;
				data.IsFinished     = true;

				// No direct damage — spawn the explosion which handles all hit effects.
				SpawnImpact(data.ImpactPosition, data.ImpactNormal);
			}

			base.OnFixedUpdate(ref data);
		}

		protected override Vector3 GetRenderPosition(ref KinematicData data, ref KinematicData fromData, float alpha)
		{
			var runner     = Context.Runner;
			float renderTime = Context.Owner == runner.LocalPlayer ? runner.LocalRenderTime : runner.RemoteRenderTime;
			return GetMovePosition(runner, ref data, renderTime / runner.DeltaTime);
		}

		// PRIVATE METHODS

		private Vector3 GetMovePosition(NetworkRunner runner, ref KinematicData data, float currentTick)
		{
			float time = (currentTick - data.FireTick) * runner.DeltaTime;

			if (time <= 0f)
				return data.Position;

			return data.Position + (Vector3)data.Velocity * time;
		}
	}
}
