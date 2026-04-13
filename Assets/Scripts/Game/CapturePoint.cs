using Fusion;
using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Point capture objective. Players stand inside the zone to earn points for their team.
	/// Majority team gets points; ties award nothing. First team to the target score wins.
	/// Uses position check (no physics) for reliable detection.
	/// </summary>
	[RequireComponent(typeof(Collider))]
	public class CapturePoint : ContextBehaviour
	{
		// PUBLIC MEMBERS

		[Tooltip("Points awarded per tick to the controlling team (majority inside zone).")]
		[SerializeField]
		private int _pointsPerTick = 1;

		[Tooltip("How often to award points (seconds).")]
		[SerializeField]
		private float _tickInterval = 0.5f;

		[Tooltip("Visual feedback: optional renderer to tint based on controlling team.")]
		[SerializeField]
		private Renderer _zoneRenderer;

		[Tooltip("Team 1 color when controlling.")]
		[SerializeField]
		private Color _team1Color = new Color(0.2f, 0.5f, 1f);

		[Tooltip("Team 2 color when controlling.")]
		[SerializeField]
		private Color _team2Color = new Color(1f, 0.3f, 0.2f);

		[Tooltip("Neutral color when contested or empty.")]
		[SerializeField]
		private Color _neutralColor = new Color(0.5f, 0.5f, 0.5f);

		[Header("Beam VFX")]
		[SerializeField]
		private float _beamHeight = 20f;
		[SerializeField]
		private float _beamWidth = 0.4f;
		[SerializeField]
		private float _pulseSpeed = 2f;
		[SerializeField]
		private float _pulseMinAlpha = 0.3f;
		[SerializeField]
		private float _pulseMaxAlpha = 0.7f;

		[Header("Zone Light")]
		[SerializeField]
		private float _lightIntensity = 3f;
		[SerializeField]
		private float _lightRange = 12f;
		[SerializeField]
		private float _lightHeight = 4f;

		// PRIVATE MEMBERS

		private TickTimer _nextTick;
		private ETeam _lastControllingTeam = ETeam.None;
		private MaterialPropertyBlock _propBlock;
		private Collider _zoneCollider;
		private LineRenderer _beamRenderer;
		private Light _zoneLight;
		private Color _currentBeamColor;

		// NetworkBehaviour INTERFACE

		public override void Spawned()
		{
			_nextTick = TickTimer.CreateFromSeconds(Runner, _tickInterval);
			_propBlock = new MaterialPropertyBlock();
			_zoneCollider = GetComponent<Collider>();
			if (_zoneCollider != null && _zoneCollider.isTrigger == false)
				_zoneCollider.isTrigger = true;

			CreateBeam();
			CreateZoneLight();
		}

		public override void FixedUpdateNetwork()
		{
			var gameplay = Context?.Gameplay;
			if (gameplay == null)
				return;

			int team1 = 0;
			int team2 = 0;

			foreach (var kvp in gameplay.Players)
			{
				var player = kvp.Value;
				if (player == null || player.ActiveAgent == null || player.ActiveAgent.Object == null)
					continue;
				if (player.ActiveAgent.Health == null || player.ActiveAgent.Health.IsAlive == false)
					continue;

				Vector3 pos = player.ActiveAgent.transform.position;
				if (IsInsideZone(pos) == false)
					continue;

				if (player.Team == ETeam.Team1) team1++;
				else if (player.Team == ETeam.Team2) team2++;
			}

			ETeam controllingTeam = ETeam.None;
			if (team1 > team2) controllingTeam = ETeam.Team1;
			else if (team2 > team1) controllingTeam = ETeam.Team2;

			if (HasStateAuthority)
			{
				if (_nextTick.ExpiredOrNotRunning(Runner))
				{
					_nextTick = TickTimer.CreateFromSeconds(Runner, _tickInterval);
					if (controllingTeam != ETeam.None && _pointsPerTick > 0)
						gameplay.AddCapturePoints(controllingTeam, _pointsPerTick);
				}
			}

			UpdateZoneColor(controllingTeam);
		}

		private bool IsInsideZone(Vector3 worldPos)
		{
			if (_zoneCollider == null)
				return Vector3.Distance(transform.position, worldPos) < 3f;
			return _zoneCollider.bounds.Contains(worldPos);
		}

		public override void Render()
		{
			if (_beamRenderer != null)
			{
				float pulse = Mathf.Lerp(_pulseMinAlpha, _pulseMaxAlpha,
					(Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f);
				Color c = _currentBeamColor;
				c.a = pulse;
				_beamRenderer.startColor = c;
				c.a = pulse * 0.3f;
				_beamRenderer.endColor = c;
			}
		}

		private void UpdateZoneColor(ETeam controllingTeam)
		{
			if (controllingTeam == _lastControllingTeam) return;
			_lastControllingTeam = controllingTeam;

			Color c = controllingTeam == ETeam.Team1 ? _team1Color
				: controllingTeam == ETeam.Team2 ? _team2Color
				: _neutralColor;

			_currentBeamColor = c;

			if (_zoneRenderer != null)
			{
				_propBlock.SetColor("_BaseColor", c);
				_propBlock.SetColor("_Color", c);
				_zoneRenderer.SetPropertyBlock(_propBlock);
			}

			if (_zoneLight != null)
			{
				_zoneLight.color = c;
			}
		}

		private void CreateBeam()
		{
			var beamGo = new GameObject("CaptureBeam");
			beamGo.transform.SetParent(transform, false);
			beamGo.transform.localPosition = Vector3.zero;

			_beamRenderer = beamGo.AddComponent<LineRenderer>();
			_beamRenderer.useWorldSpace = false;
			_beamRenderer.positionCount = 2;
			_beamRenderer.SetPosition(0, Vector3.zero);
			_beamRenderer.SetPosition(1, Vector3.up * _beamHeight);
			_beamRenderer.startWidth = _beamWidth;
			_beamRenderer.endWidth = _beamWidth * 0.1f;
			_beamRenderer.material = new Material(Shader.Find("Sprites/Default"));
			_beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			_beamRenderer.receiveShadows = false;

			_currentBeamColor = _neutralColor;
			Color startC = _neutralColor;
			startC.a = _pulseMaxAlpha;
			Color endC = _neutralColor;
			endC.a = _pulseMaxAlpha * 0.3f;
			_beamRenderer.startColor = startC;
			_beamRenderer.endColor = endC;
		}

		private void CreateZoneLight()
		{
			var lightGo = new GameObject("CaptureLight");
			lightGo.transform.SetParent(transform, false);
			lightGo.transform.localPosition = Vector3.up * _lightHeight;

			_zoneLight = lightGo.AddComponent<Light>();
			_zoneLight.type = LightType.Point;
			_zoneLight.color = _neutralColor;
			_zoneLight.intensity = _lightIntensity;
			_zoneLight.range = _lightRange;
			_zoneLight.shadows = LightShadows.None;
		}
	}
}
