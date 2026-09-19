using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the authoritative position model (logical cell + local offset). This is
    /// the player position the save system trusts, so it is verified without a scene.
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class PositionModelTests
    {
        [Test]
        public void Constructor_Stores_Seed_Values()
        {
            var model = new PositionModel(3, -4, 0.25f, -0.1f);

            Assert.AreEqual(3, model.CellX);
            Assert.AreEqual(-4, model.CellY);
            Assert.AreEqual(0.25f, model.OffsetX, 0.0001f);
            Assert.AreEqual(-0.1f, model.OffsetY, 0.0001f);
        }

        [Test]
        public void Set_Updates_And_Raises_Changed()
        {
            var model = new PositionModel(0, 0);
            PositionModel seen = null;
            int changes = 0;
            model.Changed += m => { changes++; seen = m; };

            model.Set(5, 2, 0.5f, 0.25f);

            Assert.AreEqual(1, changes);
            Assert.AreSame(model, seen);
            Assert.AreEqual(5, model.CellX);
            Assert.AreEqual(2, model.CellY);
            Assert.AreEqual(0.5f, model.OffsetX, 0.0001f);
        }

        [Test]
        public void Set_With_Identical_Value_Does_Not_Raise()
        {
            var model = new PositionModel(1, 1, 0.2f, 0.3f);
            int changes = 0;
            model.Changed += _ => changes++;

            model.Set(1, 1, 0.2f, 0.3f);

            Assert.AreEqual(0, changes);
        }

        [Test]
        public void Cell_And_Offset_Are_Independent()
        {
            var model = new PositionModel(2, 3, 0.1f, 0.1f);

            model.Set(2, 3, 0.9f, -0.4f); // same cell, new offset

            Assert.AreEqual(2, model.CellX);
            Assert.AreEqual(3, model.CellY);
            Assert.AreEqual(0.9f, model.OffsetX, 0.0001f);
            Assert.AreEqual(-0.4f, model.OffsetY, 0.0001f);
        }
    }
}
