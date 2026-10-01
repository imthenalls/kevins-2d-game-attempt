using System;

namespace Game.Core
{
    /// <summary>
    /// Authoritative room-visibility state for the school: which zone the player currently occupies.
    /// A MonoBehaviour adapter feeds it the player's local cell and hides the covers of every other
    /// zone when the result changes; all the selection rules live here.
    ///
    /// Doorway ownership is deterministic: the player's cell is <c>floor(localPosition)</c>, and a cell
    /// belongs to exactly one zone (zones are disjoint), so crossing a threshold flips the active zone
    /// once, consistently.
    ///
    /// Unity setup: none (pure C#). Runtime API: <see cref="ActiveZoneId"/>,
    /// <see cref="ApplyCell"/>, <see cref="ApplyLocalPosition"/>.
    /// </summary>
    public sealed class SchoolVisibilityModel
    {
        private readonly SchoolZoneLayout layout;

        public SchoolVisibilityModel(SchoolZoneLayout layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        /// <summary>The occupied zone id, or an empty string when the position is outside the school.</summary>
        public string ActiveZoneId { get; private set; } = string.Empty;

        /// <summary>
        /// Sets the active zone from a school-local cell. Returns true only when the zone changed, so
        /// the adapter can update cover visibility on transitions rather than every frame.
        /// </summary>
        public bool ApplyCell(int x, int z)
        {
            string next = layout.TryFindZone(x, z, out SchoolZone zone) ? zone.Id : string.Empty;
            if (string.Equals(next, ActiveZoneId, StringComparison.Ordinal))
                return false;

            ActiveZoneId = next;
            return true;
        }

        /// <summary>Convenience wrapper that floors a school-local XZ position into a cell.</summary>
        public bool ApplyLocalPosition(float localX, float localZ) =>
            ApplyCell(FloorToInt(localX), FloorToInt(localZ));

        private static int FloorToInt(float value) => (int)Math.Floor(value);
    }
}
