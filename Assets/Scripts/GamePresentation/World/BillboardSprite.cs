using UnityEngine;

/// <summary>
/// Turns a sprite (character, weapon, marker) to face the 3D isometric camera, so it stays upright
/// and readable while the world is rendered in 3D. Copy the camera's rotation, then flip 180° so the
/// sprite's front (+Z) points at the camera.
///
/// <see cref="IsoCameraRig"/> sets the camera's fixed rotation, so the value is constant; this still
/// runs each frame so it survives camera changes and works if the rig is moved.
///
/// Unity setup:
///   1. Add to the GameObject that has the SpriteRenderer (usually a visual child).
///   2. Leave Target Camera null to use Camera.main.
///   3. Set Roll to offset the sprite's own facing (e.g. a weapon swing) within the billboard plane.
///
/// Runtime API: SetRoll(float) changes the in-plane facing at runtime.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BillboardSprite : MonoBehaviour
{
    [Tooltip("Camera to face. Defaults to Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("In-plane rotation in degrees, applied after facing the camera (weapon swing, facing).")]
    [SerializeField] private float rollDegrees;

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        transform.rotation = targetCamera.transform.rotation * Quaternion.Euler(0f, 180f, rollDegrees);
    }

    /// <summary>Sets the in-plane facing roll used for weapon swing / character facing.</summary>
    public void SetRoll(float degrees) => rollDegrees = degrees;
}
