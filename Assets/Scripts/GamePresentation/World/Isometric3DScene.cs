using UnityEngine;

/// <summary>
/// Marks a scene as a 3D planar-isometric scene. <see cref="GameBootstrap"/> uses this to add an
/// <see cref="IsoCameraRig"/> instead of the flat 2D <see cref="CameraFollow"/>, and the data
/// validator uses it to skip 2D-only scene requirements (isometric Grid, walls tilemap, 2D player).
///
/// Unity setup:
///   1. Add to one root GameObject in the scene (next to WorldSceneIdentity).
///   2. Keep exactly one per scene.
///
/// Runtime API: none.
/// </summary>
[DisallowMultipleComponent]
public sealed class Isometric3DScene : MonoBehaviour
{
}
