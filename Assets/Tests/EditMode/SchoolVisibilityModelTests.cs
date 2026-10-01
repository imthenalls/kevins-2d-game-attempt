using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for school zone selection and room-visibility transitions.</summary>
    public class SchoolVisibilityModelTests
    {
        private static SchoolVisibilityModel NewModel() => new SchoolVisibilityModel(SchoolZoneLayout.Default);

        [Test]
        public void Default_Layout_Assigns_Every_Cell_To_At_Most_One_Zone()
        {
            SchoolZoneLayout layout = SchoolZoneLayout.Default;

            foreach (SchoolZone zone in layout.Zones)
            {
                foreach (GridRect rect in zone.Rects)
                {
                    for (int x = rect.X; x < rect.MaxX; x++)
                    {
                        for (int z = rect.Z; z < rect.MaxZ; z++)
                        {
                            int owners = 0;
                            foreach (SchoolZone candidate in layout.Zones)
                                if (candidate.Contains(x, z))
                                    owners++;

                            Assert.AreEqual(1, owners,
                                "cell (" + x + "," + z + ") must belong to exactly one zone.");
                        }
                    }
                }
            }
        }

        [Test]
        public void Hall_And_Rooms_Have_Expected_Ids()
        {
            SchoolZoneLayout layout = SchoolZoneLayout.Default;

            Assert.IsTrue(layout.TryFindZone(50, 7, out SchoolZone entrance));
            Assert.IsTrue(entrance.IsHallway);
            Assert.AreEqual(SchoolZoneLayout.HallId, entrance.Id);

            Assert.IsTrue(layout.TryFindZone(14, 7, out SchoolZone middleSchool));
            Assert.AreEqual("ms_classroom_1", middleSchool.Id);

            Assert.IsTrue(layout.TryFindZone(36, 8, out SchoolZone science));
            Assert.AreEqual("chem_physics", science.Id);

            Assert.IsTrue(layout.TryFindZone(80, 40, out SchoolZone gym));
            Assert.AreEqual("gym", gym.Id);

            // A corridor cell (the mid-west vertical corridor) is part of the hall zone.
            Assert.IsTrue(layout.TryFindZone(29, 50, out SchoolZone corridor));
            Assert.IsTrue(corridor.IsHallway);
        }

        [Test]
        public void Outside_The_School_Has_No_Zone()
        {
            SchoolZoneLayout layout = SchoolZoneLayout.Default;
            Assert.IsFalse(layout.TryFindZone(-5, -5, out _));
            Assert.IsFalse(layout.TryFindZone(500, 500, out _));
        }

        [Test]
        public void Hall_To_Room_To_Hall_Changes_Once_Per_Transition()
        {
            SchoolVisibilityModel model = NewModel();

            // Start outside (empty).
            Assert.IsFalse(model.ApplyCell(500, 500));
            Assert.AreEqual(string.Empty, model.ActiveZoneId);

            // Enter the hall -> changed.
            Assert.IsTrue(model.ApplyCell(50, 7));
            Assert.AreEqual(SchoolZoneLayout.HallId, model.ActiveZoneId);

            // Still in the hall -> no change.
            Assert.IsFalse(model.ApplyCell(50, 8));

            // Through a doorway into a room -> changed.
            Assert.IsTrue(model.ApplyCell(14, 7));
            Assert.AreEqual("ms_classroom_1", model.ActiveZoneId);

            // Room to adjacent room -> changed.
            Assert.IsTrue(model.ApplyCell(20, 7));
            Assert.AreEqual("ms_classroom_2", model.ActiveZoneId);

            // Back to the hall -> changed.
            Assert.IsTrue(model.ApplyCell(29, 22));
            Assert.AreEqual(SchoolZoneLayout.HallId, model.ActiveZoneId);

            // Leaving the school -> changed to empty.
            Assert.IsTrue(model.ApplyCell(500, 500));
            Assert.AreEqual(string.Empty, model.ActiveZoneId);
        }

        [Test]
        public void Doorway_Threshold_Ownership_Is_Deterministic()
        {
            SchoolZoneLayout layout = SchoolZoneLayout.Default;

            // The west corridor is x 6..9; ms_classroom_1 starts at x 10. The shared boundary is
            // stable: cell 9 is hall, cell 10 is the room, and it never flips without moving.
            Assert.IsTrue(layout.TryFindZone(9, 7, out SchoolZone hall));
            Assert.IsTrue(hall.IsHallway);
            Assert.IsTrue(layout.TryFindZone(10, 7, out SchoolZone room));
            Assert.AreEqual("ms_classroom_1", room.Id);

            SchoolVisibilityModel model = NewModel();
            Assert.IsTrue(model.ApplyCell(9, 7));
            Assert.AreEqual(SchoolZoneLayout.HallId, model.ActiveZoneId);
            Assert.IsTrue(model.ApplyCell(10, 7));
            Assert.AreEqual("ms_classroom_1", model.ActiveZoneId);
            Assert.IsFalse(model.ApplyCell(10, 7), "re-applying the same cell must not change the zone.");
        }

        [Test]
        public void ApplyLocalPosition_Floors_Negative_And_Positive_Cells()
        {
            SchoolVisibilityModel model = NewModel();

            Assert.IsTrue(model.ApplyLocalPosition(50.9f, 7.9f));
            Assert.AreEqual(SchoolZoneLayout.HallId, model.ActiveZoneId);

            // Outside (negative local X) resolves to no zone.
            model.ApplyLocalPosition(-0.2f, 7.5f);
            Assert.AreEqual(string.Empty, model.ActiveZoneId);
        }

        [Test]
        public void Every_Zone_Has_A_Hall_And_Distinct_Ids()
        {
            SchoolZoneLayout layout = SchoolZoneLayout.Default;
            var ids = new HashSet<string>();
            int halls = 0;

            foreach (SchoolZone zone in layout.Zones)
            {
                Assert.IsTrue(ids.Add(zone.Id), "duplicate zone id " + zone.Id);
                if (zone.IsHallway)
                    halls++;
            }

            Assert.AreEqual(1, halls, "there must be exactly one hallway/common zone.");
        }
    }
}
