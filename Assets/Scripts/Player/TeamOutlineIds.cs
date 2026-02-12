using UnityEngine;

namespace Projectiles
{
	/// <summary>
	/// Shader property IDs for team outlines.
	/// </summary>
	public static class TeamOutlineIds
	{
		public static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
		public static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
	}
}

