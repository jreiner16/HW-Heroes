using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Per-character UI blurb shown on the HUD.
	/// Attach this to a character agent prefab and fill in the text in the inspector.
	/// </summary>
	public sealed class CharacterBlurb : MonoBehaviour
	{
		[SerializeField, TextArea(2, 6)]
		private string _blurb;

		public string Blurb => _blurb;
	}
}

