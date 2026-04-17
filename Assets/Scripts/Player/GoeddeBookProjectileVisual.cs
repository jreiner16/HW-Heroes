using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Attached to PapercutShot: hides the default GrenadeYellow particle visual and
	/// replaces it with a tumbling book-shaped cube for Goedde's right-click throw.
	/// </summary>
	[DefaultExecutionOrder(-1)]
	public class GoeddeBookProjectileVisual : MonoBehaviour
	{
		[SerializeField] private Color _bookColor    = new Color(0.30f, 0.10f, 0.06f, 1f);
		[SerializeField] private float _tumbleSpeed  = 540f;

		private GameObject _bookGo;
		private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

		private void Awake()
		{
			// Hide existing grenade visuals (particle systems and renderers on children)
			foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
				ps.gameObject.SetActive(false);

			foreach (var rend in GetComponentsInChildren<Renderer>(true))
				rend.enabled = false;

			// Build a flat book-shaped cube
			_bookGo      = GameObject.CreatePrimitive(PrimitiveType.Cube);
			_bookGo.name = "GoeddeBookVisual";

			var col = _bookGo.GetComponent<Collider>();
			if (col != null) Destroy(col);

			_bookGo.transform.SetParent(transform, false);
			_bookGo.transform.localPosition = Vector3.zero;
			_bookGo.transform.localRotation = Quaternion.identity;
			_bookGo.transform.localScale    = new Vector3(0.20f, 0.28f, 0.04f);

			var shader = Shader.Find("Universal Render Pipeline/Lit");
			if (shader == null) shader = Shader.Find("Standard");
			var mat = new Material(shader);
			mat.color = _bookColor;
			mat.EnableKeyword("_EMISSION");
			mat.SetColor(EmissionColor, _bookColor * 0.4f);

			var bookRend = _bookGo.GetComponent<Renderer>();
			bookRend.sharedMaterial    = mat;
			bookRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		}

		private void Update()
		{
			if (_bookGo != null)
				_bookGo.transform.Rotate(Vector3.right, _tumbleSpeed * Time.deltaTime, Space.Self);
		}

		private void OnDestroy()
		{
			if (_bookGo != null)
				Destroy(_bookGo);
		}
	}
}
