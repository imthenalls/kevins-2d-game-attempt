namespace Game.Core
{
    /// <summary>
    /// One visibility zone of the school: a named region made of one or more grid rectangles. Zones
    /// are disjoint, so any walkable cell belongs to at most one zone.
    ///
    /// This is authoritative layout data (Engine-Free Core); the Unity builder reads it to generate
    /// floors, walls and opacity covers, and <see cref="SchoolVisibilityModel"/> uses it to decide
    /// which zone the player occupies.
    ///
    /// Unity setup: none (pure C#). Runtime API: <see cref="Contains"/> and the identity properties.
    /// </summary>
    public sealed class SchoolZone
    {
        private readonly GridRect[] rects;

        public SchoolZone(
            string id,
            string name,
            SchoolZoneKind kind,
            float colorR,
            float colorG,
            float colorB,
            params GridRect[] rects)
        {
            Id = id;
            Name = name;
            Kind = kind;
            ColorR = colorR;
            ColorG = colorG;
            ColorB = colorB;
            this.rects = rects ?? new GridRect[0];
        }

        public string Id { get; }
        public string Name { get; }
        public SchoolZoneKind Kind { get; }
        public float ColorR { get; }
        public float ColorG { get; }
        public float ColorB { get; }
        public GridRect[] Rects => rects;

        public bool IsHallway => Kind == SchoolZoneKind.Hallway;

        public bool Contains(int x, int z)
        {
            for (int i = 0; i < rects.Length; i++)
                if (rects[i].Contains(x, z))
                    return true;

            return false;
        }
    }
}
