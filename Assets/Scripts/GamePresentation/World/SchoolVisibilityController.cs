using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Room-based visibility for the school interior. It reads the active player's position, asks the
/// engine-free <see cref="SchoolVisibilityModel"/> which zone that cell belongs to, and — only when
/// the zone changes — hides the cover of the occupied zone and shows every other cover. Hidden rooms
/// keep running: nothing but the cover renderer is toggled.
///
/// Only the active player drives visibility; NPCs never do. Covers and this component are generated
/// by the school builder, which places the component on the school root so its transform is the
/// school-local origin (the school is offset inside the Town scene).
///
/// Unity setup:
///   1. Created automatically by the school builder on the "School Interior" root; do not add by hand.
///   2. Optional Inspector field <c>Preview Zone</c> (editor only): reveal one zone to inspect the
///      layout without Play Mode. Leave it empty to show the complete covered layout.
///
/// Runtime API: <see cref="ActiveZoneId"/>, <see cref="ForceRefresh"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class SchoolVisibilityController : MonoBehaviour
{
    [Tooltip("Editor-only: reveal this zone id to inspect its interior. Empty shows every cover.")]
    [SerializeField] private string previewZoneId;

    private SchoolVisibilityModel model;
    private readonly List<SchoolZoneCover> covers = new List<SchoolZoneCover>();
    private Transform player;
    private Vector2Int lastLocalCell = new Vector2Int(int.MinValue, int.MinValue);

    public string ActiveZoneId => model != null ? model.ActiveZoneId : string.Empty;

    private void Awake()
    {
        model = new SchoolVisibilityModel(SchoolZoneLayout.Default);
        CollectCovers();
    }

    private void Start()
    {
        ResolvePlayer();
        Recompute(force: true);
    }

    private void Update()
    {
        if (!ResolvePlayer())
            return;

        Recompute(force: false);
    }

    // Works from the player's school-local cell; the expensive cover toggle only runs on a change.
    private void Recompute(bool force)
    {
        if (player == null)
            return;

        Vector3 origin = transform.position;
        Vector3 p = player.position;
        var cell = new Vector2Int(Mathf.FloorToInt(p.x - origin.x), Mathf.FloorToInt(p.z - origin.z));

        if (!force && cell == lastLocalCell)
            return;

        lastLocalCell = cell;
        if (model.ApplyCell(cell.x, cell.y) || force)
            ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        string active = model.ActiveZoneId;
        for (int i = 0; i < covers.Count; i++)
            covers[i].SetVisible(covers[i].ZoneId != active);
    }

    private void CollectCovers()
    {
        covers.Clear();
        foreach (SchoolZoneCover cover in GetComponentsInChildren<SchoolZoneCover>(includeInactive: true))
        {
            if (cover != null)
                covers.Add(cover);
        }
    }

    private bool ResolvePlayer()
    {
        if (player != null)
            return true;

        PlayerControllerBase controller = FindAnyObjectByType<PlayerControllerBase>();
        if (controller != null)
        {
            player = controller.transform;
            return true;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
        {
            player = tagged.transform;
            return true;
        }

        return false;
    }

    /// <summary>Re-reads the player position and applies visibility immediately.</summary>
    public void ForceRefresh()
    {
        if (model == null)
        {
            model = new SchoolVisibilityModel(SchoolZoneLayout.Default);
            CollectCovers();
        }

        ResolvePlayer();
        Recompute(force: true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        CollectCovers();
        ApplyPreview();
    }

    // Editor preview: reveal the selected zone, otherwise show every cover (complete layout view).
    private void ApplyPreview()
    {
        bool showAll = string.IsNullOrEmpty(previewZoneId);
        for (int i = 0; i < covers.Count; i++)
            covers[i].SetVisible(showAll || covers[i].ZoneId != previewZoneId);
    }

    /// <summary>Editor entry point used by the custom inspector.</summary>
    public void EditorApplyPreview()
    {
        CollectCovers();
        ApplyPreview();
    }
#endif
}
