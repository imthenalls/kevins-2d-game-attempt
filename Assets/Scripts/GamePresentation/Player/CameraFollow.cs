using UnityEngine;

/// <summary>
/// Keeps the camera centred on the player, in every scene. <see cref="GameBootstrap"/> adds this to
/// the scene's <c>MainCamera</c> when it is missing, so cameras always follow without per-scene
/// wiring.
///
/// Unity setup: none - added automatically. Optionally assign Follow Target (defaults to the first
/// PlayerController2D) or change Zoom in the Inspector.
///
/// Runtime API: none.
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraFollow : MonoBehaviour
{
    [Tooltip("Target to follow. Defaults to the first PlayerController2D in the scene.")]
    [SerializeField] private Transform followTarget;

    [Tooltip("Orthographic size. Smaller = zoomed in more.")]
    [SerializeField] private float zoom = 6f;

    [Tooltip("Follow smoothing. 0 snaps instantly.")]
    [SerializeField] private float smooth = 12f;

    [SerializeField] private float zOffset = -10f;

    private Camera cameraComponent;

    private void Awake() => cameraComponent = GetComponent<Camera>();

    private void LateUpdate()
    {
        if (cameraComponent == null)
            cameraComponent = GetComponent<Camera>();

        if (cameraComponent != null)
        {
            if (!cameraComponent.orthographic)
                cameraComponent.orthographic = true;
            if (zoom > 0f && !Mathf.Approximately(cameraComponent.orthographicSize, zoom))
                cameraComponent.orthographicSize = zoom;
        }

        if (followTarget == null)
        {
            PlayerController2D player = FindAnyObjectByType<PlayerController2D>();
            if (player != null)
                followTarget = player.transform;
        }

        if (followTarget == null)
            return;

        // If the camera is parented to the target (a player-prefab camera), keep it centred in local
        // space so the follow does not fight the parenting.
        if (transform.parent == followTarget || (transform.parent != null && transform.parent.IsChildOf(followTarget)))
        {
            transform.localPosition = new Vector3(0f, 0f, zOffset);
            return;
        }

        Vector3 target = new Vector3(followTarget.position.x, followTarget.position.y, zOffset);
        Vector3 next = smooth <= 0f
            ? target
            : Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-smooth * Time.deltaTime));

        transform.position = new Vector3(next.x, next.y, zOffset);
    }
}
