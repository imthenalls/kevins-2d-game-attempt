using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RoomTransitionTrigger2D : MonoBehaviour
{
    [SerializeField] private Room2D targetRoom;
    [SerializeField] private Transform targetSpawnPoint;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (targetRoom == null)
        {
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        RoomManager2D manager = RoomManager2D.Instance;
        if (manager == null)
        {
            return;
        }

        manager.TryTransitionTo(targetRoom, targetSpawnPoint);
    }
}
