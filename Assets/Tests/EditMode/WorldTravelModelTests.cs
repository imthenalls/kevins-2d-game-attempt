using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the authoritative world-travel model: active world, per-world ability
    /// unlocks, remembered positions, and the shared wallet snapshot. These are the values the save
    /// system trusts, so they are verified without a scene.
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class WorldTravelModelTests
    {
        [Test]
        public void UnlockAbility_Adds_Once_And_Normalizes_Id()
        {
            var model = new WorldTravelModel();

            Assert.IsTrue(model.UnlockAbility(WorldLayer.WorldA, "  Dash "));
            Assert.IsFalse(model.UnlockAbility(WorldLayer.WorldA, "dash"));
            Assert.IsTrue(model.HasAbility(WorldLayer.WorldA, "DASH"));
        }

        [Test]
        public void Abilities_Are_Scoped_Per_World()
        {
            var model = new WorldTravelModel();
            model.UnlockAbility(WorldLayer.WorldA, "dash");

            Assert.IsTrue(model.HasAbility(WorldLayer.WorldA, "dash"));
            Assert.IsFalse(model.HasAbility(WorldLayer.WorldB, "dash"));
        }

        [Test]
        public void UnlockAbility_Raises_Event_Only_When_New()
        {
            var model = new WorldTravelModel();
            var seen = new List<(WorldLayer, string)>();
            model.AbilityUnlocked += (world, id) => seen.Add((world, id));

            model.UnlockAbility(WorldLayer.WorldB, "blink");
            model.UnlockAbility(WorldLayer.WorldB, "blink");

            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual((WorldLayer.WorldB, "blink"), seen[0]);
        }

        [Test]
        public void GrantAbility_Does_Not_Raise_Event()
        {
            var model = new WorldTravelModel();
            int events = 0;
            model.AbilityUnlocked += (_, _) => events++;

            Assert.IsTrue(model.GrantAbility(WorldLayer.WorldA, "seed"));
            Assert.AreEqual(0, events);
            Assert.IsTrue(model.HasAbility(WorldLayer.WorldA, "seed"));
        }

        [Test]
        public void SetCurrentWorld_Reports_Change_And_Raises_Once()
        {
            var model = new WorldTravelModel();
            int changes = 0;
            model.WorldChanged += _ => changes++;

            Assert.IsFalse(model.SetCurrentWorld(WorldLayer.WorldA));
            Assert.IsTrue(model.SetCurrentWorld(WorldLayer.WorldB));
            Assert.IsFalse(model.SetCurrentWorld(WorldLayer.WorldB));

            Assert.AreEqual(WorldLayer.WorldB, model.CurrentWorld);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void Abilities_Round_Trip_Through_Save_Entries()
        {
            var model = new WorldTravelModel();
            model.UnlockAbility(WorldLayer.WorldA, "dash");
            model.UnlockAbility(WorldLayer.WorldB, "blink");

            var saved = new List<WorldAbilitySaveEntry>();
            model.WriteAbilities(saved);

            var loaded = new WorldTravelModel();
            loaded.LoadAbilities(saved);

            Assert.IsTrue(loaded.HasAbility(WorldLayer.WorldA, "dash"));
            Assert.IsTrue(loaded.HasAbility(WorldLayer.WorldB, "blink"));
        }

        [Test]
        public void LoadAbilities_Replaces_Previous_Unlocks()
        {
            var model = new WorldTravelModel();
            model.UnlockAbility(WorldLayer.WorldA, "old");

            model.LoadAbilities(new List<WorldAbilitySaveEntry>
            {
                new WorldAbilitySaveEntry { world = "WorldA", abilityId = "new" },
            });

            Assert.IsFalse(model.HasAbility(WorldLayer.WorldA, "old"));
            Assert.IsTrue(model.HasAbility(WorldLayer.WorldA, "new"));
        }

        [Test]
        public void Positions_Round_Trip_Through_Save_Entries()
        {
            var model = new WorldTravelModel();
            model.SetPosition(WorldLayer.WorldB, new RememberedWorldPosition
            {
                Scene = "WorldBScene",
                HasCell = true,
                CellX = 4,
                CellY = -2,
                OffsetX = 0.25f,
                OffsetY = -0.5f,
                LegacyX = 10f,
                LegacyY = 11f,
                LegacyZ = 12f,
            });

            var saved = new List<WorldPositionSaveEntry>();
            model.WritePositions(saved);

            var loaded = new WorldTravelModel();
            loaded.ClearPositions();
            foreach (WorldPositionSaveEntry entry in saved)
            {
                if (System.Enum.TryParse(entry.world, out WorldLayer world))
                    loaded.SetPosition(world, RememberedWorldPosition.FromSave(entry));
            }

            Assert.IsTrue(loaded.TryGetPosition(WorldLayer.WorldB, out RememberedWorldPosition position));
            Assert.AreEqual("WorldBScene", position.Scene);
            Assert.IsTrue(position.HasCell);
            Assert.AreEqual(4, position.CellX);
            Assert.AreEqual(-2, position.CellY);
            Assert.AreEqual(0.25f, position.OffsetX, 0.0001f);
            Assert.AreEqual(-0.5f, position.OffsetY, 0.0001f);
        }

        [Test]
        public void TryGetPosition_Returns_False_For_Unknown_World()
        {
            var model = new WorldTravelModel();

            Assert.IsFalse(model.TryGetPosition(WorldLayer.WorldA, out _));
        }

        [Test]
        public void Shared_Wallet_Is_Captured_And_Loaded()
        {
            var model = new WorldTravelModel();
            Assert.IsFalse(model.HasSharedPlayerState);

            var wallet = new WalletSaveData { balance = 42, capacity = 100 };
            model.CaptureSharedWallet(wallet);

            Assert.IsTrue(model.HasSharedPlayerState);
            Assert.AreSame(wallet, model.SharedWallet);

            var replacement = new WalletSaveData { balance = 7, capacity = 50 };
            model.LoadSharedWallet(replacement);
            Assert.AreSame(replacement, model.SharedWallet);
        }
    }
}
