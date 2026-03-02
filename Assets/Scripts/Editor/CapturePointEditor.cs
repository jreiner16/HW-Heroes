using UnityEditor;
using UnityEngine;

namespace Projectiles.Editor
{
	public static class CapturePointEditor
	{
		[MenuItem("GameObject/HW Heroes/Capture Point", false, 10)]
		public static void CreateCapturePoint(MenuCommand menuCommand)
		{
			var go = new GameObject("CapturePoint");
			Undo.RegisterCreatedObjectUndo(go, "Create Capture Point");

			// Position at origin or in front of scene view
			var view = SceneView.lastActiveSceneView;
			if (view != null)
			{
				go.transform.position = view.pivot + view.rotation * Vector3.forward * 5f;
			}

			// Add required components
			var collider = go.AddComponent<BoxCollider>();
			collider.isTrigger = true;
			collider.size = new Vector3(4f, 4f, 4f);
			collider.center = new Vector3(0f, 2f, 0f);

			go.AddComponent<CapturePoint>();

			// Ensure NetworkObject exists (Fusion requires it for NetworkBehaviour)
			if (go.GetComponent<Fusion.NetworkObject>() == null)
			{
				go.AddComponent<Fusion.NetworkObject>();
			}

			Selection.activeGameObject = go;
		}
	}
}
