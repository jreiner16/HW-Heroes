using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Cohen's primary projectile: travels in a straight line and stops on the first target
	/// it hits. Spawns an impact object (CohenExplosion) at the hit point.
	/// Does no direct damage — all damage and healing is handled by the explosion.
	/// </summary>
	public class CohenPrimaryProjectile : KinematicProjectile
	{
		[SerializeField]
		private LayerMask _hitMask;

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

			if (ProjectileUtility.ProjectileCast(runner, Context.Owner, previousPosition, direction, distance, _hitMask, out LagCompensatedHit hit) == true)
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
