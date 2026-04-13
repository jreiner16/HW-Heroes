using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Cohen's ultimate ability: throws a LOS-blocking sphere.
	/// Allies heal inside; enemies take damage. Triggered by the X key.
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Cohen Ultimate Ability")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(5)]
	public class CohenUltimateAbility : AbilityBase, IUltimateAbility
	{
		public override EAbilitySlot Slot => EAbilitySlot.Ultimate;

		[Header("Ultimate Settings")]
		[SerializeField, Tooltip("How many seconds of ultimate cooldown are removed per 1 damage dealt.")]
		private float _cooldownSecondsPerDamage = 0.05f;
		[SerializeField] private float _spawnDistance = 4.0f;
		[SerializeField] private float _spawnHeightOffset = 0.5f;

		[Header("References")]
		[SerializeField] private CohenLoSSphere _spherePrefab;

		[Networked] private NetworkId _activeSphereId { get; set; }

		public override void FixedUpdateNetwork()
		{
			if (!ValidateCanAct())
				return;

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.X))
			{
				TryThrowSphere();
			}
		}

		public void AccelerateCooldownFromDamage(float damageDealt)
		{
			if (HasStateAuthority == false)
				return;
			if (_cooldownSecondsPerDamage <= 0f || damageDealt <= 0f)
				return;

			ReduceCooldownSeconds(damageDealt * _cooldownSecondsPerDamage);
		}

		private void TryThrowSphere()
		{
			if (IsOnCooldown || HasStateAuthority == false)
				return;

			if (_spherePrefab == null || _agent.Weapons?.FireTransform == null)
				return;

			var id = _activeSphereId;
			DespawnActiveObjectIfAny(ref id);
			_activeSphereId = id;

			var forward = GetHorizontalAimForward();
			var spawnPosition = transform.position + forward * _spawnDistance + Vector3.up * _spawnHeightOffset;

			var sphere = Runner.Spawn(_spherePrefab, spawnPosition, Quaternion.identity, Object.InputAuthority);
			if (sphere != null)
			{
				sphere.Initialize(_agent.Owner);
				_activeSphereId = sphere.Object.Id;
			}

			StartCooldown();
		}
	}
}
