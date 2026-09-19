using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the authoritative mana model. The Unity Wallet facade forwards to this,
    /// so money conservation, capacity rules, and save round-trips are covered without a scene.
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class ManaAccountTests
    {
        [Test]
        public void Transfer_Conserves_Total_Mana()
        {
            var a = new ManaAccount();
            var b = new ManaAccount();
            a.InitializeMana(100, 1000);
            b.InitializeMana(0, 1000);

            Assert.IsTrue(a.TryTransferTo(b, 30));

            Assert.AreEqual(70, a.Balance);
            Assert.AreEqual(30, b.Balance);
            Assert.AreEqual(100, a.Balance + b.Balance, "mana must be conserved");
        }

        [Test]
        public void Transfer_Credits_The_Recipient_Once()
        {
            var a = new ManaAccount();
            var b = new ManaAccount();
            a.InitializeMana(100, 1000);
            b.InitializeMana(0, 1000);

            int credited = 0;
            b.Credited += amount => credited += amount;

            a.TryTransferTo(b, 25);
            a.TryTransferTo(b, 10);

            Assert.AreEqual(35, credited);
        }

        [Test]
        public void Transfer_To_A_Full_Recipient_Is_Atomic()
        {
            var a = new ManaAccount();
            var b = new ManaAccount();
            a.InitializeMana(100, 1000);
            b.InitializeMana(40, 50);

            Assert.IsFalse(a.TryTransferTo(b, 30));
            Assert.AreEqual(100, a.Balance);
            Assert.AreEqual(40, b.Balance);
        }

        [Test]
        public void Add_Cannot_Exceed_Capacity()
        {
            var account = new ManaAccount();
            account.InitializeMana(40, 50);

            Assert.IsFalse(account.Add(20));
            Assert.AreEqual(40, account.Balance);
            Assert.IsTrue(account.Add(10));
            Assert.AreEqual(50, account.Balance);
        }

        [Test]
        public void Reducing_Capacity_Clamps_Balance()
        {
            var account = new ManaAccount();
            account.InitializeMana(90, 100);

            account.SetCapacity(50);

            Assert.AreEqual(50, account.Capacity);
            Assert.AreEqual(50, account.Balance);
        }

        [Test]
        public void Growth_Restores_Only_Available_Capacity()
        {
            var account = new ManaAccount();
            account.InitializeMana(10, 20);

            int restored = account.RestoreMana(50);

            Assert.AreEqual(10, restored);
            Assert.AreEqual(20, account.Balance);
        }

        [Test]
        public void Save_Load_Round_Trip_Preserves_Balance_Capacity_And_History()
        {
            var account = new ManaAccount();
            account.InitializeMana(100, 500);
            account.Add(25, "reward");
            account.TrySpend(40, "purchase");

            WalletSaveData saved = account.GetSaveData();
            Assert.AreEqual(85, saved.balance);
            Assert.AreEqual(500, saved.capacity);
            Assert.AreEqual(2, saved.transactions.Count);

            var restored = new ManaAccount();
            restored.LoadSaveData(saved);

            Assert.AreEqual(85, restored.Balance);
            Assert.AreEqual(500, restored.Capacity);
            Assert.AreEqual(2, restored.TransactionHistory.Count);
            Assert.AreEqual(saved.transactions[1].referenceId, restored.TransactionHistory[1].referenceId);
        }

        [Test]
        public void History_Is_Trimmed_To_The_Maximum()
        {
            var account = new ManaAccount(maxTransactionHistory: 3);
            account.InitializeMana(0, 1000);

            for (int i = 0; i < 6; i++)
                account.Add(1, "tick" + i);

            Assert.AreEqual(3, account.TransactionHistory.Count);
            Assert.AreEqual("tick5", account.TransactionHistory[2].reason);
        }
    }
}
