using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// The canonical school layout: every room zone plus the single hallway/common zone, as grid
    /// rectangles. The Unity school builder reads this to generate floors, walls and opacity covers,
    /// so geometry and visibility rules never drift apart.
    ///
    /// Coordinate convention: +X is east, +Z is north; the south entrance is at low Z. Cells are
    /// 1 world unit. Zones are disjoint, so <see cref="TryFindZone"/> returns the single owner.
    ///
    /// Unity setup: none (pure C#). Runtime API: <see cref="Default"/>, <see cref="TryFindZone"/>,
    /// <see cref="FindById"/>.
    /// </summary>
    public sealed class SchoolZoneLayout
    {
        public const string HallId = "hall";

        private readonly List<SchoolZone> zones;

        public SchoolZoneLayout(List<SchoolZone> zones)
        {
            this.zones = zones ?? new List<SchoolZone>();
        }

        public List<SchoolZone> Zones => zones;

        /// <summary>Locates the zone that owns a cell, or returns false when the cell is outside.</summary>
        public bool TryFindZone(int x, int z, out SchoolZone zone)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].Contains(x, z))
                {
                    zone = zones[i];
                    return true;
                }
            }

            zone = null;
            return false;
        }

        public SchoolZone FindById(string id)
        {
            for (int i = 0; i < zones.Count; i++)
                if (zones[i].Id == id)
                    return zones[i];

            return null;
        }

        /// <summary>The shared layout built once; the builder and model both use it.</summary>
        public static SchoolZoneLayout Default { get; } = BuildDefault();

        private static SchoolZoneLayout BuildDefault()
        {
            var zones = new List<SchoolZone>
            {
                // Corridors form one continuous hallway/common zone.
                new SchoolZone(HallId, "Corridor", SchoolZoneKind.Hallway, 0.72f, 0.71f, 0.68f,
                    new GridRect(44, 2, 12, 8),    // entrance lobby (south)
                    new GridRect(44, 10, 12, 29),  // commons (central)
                    new GridRect(44, 39, 12, 13),  // cafeteria / dining (central-north)
                    new GridRect(6, 22, 38, 4),    // west corridor
                    new GridRect(56, 22, 37, 4),   // east corridor
                    new GridRect(6, 56, 87, 4),    // north corridor
                    new GridRect(6, 2, 4, 69),     // west vertical corridor
                    new GridRect(28, 2, 4, 69),    // mid-west vertical corridor
                    new GridRect(56, 16, 37, 6),   // south-east corridor
                    new GridRect(56, 2, 4, 20),    // south-east vertical corridor
                    new GridRect(66, 26, 4, 30)),  // east vertical corridor

                // Southwest: middle school.
                Room("ms_classroom_1", "Middle School Classroom 1", SchoolZoneKind.Classroom, 0.55f, 0.72f, 0.86f, 10, 3, 9, 10),
                Room("ms_classroom_2", "Middle School Classroom 2", SchoolZoneKind.Classroom, 0.55f, 0.72f, 0.86f, 19, 3, 9, 10),
                Room("ms_classroom_3", "Middle School Classroom 3", SchoolZoneKind.Classroom, 0.55f, 0.72f, 0.86f, 10, 13, 9, 9),
                Room("ms_classroom_4", "Middle School Classroom 4", SchoolZoneKind.Classroom, 0.55f, 0.72f, 0.86f, 19, 13, 9, 9),

                // Far west: science.
                Room("chem_physics", "Chemistry & Physics Lab", SchoolZoneKind.Science, 0.45f, 0.78f, 0.68f, 32, 3, 12, 10),
                Room("biology", "Biology Lab", SchoolZoneKind.Science, 0.45f, 0.78f, 0.68f, 32, 13, 12, 9),

                // Northwest: high school + practical teaching.
                Room("hs_classroom_1", "High School Classroom 1", SchoolZoneKind.Classroom, 0.58f, 0.70f, 0.88f, 10, 26, 9, 10),
                Room("hs_classroom_2", "High School Classroom 2", SchoolZoneKind.Classroom, 0.58f, 0.70f, 0.88f, 19, 26, 9, 10),
                Room("sewing", "Sewing Room", SchoolZoneKind.Workshop, 0.86f, 0.74f, 0.62f, 10, 36, 9, 9),
                Room("cooking", "Cooking Room", SchoolZoneKind.Kitchen, 0.90f, 0.80f, 0.55f, 19, 36, 9, 9),
                Room("living_learning", "Living & Learning Center", SchoolZoneKind.Study, 0.72f, 0.78f, 0.66f, 10, 45, 9, 11),
                Room("hs_lab", "High School Lab", SchoolZoneKind.Science, 0.50f, 0.76f, 0.72f, 19, 45, 9, 11),

                // Central-west: auditorium + administration.
                Room("auditorium", "Auditorium", SchoolZoneKind.Auditorium, 0.62f, 0.55f, 0.85f, 32, 26, 12, 19),
                Room("admin_office", "Administrative Office", SchoolZoneKind.Admin, 0.80f, 0.75f, 0.62f, 32, 45, 6, 11),
                Room("nurse", "Nurse's Office", SchoolZoneKind.Admin, 0.86f, 0.80f, 0.80f, 38, 45, 6, 11),

                // North: art, library, study, workshops.
                Room("art", "Art Room", SchoolZoneKind.Art, 0.88f, 0.62f, 0.72f, 10, 60, 18, 11),
                Room("library", "Library", SchoolZoneKind.Library, 0.55f, 0.75f, 0.55f, 32, 60, 12, 11),
                Room("study_hall", "Study Hall", SchoolZoneKind.Study, 0.74f, 0.76f, 0.70f, 44, 60, 12, 11),
                Room("wood_shop", "Industrial Arts Shop", SchoolZoneKind.Workshop, 0.70f, 0.58f, 0.45f, 56, 60, 17, 11),
                Room("auto_shop", "Vocational Agriculture Shop", SchoolZoneKind.Workshop, 0.64f, 0.56f, 0.44f, 73, 60, 20, 11),

                // Southeast: music + wrestling.
                Room("vocal_music", "Vocal Music", SchoolZoneKind.Music, 0.90f, 0.66f, 0.42f, 60, 2, 13, 14),
                Room("instrumental_music", "Instrumental Music", SchoolZoneKind.Music, 0.90f, 0.62f, 0.36f, 73, 2, 12, 14),
                Room("wrestling", "Wrestling / Apparatus", SchoolZoneKind.Wrestling, 0.80f, 0.55f, 0.48f, 85, 2, 8, 14),

                // East-central: kitchen, staff, gym + lockers.
                Room("staff_room", "Staff Room", SchoolZoneKind.Admin, 0.78f, 0.78f, 0.68f, 56, 26, 10, 13),
                Room("kitchen", "Kitchen", SchoolZoneKind.Kitchen, 0.90f, 0.85f, 0.55f, 56, 39, 10, 17),
                Room("gym", "Gymnasium", SchoolZoneKind.Gym, 0.85f, 0.72f, 0.45f, 70, 26, 19, 30),
                Room("boys_lockers", "Boys' Locker Room", SchoolZoneKind.Lockers, 0.66f, 0.68f, 0.72f, 89, 26, 4, 15),
                Room("girls_lockers", "Girls' Locker Room", SchoolZoneKind.Lockers, 0.72f, 0.68f, 0.74f, 89, 41, 4, 15),
            };

            return new SchoolZoneLayout(zones);
        }

        private static SchoolZone Room(
            string id, string name, SchoolZoneKind kind,
            float r, float g, float b, int x, int z, int w, int d)
        {
            return new SchoolZone(id, name, kind, r, g, b, new GridRect(x, z, w, d));
        }
    }
}
