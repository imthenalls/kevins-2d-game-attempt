using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor convenience for 3D planar-isometric scenes: snaps the Scene view camera to the same
/// isometric angle the game uses (pitch 30°, yaw 45°), frames all rendered content, and hides
/// gizmos so camera-frustum wireframes do not clutter the view.
///
/// Unity setup: menu Tools &gt; Worlds &gt; Isometric 3D &gt; Focus Scene View (or use the
/// <c>Ctrl</c> shortcut shown on the menu item). The Town 3D builder also calls this after it
/// rebuilds the scene.
///
/// Runtime API: none (editor only).
/// </summary>
public static class Isometric3DSceneView
{
    private const float Pitch = 30f;
    private const float Yaw = 45f;

    [MenuItem("Tools/Worlds/Isometric 3D/Focus Scene View")]
    public static void Focus()
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null)
        {
            Debug.LogWarning("[Isometric3D] No Scene view is open; open one and try again.");
            return;
        }

        Bounds bounds = TryGetContentBounds(out Bounds content)
            ? content
            : new Bounds(new Vector3(32f, 0f, 22f), new Vector3(66f, 4f, 46f));

        view.in2DMode = false;
        view.orthographic = true;
        view.drawGizmos = false;
        view.LookAt(
            bounds.center,
            Quaternion.Euler(Pitch, Yaw, 0f),
            Mathf.Max(1f, bounds.extents.magnitude * 1.05f),
            ortho: true);
        view.Repaint();
    }

    private static bool TryGetContentBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasContent = false;

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (!hasContent)
            {
                bounds = renderer.bounds;
                hasContent = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasContent;
    }
}
