using UnityEngine;
using UnityEngine.UI;

namespace Projectiles.UI
{
	public static class UIUtility
	{
		public static Image AddBackgroundPanel(RectTransform parent, Color color, float padding = 8f)
		{
			var bg = new GameObject("Background").AddComponent<Image>();
			bg.transform.SetParent(parent, false);
			bg.transform.SetAsFirstSibling();
			bg.rectTransform.anchorMin = Vector2.zero;
			bg.rectTransform.anchorMax = Vector2.one;
			bg.rectTransform.offsetMin = new Vector2(-padding, -padding);
			bg.rectTransform.offsetMax = new Vector2(padding, padding);
			bg.color = color;
			bg.raycastTarget = false;
			return bg;
		}
	}
}
