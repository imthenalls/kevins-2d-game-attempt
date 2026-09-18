using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Conservation and save/load tests for the mana Wallet. Mana must move without being created
    /// or destroyed, transfers must be atomic, and a saved wallet must restore exactly.
    ///
    /// Unity setup: none. Runs in EditMode with no scene.
    /// </summary>
    public class WalletTests
    {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
                Object.DestroyImmediate(o);
            created.Clear();
        }

        [Test]
        public void Transfer_Conserves_Total_Mana()
        {
            Wallet a = MakeWallet(100, 1000);
            Wallet b = MakeWallet(0, 1000);

            Assert.IsTrue(a.TryTransferTo(b, 30));

            Assert.AreEqual(70, a.Balance);
            Assert.AreEqual(30, b.Balance);
            Assert.AreEqual(100, a.Balance + b.Balance, "mana must be conserved");
        }

        [Test]
        public void Transfer_To_A_Full_Recipient_Is_Rejected_Without_Mutation()
        {
            Wallet a = MakeWallet(100, 1000);
            Wallet b = MakeWallet(40, 50);

            Assert.IsFalse(a.TryTransferTo(b, 30));

            Assert.AreEqual(100, a.Balance);
            Assert.AreEqual(40, b.Balance);
        }

        [Test]
        public void Transfer_Beyond_Balance_Is_Rejected_Without_Mutation()
        {
            Wallet a = MakeWallet(20, 1000);
            Wallet b = MakeWallet(0, 1000);

            Assert.IsFalse(a.TryTransferTo(b, 30));

            Assert.AreEqual(20, a.Balance);
            Assert.AreEqual(0, b.Balance);
        }

        [Test]
        public void Add_Cannot_Exceed_Capacity()
        {
            Wallet wallet = MakeWallet(40, 50);

            Assert.IsFalse(wallet.Add(20));
            Assert.AreEqual(40, wallet.Balance);

            Assert.IsTrue(wallet.Add(10));
            Assert.AreEqual(50, wallet.Balance);
        }

        [Test]
        public void Save_Load_Round_Trip_Preserves_Balance_Capacity_And_History()
        {
            Wallet wallet = MakeWallet(100, 500);
            wallet.Add(25, "reward");
            wallet.TrySpend(40, "purchase");

            WalletSaveData saved = wallet.GetSaveData();
            Assert.AreEqual(85, saved.balance);
            Assert.AreEqual(500, saved.capacity);
            Assert.AreEqual(2, saved.transactions.Count);

            Wallet restored = MakeWallet(0, 0);
            restored.LoadSaveData(saved);

            Assert.AreEqual(85, restored.Balance);
            Assert.AreEqual(500, restored.Capacity);
            Assert.AreEqual(2, restored.TransactionHistory.Count);
            Assert.AreEqual(saved.transactions[1].referenceId, restored.TransactionHistory[1].referenceId);
        }

        [Test]
        public void Reducing_Capacity_Clamps_Balance_And_Never_Goes_Negative()
        {
            Wallet wallet = MakeWallet(90, 100);

            wallet.SetCapacity(50);

            Assert.AreEqual(50, wallet.Capacity);
            Assert.AreEqual(50, wallet.Balance);
            Assert.GreaterOrEqual(wallet.Balance, 0);
        }

        private Wallet MakeWallet(int balance, int capacity)
        {
            var go = new GameObject("Wallet");
            created.Add(go);
            Wallet wallet = go.AddComponent<Wallet>();
            wallet.InitializeMana(balance, capacity);
            return wallet;
        }
    }
}
