using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Theiss's ultimate ability: places a debuff field that reduces enemy outgoing damage.
	/// Triggered by the X key.
	/// </summary>
	[DefaultExecutionOrder(5)]
	public class TheissUltimateAbility : AbilityBase, IUltimateAbility
	{
		public override EAbilitySlot Slot => EAbilitySlot.Ultimate;

		[Header("Ultimate Settings")]
		[SerializeField, Tooltip("How many seconds of ultimate cooldown are removed per 1 damage dealt.")]
		private float _cooldownSecondsPerDamage = 0.05f;
		[SerializeField]
		private float _spawnDistance = 2.5f;
		[SerializeField]
		private float _spawnHeightOffset = 0f;

		[Header("References")]
		[SerializeField]
		private TheissDamageDebuffField _fieldPrefab;

		[Networked]
		private NetworkId _activeFieldId { get; set; }

		public override void FixedUpdateNetwork()
		{
			if (!ValidateCanAct())
				return;

			if (GetInput(out GameplayInput input) == false)
				return;

			if (input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.X))
			{
				TryCastField();
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

		private void TryCastField()
		{
			if (IsOnCooldown || HasStateAuthority == false)
				return;

			if (_fieldPrefab == null || _agent.Weapons?.FireTransform == null)
				return;

			var id = _activeFieldId;
			DespawnActiveObjectIfAny(ref id);
			_activeFieldId = id;

			var forward = GetHorizontalAimForward();
			var spawnPosition = transform.position + forward * _spawnDistance + Vector3.up * _spawnHeightOffset;

			var field = Runner.Spawn(_fieldPrefab, spawnPosition, Quaternion.identity, Object.InputAuthority);
			if (field != null)
			{
				field.Initialize(_agent.Owner);
				_activeFieldId = field.Object.Id;
			}

			StartCooldown();
		}
	}
}
