using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoomMapUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoomManager2D roomManager;
    [SerializeField] private RectTransform mapRoot;

    [Header("Layout")]
    [SerializeField] private Vector2 cellSize = new Vector2(22f, 22f);

    [Header("Colors")]
    [SerializeField] private Color undiscoveredColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    [SerializeField] private Color discoveredColor = new Color(0.7f, 0.7f, 0.7f, 0.9f);
    [SerializeField] private Color currentRoomColor = new Color(0.2f, 1f, 0.35f, 1f);

    private readonly Dictionary<Room2D, Image> roomTiles = new Dictionary<Room2D, Image>();

    private void Awake()
    {
        if (roomManager == null)
        {
            roomManager = RoomManager2D.Instance;
        }
    }

    private void OnEnable()
    {
        if (roomManager != null)
        {
            roomManager.RoomChanged += HandleRoomChanged;
        }

        BuildMap();
        RefreshMapColors();
    }

    private void OnDisable()
    {
        if (roomManager != null)
        {
            roomManager.RoomChanged -= HandleRoomChanged;
        }
    }

    private void HandleRoomChanged(Room2D _)
    {
        RefreshMapColors();
    }

    private void BuildMap()
    {
        if (mapRoot == null)
        {
            return;
        }

        foreach (Transform child in mapRoot)
        {
            Destroy(child.gameObject);
        }

        roomTiles.Clear();

        Room2D[] rooms = FindObjectsOfType<Room2D>(true);
        for (int i = 0; i < rooms.Length; i++)
        {
            Room2D room = rooms[i];
            if (room == null)
            {
                continue;
            }

            GameObject tileObj = new GameObject($"Map_{room.RoomId}", typeof(RectTransform), typeof(Image));
            tileObj.transform.SetParent(mapRoot, false);

            RectTransform tileRect = tileObj.GetComponent<RectTransform>();
            tileRect.anchorMin = new Vector2(0.5f, 0.5f);
            tileRect.anchorMax = new Vector2(0.5f, 0.5f);
            tileRect.pivot = new Vector2(0.5f, 0.5f);
            tileRect.sizeDelta = cellSize;
            tileRect.anchoredPosition = new Vector2(room.MapGridPosition.x * cellSize.x, room.MapGridPosition.y * cellSize.y);

            Image image = tileObj.GetComponent<Image>();
            image.color = undiscoveredColor;
            roomTiles[room] = image;
        }
    }

    private void RefreshMapColors()
    {
        if (roomManager == null)
        {
            return;
        }

        foreach (KeyValuePair<Room2D, Image> pair in roomTiles)
        {
            Room2D room = pair.Key;
            Image tile = pair.Value;
            if (room == null || tile == null)
            {
                continue;
            }

            if (room == roomManager.CurrentRoom)
            {
                tile.color = currentRoomColor;
            }
            else
            {
                tile.color = roomManager.IsDiscovered(room) ? discoveredColor : undiscoveredColor;
            }
        }
    }
}
