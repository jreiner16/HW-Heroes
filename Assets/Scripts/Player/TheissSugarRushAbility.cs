using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Theiss E-key ability: Sugar Rush — temporarily grants 20% damage reduction, +30% max HP
	/// as bonus shield health, and a 15% speed boost for 10 seconds. Press E again to cancel early.
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Theiss Sugar Rush Ability")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(-10)]
	public class TheissSugarRushAbility : AbilityBase
	{
		public override EAbilitySlot Slot               => EAbilitySlot.Movement;
		public override bool         IsActive           => _isActive;
		public override bool         IsReady            => base.IsReady && _isActive == false;
		public override bool         HasDuration        => true;
		public override float        DurationRemainingTime => _durationTimer.RemainingTime(Runner).GetValueOrDefault();
		public override float        DurationTotal      => _duration;

		[Header("Sugar Rush Settings")]
		[SerializeField] private float _duration = 10f;
		[SerializeField, Range(0f, 1f), Tooltip("Fraction of base max HP added as bonus shield HP on activation")]
		private float _shieldHpFraction = 0.30f;
		[SerializeField, Range(0f, 1f), Tooltip("Fraction of incoming damage reduced (0.20 = 20% less damage taken)")]
		private float _damageReduction = 0.20f;
		[SerializeField, Tooltip("Move speed multiplier while Sugar Rush is active")]
		private float _speedMultiplier = 1.15f;

		[Networked] private NetworkBool _isActive           { get; set; }
		[Networked] private TickTimer   _durationTimer      { get; set; }
		[Networked] private NetworkBool _shieldApplied      { get; set; }
		[Networked] private float       _appliedShieldAmount { get; set; }

		// Static registry so HitUtility can query incoming damage reduction without a direct reference.
		private static readonly List<TheissSugarRushAbility> _allInstances = new();

		// STATIC QUERY -------------------------------------------------------

		/// <summary>
		/// Returns the incoming damage multiplier for targetRef (1.0 = no reduction).
		/// Called by HitUtility before applying damage.
		/// </summary>
		public static float GetIncomingDamageMultiplier(NetworkRunner runner, PlayerRef targetRef)
		{
			if (runner == null || targetRef == default)
				return 1f;

			for (int i = 0; i < _allInstances.Count; i++)
			{
				var inst = _allInstances[i];
				if (inst == null || inst.Object == null)
					continue;
				if (inst.Runner != runner)
					continue;
				if (inst._isActive == false)
					continue;
				if (inst.Object.InputAuthority != targetRef)
					continue;

				return 1f - inst._damageReduction;
			}

			return 1f;
		}

		// NetworkBehaviour INTERFACE -----------------------------------------

		public override void Spawned()
		{
			base.Spawned();
			_allInstances.Add(this);
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			_allInstances.Remove(this);
			base.Despawned(runner, hasState);
		}

		// AbilityBase / NetworkBehaviour INTERFACE ---------------------------

		public override void FixedUpdateNetwork()
		{
			// Reset speed multiplier each tick; we re-apply below if active.
			if (_agent != null)
				_agent.MoveSpeedMultiplier = 1f;

			if (ValidateCanAct() == false)
			{
				if (_isActive && HasStateAuthority)
					ForceDeactivate();
				return;
			}

			// Natural expiry
			if (_isActive && _durationTimer.Expired(Runner))
			{
				Deactivate();
				return;
			}

			// Apply speed buff while active
			if (_isActive)
				_agent.MoveSpeedMultiplier = _speedMultiplier;

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.E))
			{
				if (_isActive)
					Deactivate();   // Press E again to cancel early
				else
					TryActivate();
			}
		}

		// PRIVATE METHODS ----------------------------------------------------

		private void TryActivate()
		{
			if (IsOnCooldown || HasStateAuthority == false)
				return;
			Activate();
		}

		private void Activate()
		{
			_isActive      = true;
			_durationTimer = TickTimer.CreateFromSeconds(Runner, _duration);
			ApplyShieldBuff();
		}

		private void Deactivate()
		{
			RemoveShieldBuff();
			_isActive = false;
			StartCooldown();
		}

		private void ForceDeactivate()
		{
			RemoveShieldBuff();
			_isActive      = false;
			_durationTimer = default;
			_cooldownTimer = default;
		}

		private void ApplyShieldBuff()
		{
			if (_shieldApplied || _agent?.Health == null)
				return;

			float shield         = _agent.Health.BaseMaxHealth * _shieldHpFraction;
			_appliedShieldAmount = shield;
			_agent.Health.ChangeMaxHealthBonus(shield);
			_agent.Health.Heal(shield);
			_shieldApplied = true;
		}

		private void RemoveShieldBuff()
		{
			if (_shieldApplied == false || _agent?.Health == null)
				return;

			_agent.Health.ChangeMaxHealthBonus(-_appliedShieldAmount);
			_shieldApplied       = false;
			_appliedShieldAmount = 0f;
		}
	}
}
