using Fusion;
using UnityEngine;
using UnityEngine.Rendering;

namespace Projectiles
{
	/// <summary>
	/// Component handling all visual/hierarchy related tasks and effects (immortality, death).
	/// </summary>
	public class PlayerBody : ContextBehaviour
	{
		// PRIVATE MEMBERS

		[SerializeField]
		private GameObject _root;
		[SerializeField]
		private GameObject _visual;
		[SerializeField]
		private GameObject _immortalityEffect;
		[SerializeField]
		private Transform _capTransform;
		[SerializeField]
		private Rigidbody _flyingCapPrefab;
		[SerializeField]
		private float _capImpulse = 10f;
		[SerializeField]
		private GameObject _deathEffectPrefab;

		[Header("Team Outlines")]
		[SerializeField]
		private bool _enableTeamOutlines = true;
		[SerializeField]
		private float _outlineWidth = 0.02f;
		[SerializeField]
		private Color _allyOutlineColor = new Color(0f, 0.63f, 1f, 1f);
		[SerializeField]
		private Color _enemyOutlineColor = new Color(1f, 0.1f, 0.1f, 1f);

		private PlayerAgent _agent;
		private HitboxRoot _hitboxRoot;

		private SkinnedMeshRenderer[] _sourceSkinnedRenderers;
		private readonly System.Collections.Generic.List<SkinnedMeshRenderer> _outlineRenderers = new();
		private Material _outlineMaterial;
		private MaterialPropertyBlock _outlineBlock;

		// ContextBehaviour INTERFACE

		public override void Spawned()
		{
			_root.SetActive(_agent.Health.IsAlive);
			_agent.Health.FatalHitTaken += OnFatalHit;

			// Disable visual for local player
			var renderers = _visual.GetComponentsInChildren<MeshRenderer>();
			for (int i = 0; i < renderers.Length; i++)
			{
				renderers[i].shadowCastingMode = HasInputAuthority ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
			}

			if (_enableTeamOutlines == true)
			{
				SetupTeamOutlineRenderers();
			}
		}

		public override void FixedUpdateNetwork()
		{
			// Disable hitbox detection when agent is dead
			_hitboxRoot.HitboxRootActive = _agent.Health.IsAlive;
		}

		public override void Render()
		{
			_immortalityEffect.SetActive(_agent.Health.IsImmortal);

			if (_enableTeamOutlines == true)
			{
				UpdateTeamOutline();
			}
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			_agent.Health.FatalHitTaken -= OnFatalHit;
		}

		// MONOBEHAVIOUR

		protected void Awake()
		{
			_agent = GetComponent<PlayerAgent>();
			_hitboxRoot = GetComponent<HitboxRoot>();
		}

		// PRIVATE METHODS

		private void SetupTeamOutlineRenderers()
		{
			if (_visual == null)
				return;

			_sourceSkinnedRenderers = _visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
			if (_sourceSkinnedRenderers == null || _sourceSkinnedRenderers.Length == 0)
				return;

			_outlineBlock ??= new MaterialPropertyBlock();

			// Create (or reuse) a shared outline material.
			if (_outlineMaterial == null)
			{
				var shader = Shader.Find("Projectiles/TeamOutline");
				if (shader == null)
				{
					Debug.LogError("Team outline shader not found. Ensure `Assets/Shaders/TeamOutline.shader` exists.");
					return;
				}

				_outlineMaterial = new Material(shader)
				{
					name = "M_TeamOutline (Runtime)",
					hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
				};
			}

			// Duplicate each skinned mesh renderer so we can render an extruded silhouette.
			for (int i = 0; i < _sourceSkinnedRenderers.Length; i++)
			{
				var source = _sourceSkinnedRenderers[i];
				if (source == null || source.sharedMesh == null)
					continue;

				// Avoid duplicating our own outlines (in case of re-setup).
				if (source.gameObject.name.Contains("TeamOutline") == true)
					continue;

				var outlineGO = new GameObject($"{source.gameObject.name}_TeamOutline");
				outlineGO.layer = source.gameObject.layer;
				outlineGO.transform.SetParent(source.transform, false);

				var outline = outlineGO.AddComponent<SkinnedMeshRenderer>();
				outline.sharedMesh = source.sharedMesh;
				outline.bones = source.bones;
				outline.rootBone = source.rootBone;
				outline.quality = source.quality;
				outline.updateWhenOffscreen = source.updateWhenOffscreen;

				outline.sharedMaterial = _outlineMaterial;
				outline.shadowCastingMode = ShadowCastingMode.Off;
				outline.receiveShadows = false;
				outline.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

				_outlineRenderers.Add(outline);
			}
		}

		private void UpdateTeamOutline()
		{
			// Don’t outline your own body.
			if (HasInputAuthority == true)
			{
				SetOutlineEnabled(false);
				return;
			}

			if (_outlineRenderers.Count == 0)
				return;

			// Need local player + both teams to decide ally/enemy colors.
			var localPlayer = Context != null && Context.Gameplay != null ? Context.Gameplay.GetLocalPlayer() : null;
			if (localPlayer == null || _agent == null || _agent.Owner == null)
			{
				SetOutlineEnabled(false);
				return;
			}

			var localTeam = localPlayer.Team;
			var targetTeam = _agent.Owner.Team;

			if (localTeam == ETeam.None || targetTeam == ETeam.None)
			{
				SetOutlineEnabled(false);
				return;
			}

			bool isAlly = localTeam == targetTeam;
			Color outlineColor = isAlly ? _allyOutlineColor : _enemyOutlineColor;

			_outlineBlock.Clear();
			_outlineBlock.SetColor(TeamOutlineIds.OutlineColor, outlineColor);
			_outlineBlock.SetFloat(TeamOutlineIds.OutlineWidth, _outlineWidth);

			SetOutlineEnabled(true, _outlineBlock);
		}

		private void SetOutlineEnabled(bool enabled, MaterialPropertyBlock block = null)
		{
			for (int i = 0; i < _outlineRenderers.Count; i++)
			{
				var r = _outlineRenderers[i];
				if (r == null)
					continue;

				r.enabled = enabled;
				if (enabled == true && block != null)
				{
					r.SetPropertyBlock(block);
				}
			}
		}

		private void OnFatalHit(HitData hit)
		{
			_agent.KCC.SetActive(false);
			_root.SetActive(false);

			var deathEffect = Runner.InstantiateInRunnerScene(_deathEffectPrefab);
			deathEffect.transform.position = transform.position + Vector3.up;

			var flyingCap = Runner.InstantiateInRunnerScene(_flyingCapPrefab);
			flyingCap.transform.SetPositionAndRotation(_capTransform.position, _capTransform.rotation);

			var direction = (hit.Direction + 2f * Vector3.up).normalized;
			flyingCap.AddForceAtPosition(direction * _capImpulse, flyingCap.transform.position - hit.Direction * 0.2f, ForceMode.Impulse);

			if (Runner.Config.PeerMode == NetworkProjectConfig.PeerModes.Multiple)
			{
				Runner.AddVisibilityNodes(flyingCap.gameObject);
				Runner.AddVisibilityNodes(deathEffect.gameObject);
			}
		}
	}
}
