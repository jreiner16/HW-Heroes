using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Projectiles
{
	/// <summary>
	/// Theiss right-click ability. Hold RMB to preview shield placement, release to lock it in place.
	/// </summary>
	[AddComponentMenu("Projectiles/Abilities/Theiss Shield Ability")]
	[DisallowMultipleComponent]
	[DefaultExecutionOrder(5)]
	public class TheissShieldAbility : ContextBehaviour
	{
		// PUBLIC MEMBERS

		public bool IsOnCooldown => _cooldownTimer.ExpiredOrNotRunning(Runner) == false;
		public bool IsReady      => _cooldownTimer.ExpiredOrNotRunning(Runner);
		public float CooldownRemainingTime => _cooldownTimer.RemainingTime(Runner).GetValueOrDefault();
		public float CooldownTotal => _cooldown;

		// PRIVATE MEMBERS

		[Header("Ability Settings")]
		[SerializeField]
		private float _cooldown = 12f;
		[SerializeField]
		private float _spawnDistance = 2.5f;
		[SerializeField]
		private float _spawnHeightOffset = 0f;
		[SerializeField]
		[Range(0f, 1f)]
		private float _previewAlpha = 0.35f;

		[Header("References")]
		[SerializeField]
		private TheissShieldWall _shieldPrefab;
		/// <summary>
		/// Optional: assign a prefab to use as the placement preview. If left empty, the ability
		/// will attempt to build a ghost from the shield prefab's renderers at runtime.
		/// Recommended: duplicate the shield visual mesh prefab and assign a semi-transparent URP
		/// material to it, then assign that prefab here.
		/// </summary>
		[SerializeField]
		private GameObject _shieldPreviewPrefab;

		[Networked]
		private TickTimer _cooldownTimer { get; set; }
		[Networked]
		private NetworkId _activeShieldId { get; set; }

		private PlayerAgent _agent;
		private GameObject  _previewInstance;

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_agent = GetComponent<PlayerAgent>();
		}

		// NetworkBehaviour INTERFACE

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
			if (_agent == null || _agent.Owner == null || _agent.Health.IsAlive == false)
				return;

			if (GetInput(out GameplayInput input) == false)
				return;

			// Cast shield when RMB is released (hold to aim, release to place)
			bool altFireReleased = _agent.Input.PreviousButtons.IsSet(EInputButton.AltFire)
			                    && input.Buttons.IsSet(EInputButton.AltFire) == false;

			if (altFireReleased)
			{
				TryCastShield();
			}
		}

		public override void Render()
		{
			UpdatePreview();
		}

		// PRIVATE METHODS

		private void UpdatePreview()
		{
			if (_previewInstance == null)
				return;
			if (HasInputAuthority == false)
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

			var fireTransform = _agent.Weapons?.FireTransform;
			if (fireTransform == null)
				return;

			var forward = fireTransform.forward;
			forward.y = 0f;
			if (forward.sqrMagnitude < 0.0001f)
			{
				forward = transform.forward;
				forward.y = 0f;
			}
			forward.Normalize();

			var previewPos = transform.position + forward * _spawnDistance + Vector3.up * _spawnHeightOffset;
			var previewRot = Quaternion.LookRotation(forward, Vector3.up);
			_previewInstance.transform.SetPositionAndRotation(previewPos, previewRot);
		}

		/// <summary>
		/// Duplicates each material on every renderer in the preview object and forces
		/// URP Lit-compatible transparency at the configured alpha level.
		/// </summary>
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

		/// <summary>
		/// Patches a URP Lit (or compatible) material to render as alpha-blended transparent.
		/// </summary>
		private static void SetMaterialTransparent(Material mat, float alpha)
		{
			// URP Lit surface type: 0 = Opaque, 1 = Transparent
			mat.SetFloat("_Surface", 1f);
			// Blend mode: 0 = Alpha
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
			if (IsOnCooldown)
				return;
			if (_shieldPrefab == null)
				return;
			if (_agent.Weapons == null || _agent.Weapons.FireTransform == null)
				return;
			if (HasStateAuthority == false)
				return;

			DespawnActiveShieldIfAny();

			var fireTransform = _agent.Weapons.FireTransform;
			var forward = fireTransform.forward;
			forward.y = 0f;
			if (forward.sqrMagnitude < 0.0001f)
			{
				forward = transform.forward;
				forward.y = 0f;
			}
			forward.Normalize();

			var spawnPosition = transform.position + forward * _spawnDistance + Vector3.up * _spawnHeightOffset;
			var spawnRotation = Quaternion.LookRotation(forward, Vector3.up);

			var shield = Runner.Spawn(_shieldPrefab, spawnPosition, spawnRotation, Object.InputAuthority);
			if (shield != null)
			{
				shield.Initialize(_agent.Owner);
				_activeShieldId = shield.Object.Id;
			}

			_cooldownTimer = TickTimer.CreateFromSeconds(Runner, _cooldown);
		}

		private void DespawnActiveShieldIfAny()
		{
			if (_activeShieldId.IsValid == false)
				return;

			var existingObject = Runner.FindObject(_activeShieldId);
			if (existingObject != null)
			{
				Runner.Despawn(existingObject);
			}

			_activeShieldId = default;
		}
	}
}
