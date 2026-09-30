using UnityEngine;

/// <summary>
/// Marks a reachable point inside one principal room of the school interior generated into the Town
/// scene. The school builder places one at each room's doorway; Play Mode reachability tests
/// flood-fill the walkable grid and assert every marker is reached, so a blocked doorway or an
/// unreachable wing fails the suite instead of shipping.
///
/// This is presentation-only metadata (a scene anchor); it owns no state and has no behaviour.
///
/// Unity setup:
///   1. Created automatically by Tools &gt; Worlds &gt; Rebuild School Interior Scene.
///   2. Do not add by hand; the builder owns the markers and keeps one per room.
///
/// Runtime API: none.
/// </summary>
[DisallowMultipleComponent]
public sealed class SchoolRoomMarker : MonoBehaviour
{
    [Tooltip("Stable room id, matching the builder's layout table.")]
    [SerializeField] private string roomId;

    public string RoomId => roomId;
}
