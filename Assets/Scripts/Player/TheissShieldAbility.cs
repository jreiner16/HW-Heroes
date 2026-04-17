using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Projectiles
{
	/// <summary>
	/// Theiss's right-click ability: hold to preview shield placement, release to lock it in place.
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Theiss Shield Ability")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(5)]
	public class TheissShieldAbility : AbilityBase
	{
		public override EAbilitySlot Slot => EAbilitySlot.RightClick;

		[Header("Shield Settings")]
		[SerializeField]
		private float _spawnDistance = 2.5f;
		[SerializeField]
		private float _spawnHeightOffset = 0f;
		[SerializeField, Range(0f, 1f)]
		private float _previewAlpha = 0.35f;

		[Header("References")]
		[SerializeField]
		private TheissShieldWall _shieldPrefab;
		[SerializeField]
		private GameObject _shieldPreviewPrefab;

		[Networked]
		private NetworkId _activeShieldId { get; set; }

		private GameObject _previewInstance;

		public override void Spawned()
		{
			if (HasInputAuthority == false)
				return;

			if (_shieldPreviewPrefab != null)
			{
				_previewInstance = Instantiate(_shieldPreviewPrefab);
				_previewInstance.SetActive(false);
				ApplyPreviewMaterials(_previewInstance);
			}
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			if (_previewInstance != null)
			{
				Destroy(_previewInstance);
				_previewInstance = null;
			}
		}

		public override void FixedUpdateNetwork()
		{
			if (!ValidateCanAct())
				return;

			if (GetInput(out GameplayInput input) == false)
				return;

			bool abilityPressed = input.Buttons.WasPressed(_agent.Input.PreviousButtons, EInputButton.Ability);
			if (abilityPressed && HasStateAuthority)
			{
				var id = _activeShieldId;
				DespawnActiveObjectIfAny(ref id);
				_activeShieldId = id;
			}

			bool abilityReleased = _agent.Input.PreviousButtons.IsSet(EInputButton.Ability)
			                    && input.Buttons.IsSet(EInputButton.Ability) == false;

			if (abilityReleased)
			{
				TryCastShield();
			}
		}

		public override void Render()
		{
			UpdatePreview();
		}

		private void UpdatePreview()
		{
			if (_previewInstance == null || HasInputAuthority == false)
				return;

			var mouse = Mouse.current;
			bool isHolding = mouse != null
			              && mouse.rightButton.isPressed
			              && IsReady
			              && _agent != null
			              && _agent.Health.IsAlive;

			_previewInstance.SetActive(isHolding);

			if (isHolding == false)
				return;

			var forward = GetHorizontalAimForward();
			var previewPos = transform.position + forward * _spawnDistance + Vector3.up * _spawnHeightOffset;
			var previewRot = Quaternion.LookRotation(forward, Vector3.up);
			_previewInstance.transform.SetPositionAndRotation(previewPos, previewRot);
		}

		private void ApplyPreviewMaterials(GameObject previewObj)
		{
			foreach (var rend in previewObj.GetComponentsInChildren<Renderer>(true))
			{
				var mats = rend.materials;
				for (int i = 0; i < mats.Length; i++)
				{
					var mat = new Material(mats[i]);
					SetMaterialTransparent(mat, _previewAlpha);
					mats[i] = mat;
				}
				rend.materials = mats;
			}
		}

		private static void SetMaterialTransparent(Material mat, float alpha)
		{
			mat.SetFloat("_Surface", 1f);
			mat.SetFloat("_Blend", 0f);
			mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
			mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			mat.SetInt("_ZWrite", 0);
			mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
			mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

			var color = mat.color;
			color.a = alpha;
			mat.color = color;
		}

		private void TryCastShield()
		{
			if (IsOnCooldown || HasStateAuthority == false)
				return;
			if (_shieldPrefab == null || _agent.Weapons?.FireTransform == null)
				return;

			var id = _activeShieldId;
			DespawnActiveObjectIfAny(ref id);
			_activeShieldId = id;

			var forward = GetHorizontalAimForward();
			var spawnPosition = transform.position + forward * _spawnDistance + Vector3.up * _spawnHeightOffset;
			var spawnRotation = Quaternion.LookRotation(forward, Vector3.up);

			var shield = Runner.Spawn(_shieldPrefab, spawnPosition, spawnRotation, Object.InputAuthority);
			if (shield != null)
			{
				shield.Initialize(_agent.Owner);
				_activeShieldId = shield.Object.Id;
			}

			StartCooldown();
		}
	}
}
