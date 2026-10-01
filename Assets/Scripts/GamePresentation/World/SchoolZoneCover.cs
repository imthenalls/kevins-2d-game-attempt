using UnityEngine;

/// <summary>
/// Marks the opacity cover for one school visibility zone. The cover is an opaque roof mesh generated
/// over the zone's exact cells; <see cref="SchoolVisibilityController"/> hides the cover of the zone
/// the player occupies and shows every other cover. Purely visual — the cover has no collider and is
/// never a gameplay object.
///
/// Unity setup:
///   1. Created automatically by Tools &gt; Worlds &gt; Rebuild Town Scene (3D) or
///      Tools &gt; Worlds &gt; Town 3D &gt; Refresh School Interior; do not add by hand.
///   2. Lives under the school root's "Visibility Covers" object.
///
/// Runtime API: <see cref="ZoneId"/>; <see cref="SetVisible"/> toggles the cover renderer.
/// </summary>
[DisallowMultipleComponent]
public sealed class SchoolZoneCover : MonoBehaviour
{
    [Tooltip("Visibility zone this cover conceals (a Game.Core.SchoolZoneLayout id).")]
    [SerializeField] private string zoneId;

    private Renderer coverRenderer;

    public string ZoneId => zoneId;

    /// <summary>True when the cover renderer is currently shown.</summary>
    public bool IsVisible
    {
        get
        {
            if (coverRenderer == null)
                coverRenderer = GetComponent<Renderer>();
            return coverRenderer != null && coverRenderer.enabled;
        }
    }

    /// <summary>Sets the zone id; used by the builder.</summary>
    public void Configure(string id) => zoneId = id;

    /// <summary>Shows or hides the cover without touching gameplay objects.</summary>
    public void SetVisible(bool visible)
    {
        if (coverRenderer == null)
            coverRenderer = GetComponent<Renderer>();

        if (coverRenderer != null)
            coverRenderer.enabled = visible;
    }
}
