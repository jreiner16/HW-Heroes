using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// A ground-based flamethrower area spawned by Goedde's secondary ability at a targeted floor point.
	/// Each pulse it fires one or more <see cref="StandaloneProjectile"/> flamethrower projectiles
	/// upward from the floor with configurable spread, reusing the existing flamethrower projectile
	/// prefab (FlamethrowerProjectile) for both damage and visuals.
	/// Automatically despawns after <see cref="_duration"/> seconds.
	/// </summary>
	[AddComponentMenu("Projectiles/Goedde Flamethrower Area")]
	public class GoeddeFlamethrowerArea : ContextBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField, Tooltip("How long the ground flamethrower stays active.")]
		private float _duration = 3f;
		[SerializeField, Tooltip("The existing FlamethrowerProjectile standalone prefab to fire each pulse.")]
		private StandaloneProjectile _flamethrowerProjectilePrefab;
		[SerializeField, Tooltip("Number of projectiles fired per pulse.")]
		private int _projectilesPerPulse = 2;
		[SerializeField, Tooltip("How many pulses per second.")]
		private int _pulsesPerSecond = 8;
		[SerializeField, Tooltip("Dispersion angle in degrees applied to each upward-fired projectile (spread of the flame cone).")]
		private float _dispersion = 25f;
		[SerializeField, Tooltip("Vertical offset so projectiles spawn just above the floor surface.")]
		private float _spawnHeightOffset = 0.1f;

		[Networked]
		private TickTimer _pulseTimer { get; set; }
		[Networked]
		private TickTimer _despawnTimer { get; set; }

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			if (HasStateAuthority == false)
				return;

			float pulseInterval = 1f / Mathf.Max(1, _pulsesPerSecond);
			_pulseTimer  = TickTimer.CreateFromSeconds(Runner, pulseInterval);
			_despawnTimer = TickTimer.CreateFromSeconds(Runner, _duration);
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

			if (_pulseTimer.ExpiredOrNotRunning(Runner))
			{
				FirePulse();
				float pulseInterval = 1f / Mathf.Max(1, _pulsesPerSecond);
				_pulseTimer = TickTimer.CreateFromSeconds(Runner, pulseInterval);
			}
		}

		// PRIVATE METHODS

		private void FirePulse()
		{
			if (_flamethrowerProjectilePrefab == null)
				return;

			var spawnOrigin = transform.position + Vector3.up * _spawnHeightOffset;

			for (int i = 0; i < _projectilesPerPulse; i++)
			{
				// Apply random spread around straight up so the flames fan outward.
				Random.InitState(Runner.Tick * 397 + i * 31 + unchecked((int)Object.Id.Raw));
				var spread    = Random.insideUnitSphere * _dispersion;
				var direction = Quaternion.Euler(spread.x, spread.y, spread.z) * Vector3.up;

				var projectile = Runner.Spawn(
					_flamethrowerProjectilePrefab,
					spawnOrigin,
					Quaternion.LookRotation(direction),
					Object.InputAuthority);

				projectile.Fire(spawnOrigin, direction);
			}
		}
	}
}
