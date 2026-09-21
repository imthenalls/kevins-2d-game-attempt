using UnityEngine;

/// <summary>
/// Keeps a 3D camera at a fixed isometric angle above the player, for 3D planar-isometric scenes.
/// The diamond/isometric look comes from the camera's yaw + pitch, not from a tilemap projection.
///
/// <see cref="GameBootstrap"/> adds this automatically to the scene's <c>MainCamera</c> when the
/// scene contains an <see cref="Isometric3DScene"/> or a <see cref="PlayerController3D"/>.
///
/// Unity setup:
///   1. Added automatically; optionally set Follow Target in the Inspector (defaults to the first
///      <see cref="PlayerControllerBase"/>).
///   2. Pitch 30-35 gives the classic 2:1 isometric framing; Yaw 45 faces the +X/+Z corner.
///
/// Runtime API: none.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class IsoCameraRig : MonoBehaviour
{
    [Tooltip("Target to follow. Defaults to the first PlayerControllerBase in the scene.")]
    [SerializeField] private Transform followTarget;

    [Tooltip("Orthographic size. Smaller = zoomed in more.")]
    [SerializeField] private float zoom = 9f;

    [Tooltip("Camera pitch in degrees. ~30 is a 2:1 isometric look.")]
    [SerializeField] private float pitch = 30f;

    [Tooltip("Camera yaw in degrees. 45 faces the +X/+Z corner.")]
    [SerializeField] private float yaw = 45f;

    [Tooltip("Distance along the view axis. Only affects clipping for an orthographic camera.")]
    [SerializeField] private float distance = 60f;

    [Tooltip("Follow smoothing. 0 snaps instantly.")]
    [SerializeField] private float smooth = 12f;

    private Camera cameraComponent;

    /// <summary>The rotation every billboard should copy to stay upright and face this camera.</summary>
    public Quaternion BillboardRotation => transform.rotation;

    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        ApplyProjection();
        ApplyRigImmediate();
    }

    private void LateUpdate()
    {
        if (cameraComponent == null)
            cameraComponent = GetComponent<Camera>();

        ApplyProjection();
        ApplyRig(smooth);
    }

    private void ApplyProjection()
    {
        if (cameraComponent == null)
            return;

        cameraComponent.orthographic = true;
        if (zoom > 0f && !Mathf.Approximately(cameraComponent.orthographicSize, zoom))
            cameraComponent.orthographicSize = zoom;
    }

    private void ApplyRig(float smoothing)
    {
        if (!TryResolveTarget(out Vector3 focus))
            return;

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rotation;
        Vector3 desired = focus - rotation * Vector3.forward * distance;

        transform.position = smoothing <= 0f
            ? desired
            : Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
    }

    private void ApplyRigImmediate()
    {
        if (!TryResolveTarget(out Vector3 focus))
            return;

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rotation;
        transform.position = focus - rotation * Vector3.forward * distance;
    }

    private bool TryResolveTarget(out Vector3 focus)
    {
        if (followTarget == null)
        {
            PlayerControllerBase player = FindAnyObjectByType<PlayerControllerBase>();
            if (player != null)
                followTarget = player.transform;
        }

        if (followTarget == null)
        {
            focus = Vector3.zero;
            return false;
        }

        focus = followTarget.position;
        return true;
    }
}
