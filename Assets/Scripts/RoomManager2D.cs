using System;
using System.Collections.Generic;
using UnityEngine;

public class RoomManager2D : MonoBehaviour
{
    public static RoomManager2D Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Startup")]
    [SerializeField] private Room2D startingRoom;

    [Header("Behavior")]
    [SerializeField] private bool keepOnlyCurrentRoomActive = true;
    [SerializeField, Min(0f)] private float transitionCooldown = 0.15f;

    [Header("2D Depth Safety")]
    [SerializeField] private bool enforce2DDepth = true;
    [SerializeField] private float playerZ = 0f;

    private readonly HashSet<Room2D> discoveredRooms = new HashSet<Room2D>();
    private Room2D[] allRooms = Array.Empty<Room2D>();
    private float lastTransitionTime;

    public Room2D CurrentRoom { get; private set; }
    public event Action<Room2D> RoomChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CacheRooms();
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        Room2D initialRoom = startingRoom != null ? startingRoom : GetRoomContainingPlayer();
        if (initialRoom == null)
        {
            initialRoom = FindNearestRoomToPlayer();
        }

        if (initialRoom != null)
        {
            EnterRoom(initialRoom, initialRoom.DefaultSpawnPoint, false);
        }

        ApplyPlayerDepth();
    }

    public void TryTransitionTo(Room2D targetRoom, Transform targetSpawnPoint = null)
    {
        if (targetRoom == null || player == null)
        {
            return;
        }

        if (Time.time < lastTransitionTime + transitionCooldown)
        {
            return;
        }

        EnterRoom(targetRoom, targetSpawnPoint != null ? targetSpawnPoint : targetRoom.DefaultSpawnPoint, true);
    }

    public bool IsDiscovered(Room2D room)
    {
        return room != null && discoveredRooms.Contains(room);
    }

    private void EnterRoom(Room2D room, Transform spawnPoint, bool movePlayer)
    {
        CurrentRoom = room;
        discoveredRooms.Add(room);

        if (keepOnlyCurrentRoomActive)
        {
            for (int i = 0; i < allRooms.Length; i++)
            {
                Room2D candidate = allRooms[i];
                if (candidate == null)
                {
                    continue;
                }

                bool shouldStayActive = candidate == room || ContainsProtectedObjects(candidate);
                candidate.gameObject.SetActive(shouldStayActive);
            }
        }

        if (movePlayer)
        {
            Vector3 targetPosition = spawnPoint.position;
            if (enforce2DDepth)
            {
                targetPosition.z = playerZ;
            }

            player.position = targetPosition;

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        ApplyPlayerDepth();

        lastTransitionTime = Time.time;
        RoomChanged?.Invoke(room);
    }

    private void CacheRooms()
    {
        allRooms = FindObjectsOfType<Room2D>(true);
    }

  

    private Room2D FindNearestRoomToPlayer()
    {
        if (player == null || allRooms.Length == 0)
        {
            return allRooms.Length > 0 ? allRooms[0] : null;
        }

        float bestDistance = float.MaxValue;
        Room2D nearest = null;

        for (int i = 0; i < allRooms.Length; i++)
        {
            Room2D room = allRooms[i];
            if (room == null)
            {
                continue;
            }

            float sqrDistance = (room.transform.position - player.position).sqrMagnitude;
            if (sqrDistance < bestDistance)
            {
                bestDistance = sqrDistance;
                nearest = room;
            }
        }

        return nearest;
    }

    private Room2D GetRoomContainingPlayer()
    {
        if (player == null)
        {
            return null;
        }

        return player.GetComponentInParent<Room2D>();
    }

    private bool ContainsProtectedObjects(Room2D candidate)
    {
        Transform candidateTransform = candidate.transform;

        if (player != null && player.IsChildOf(candidateTransform))
        {
            return true;
        }


        if (transform.IsChildOf(candidateTransform))
        {
            return true;
        }

        return false;
    }

    private void ApplyPlayerDepth()
    {
        if (!enforce2DDepth || player == null)
        {
            return;
        }

        Vector3 pos = player.position;
        pos.z = playerZ;
        player.position = pos;
    }
}
