using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the pending quest-reward ledger: merging, partial delivery, unknown
    /// items, retrying claims, and the save snapshot. Delivery itself is a delegate the Unity
    /// adapter supplies, so the ledger is verified without a scene or ItemData.
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class PendingRewardLedgerTests
    {
        [Test]
        public void Record_Merges_Repeated_Grants_For_The_Same_Quest_And_Item()
        {
            var ledger = new PendingRewardLedger();

            PendingRewardEntry first = ledger.Record("quest", "sword", 2);
            PendingRewardEntry second = ledger.Record("quest", "sword", 3);

            Assert.AreSame(first, second);
            Assert.AreEqual(1, ledger.Pending.Count);
            Assert.AreEqual(5, ledger.Pending[0].remaining);
            Assert.AreEqual("quest:sword", ledger.Pending[0].rewardId);
        }

        [Test]
        public void Record_Keeps_Different_Quests_Apart()
        {
            var ledger = new PendingRewardLedger();

            ledger.Record("questA", "sword", 1);
            ledger.Record("questB", "sword", 1);

            Assert.AreEqual(2, ledger.Pending.Count);
        }

        [Test]
        public void Record_Ignores_NonPositive_Or_Empty_Inputs()
        {
            var ledger = new PendingRewardLedger();

            Assert.IsNull(ledger.Record("quest", "sword", 0));
            Assert.IsNull(ledger.Record("quest", "", 4));
            Assert.IsFalse(ledger.HasPendingRewards);
        }

        [Test]
        public void Claim_Partial_Delivery_Leaves_The_Remainder()
        {
            var ledger = new PendingRewardLedger();
            ledger.Record("quest", "sword", 5);

            PendingRewardClaimResult result = ledger.Claim((id, requested) => PendingRewardDelivery.Accepted(3));

            Assert.AreEqual(3, result.Delivered);
            Assert.AreEqual(0, result.Dropped);
            Assert.AreEqual(1, ledger.Pending.Count);
            Assert.AreEqual(2, ledger.Pending[0].remaining);
        }

        [Test]
        public void Claim_With_Full_Inventory_Delivers_Nothing_And_Keeps_The_Entry()
        {
            var ledger = new PendingRewardLedger();
            ledger.Record("quest", "sword", 4);

            PendingRewardClaimResult result = ledger.Claim((id, requested) => PendingRewardDelivery.Accepted(0));

            Assert.AreEqual(0, result.Delivered);
            Assert.AreEqual(1, ledger.Pending.Count);
            Assert.AreEqual(4, ledger.Pending[0].remaining);
        }

        [Test]
        public void Claim_Retry_Delivers_The_Rest_And_Removes_The_Entry()
        {
            var ledger = new PendingRewardLedger();
            ledger.Record("quest", "sword", 5);
            ledger.Claim((id, requested) => PendingRewardDelivery.Accepted(2));

            PendingRewardClaimResult result = ledger.Claim((id, requested) => PendingRewardDelivery.Accepted(10));

            Assert.AreEqual(3, result.Delivered); // clamped to the remaining 3
            Assert.IsFalse(ledger.HasPendingRewards);
        }

        [Test]
        public void Claim_Drops_Unknown_Items()
        {
            var ledger = new PendingRewardLedger();
            ledger.Record("quest", "ghost", 3);

            PendingRewardClaimResult result = ledger.Claim((id, requested) => PendingRewardDelivery.Unknown());

            Assert.AreEqual(1, result.Dropped);
            Assert.AreEqual(0, result.Delivered);
            Assert.IsFalse(ledger.HasPendingRewards);
        }

        [Test]
        public void Claim_Null_Delegate_Is_A_NoOp()
        {
            var ledger = new PendingRewardLedger();
            ledger.Record("quest", "sword", 3);

            PendingRewardClaimResult result = ledger.Claim(null);

            Assert.AreEqual(0, result.Delivered);
            Assert.AreEqual(1, ledger.Pending.Count);
        }

        [Test]
        public void Snapshot_Is_Independent_Of_Later_Changes()
        {
            var ledger = new PendingRewardLedger();
            ledger.Record("quest", "sword", 3);

            List<PendingRewardEntry> snapshot = ledger.Snapshot();
            ledger.Record("quest", "sword", 4);

            Assert.AreEqual(3, snapshot[0].remaining);
            Assert.AreEqual(7, ledger.Pending[0].remaining);
        }

        [Test]
        public void Load_Round_Trips_And_Skips_Invalid_Entries()
        {
            var ledger = new PendingRewardLedger();
            ledger.Record("quest", "sword", 3);

            var loaded = new PendingRewardLedger();
            loaded.Load(new List<PendingRewardEntry>
            {
                ledger.Snapshot()[0],
                new PendingRewardEntry { questId = "q", itemId = "bad", remaining = 0 },
                null,
            });

            Assert.AreEqual(1, loaded.Pending.Count);
            Assert.AreEqual("sword", loaded.Pending[0].itemId);
            Assert.AreEqual(3, loaded.Pending[0].remaining);
        }

        [Test]
        public void Changed_Fires_On_Record_And_Successful_Claim()
        {
            var ledger = new PendingRewardLedger();
            int changes = 0;
            ledger.Changed += () => changes++;

            ledger.Record("quest", "sword", 1);
            ledger.Claim((id, requested) => PendingRewardDelivery.Accepted(1));

            Assert.AreEqual(2, changes);
        }
    }
}
