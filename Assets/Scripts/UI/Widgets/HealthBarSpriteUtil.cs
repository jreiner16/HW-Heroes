using UnityEngine;

namespace Projectiles.UI
{
	/// <summary>
	/// Unity's built-in UI sprites often return null in newer versions — Images then draw nothing.
	/// </summary>
	public static class HealthBarSpriteUtil
	{
		private static Sprite _white;

		public static Sprite WhiteSprite
		{
			get
			{
				if (_white != null)
					return _white;

				var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
				{
					filterMode = FilterMode.Bilinear,
					wrapMode = TextureWrapMode.Clamp,
				};
				var px = new Color[16];
				for (int i = 0; i < px.Length; i++)
					px[i] = Color.white;
				tex.SetPixels(px);
				tex.Apply(false, true);

				_white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
				_white.name = "HB_WhiteSprite";
				return _white;
			}
		}
	}
}
