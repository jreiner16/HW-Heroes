using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Base class for all character abilities. Provides shared cooldown management,
	/// duration tracking, validation helpers, and common utility methods.
	/// Subclasses override abstract/virtual members for ability-specific behavior.
	/// </summary>
	[DefaultExecutionOrder(5)]
	public abstract class AbilityBase : ContextBehaviour, IAbility
	{
		// IAbility INTERFACE

		public abstract EAbilitySlot Slot { get; }
		public virtual bool IsActive => false;
		public bool IsOnCooldown => _cooldownTimer.ExpiredOrNotRunning(Runner) == false;
		public virtual bool IsReady => _cooldownTimer.ExpiredOrNotRunning(Runner);
		public float CooldownRemainingTime => _cooldownTimer.RemainingTime(Runner).GetValueOrDefault();
		public float CooldownTotal => _cooldown;
		public virtual bool HasDuration => false;
		public virtual float DurationRemainingTime => 0f;
		public virtual float DurationTotal => 0f;

		// SERIALIZED MEMBERS

		[Header("Cooldown")]
		[SerializeField]
		protected float _cooldown;

		// NETWORKED STATE

		[Networked]
		protected TickTimer _cooldownTimer { get; set; }

		// CACHED REFERENCES

		protected PlayerAgent _agent;

		// MONOBEHAVIOUR

		protected virtual void Awake()
		{
			_agent = GetComponent<PlayerAgent>();
		}

		// SHARED HELPERS

		protected void StartCooldown()
		{
			_cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);
		}

		protected bool ValidateCanAct()
		{
			return _agent != null && _agent.Owner != null && _agent.Health.IsAlive;
		}

		/// <summary>
		/// Reduces the remaining cooldown by the given number of seconds.
		/// Used by ultimate abilities to accelerate cooldown from damage dealt.
		/// </summary>
		protected void ReduceCooldownSeconds(float seconds)
		{
			if (seconds <= 0f)
				return;
			if (_cooldownTimer.ExpiredOrNotRunning(Runner))
				return;

			float remaining = _cooldownTimer.RemainingTime(Runner).GetValueOrDefault();
			float newRemaining = Mathf.Max(0f, remaining - seconds);
			_cooldownTimer = newRemaining > 0f ? TickTimer.CreateFromSeconds(Runner, newRemaining) : default;
		}

		/// <summary>
		/// Despawns a networked object tracked by NetworkId, then resets the id to default.
		/// Used by abilities that spawn shields, spheres, fields, etc.
		/// </summary>
		protected void DespawnActiveObjectIfAny(ref NetworkId id)
		{
			if (id.IsValid == false)
				return;

			var existingObject = Runner.FindObject(id);
			if (existingObject != null)
			{
				Runner.Despawn(existingObject);
			}

			id = default;
		}

		/// <summary>
		/// Gets the horizontal forward direction from the weapon aim, falling back to transform.forward.
		/// Used by abilities that spawn objects in front of the player.
		/// </summary>
		protected Vector3 GetHorizontalAimForward()
		{
			var forward = _agent.Weapons != null ? _agent.Weapons.AimDirection : transform.forward;
			forward.y = 0f;
			if (forward.sqrMagnitude < 0.0001f)
			{
				forward = transform.forward;
				forward.y = 0f;
			}
			forward.Normalize();
			return forward;
		}
	}
}
