namespace Projectiles
{
	public enum EAbilitySlot : byte
	{
		Movement,
		RightClick,
		Ultimate,
	}

	/// <summary>
	/// Read-only interface for querying any ability's state.
	/// Used by UI widgets and gameplay systems to avoid depending on concrete ability types.
	/// </summary>
	public interface IAbility
	{
		EAbilitySlot Slot { get; }
		bool IsActive { get; }
		bool IsOnCooldown { get; }
		bool IsReady { get; }
		float CooldownRemainingTime { get; }
		float CooldownTotal { get; }
		bool HasDuration { get; }
		float DurationRemainingTime { get; }
		float DurationTotal { get; }
	}

	/// <summary>
	/// Extension of IAbility for ultimate abilities that accelerate cooldown from damage dealt.
	/// </summary>
	public interface IUltimateAbility : IAbility
	{
		void AccelerateCooldownFromDamage(float damageDealt);
	}
}
