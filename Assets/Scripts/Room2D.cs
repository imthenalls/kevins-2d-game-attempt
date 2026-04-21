using UnityEngine;

public class Room2D : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string roomId = "Room";

    [Header("Spawn + Map")]
    [SerializeField] private Transform defaultSpawnPoint;
    [SerializeField] private Vector2 mapGridPosition;

    [Header("Camera")]
    [SerializeField] private Transform cameraAnchor;
    [SerializeField] private Vector2 roomWorldSize = new Vector2(16f, 9f);

    public string RoomId => roomId;
    public Transform DefaultSpawnPoint => defaultSpawnPoint != null ? defaultSpawnPoint : transform;
    public Vector2 MapGridPosition => mapGridPosition;
    public Vector3 CameraCenter => cameraAnchor != null ? cameraAnchor.position : transform.position;
    public Vector2 RoomWorldSize => roomWorldSize;
}
