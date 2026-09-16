using Game.Core;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    /// <summary>
    /// Conservation and atomicity tests for the economy: a trade must move items and mana without
    /// creating or destroying either, and a failed trade must leave both sides untouched.
    ///
    /// These exercise the real TradeService / InventoryTransferService against lightweight test
    /// participants, rather than a copy of the logic.
    ///
    /// Unity setup: none. Runs in EditMode with no scene.
    /// </summary>
    public class TradeConservationTests
    {
        private sealed class FakeParticipant : ITradeParticipant
        {
            public string TradeParticipantId { get; set; }
            public Wallet TradeWallet { get; set; }
            public InventoryModel TradeInventory { get; set; }
        }

        private readonly List<Object> created = new();
        private ItemData apple;
        private InventoryModel sellerInventory;
        private InventoryModel buyerInventory;
        private Wallet sellerWallet;
        private Wallet buyerWallet;
        private FakeParticipant seller;
        private FakeParticipant buyer;

        [SetUp]
        public void SetUp()
        {
            TradeService.LoadSaveData(null);

            apple = MakeItem("apple", stackable: true);
            sellerInventory = new InventoryModel(2, 2);
            buyerInventory = new InventoryModel(2, 2);
            sellerWallet = MakeWallet(balance: 0, capacity: 1000);
            buyerWallet = MakeWallet(balance: 100, capacity: 1000);

            seller = new FakeParticipant { TradeParticipantId = "npc", TradeWallet = sellerWallet, TradeInventory = sellerInventory };
            buyer = new FakeParticipant { TradeParticipantId = "player", TradeWallet = buyerWallet, TradeInventory = buyerInventory };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created)
                Object.DestroyImmediate(o);
            created.Clear();
            TradeService.LoadSaveData(null);
        }

        [Test]
        public void Successful_Trade_Conserves_Items_And_Mana()
        {
            sellerInventory.AddItem(apple, 10);

            TradeResult result = TradeService.TryExecute(Request(3, 5));

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(3, buyerInventory.CountItem(apple));
            Assert.AreEqual(7, sellerInventory.CountItem(apple));
            Assert.AreEqual(10, sellerInventory.CountItem(apple) + buyerInventory.CountItem(apple), "items must be conserved");
            Assert.AreEqual(85, buyerWallet.Balance);
            Assert.AreEqual(15, sellerWallet.Balance);
            Assert.AreEqual(100, buyerWallet.Balance + sellerWallet.Balance, "mana must be conserved");
        }

        [Test]
        public void Failed_Trade_Leaves_Both_Sides_Unchanged()
        {
            sellerInventory.AddItem(apple, 1); // buyer wants 3

            TradeResult result = TradeService.TryExecute(Request(3, 5));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(TradeFailure.InsufficientStock, result.Failure);
            Assert.AreEqual(1, sellerInventory.CountItem(apple));
            Assert.AreEqual(0, buyerInventory.CountItem(apple));
            Assert.AreEqual(100, buyerWallet.Balance);
            Assert.AreEqual(0, sellerWallet.Balance);
        }

        [Test]
        public void Buyer_Cannot_Afford_Trade_And_Nothing_Moves()
        {
            sellerInventory.AddItem(apple, 10);
            buyerWallet.InitializeMana(4, 1000); // needs 15

            TradeResult result = TradeService.TryExecute(Request(3, 5));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(TradeFailure.InsufficientMana, result.Failure);
            Assert.AreEqual(10, sellerInventory.CountItem(apple));
            Assert.AreEqual(0, buyerInventory.CountItem(apple));
            Assert.AreEqual(4, buyerWallet.Balance);
        }

        [Test]
        public void Full_Buyer_Inventory_Rejects_Trade_Without_Mutation()
        {
            sellerInventory.AddItem(apple, 10);

            // Buyer inventory holds a unique, non-stackable item in its only slot.
            ItemData sword = MakeItem("sword", stackable: false);
            buyerInventory = new InventoryModel(1, 1);
            buyer.TradeInventory = buyerInventory;
            buyerInventory.AddItem(sword, 1);

            TradeResult result = TradeService.TryExecute(Request(1, 5));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(TradeFailure.InsufficientInventorySpace, result.Failure);
            Assert.AreEqual(10, sellerInventory.CountItem(apple));
            Assert.AreEqual(0, buyerInventory.CountItem(apple));
        }

        [Test]
        public void Same_Participant_Trade_Is_Rejected()
        {
            sellerInventory.AddItem(apple, 10);

            TradeResult result = TradeService.TryExecute(new TradeRequest
            {
                buyer = buyer,
                seller = buyer,
                item = apple,
                quantity = 1,
                unitPrice = 5,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(TradeFailure.SameParticipant, result.Failure);
        }

        [Test]
        public void Price_Overflow_Is_Rejected()
        {
            sellerInventory.AddItem(apple, 100);

            TradeResult result = TradeService.TryExecute(Request(100, int.MaxValue));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(TradeFailure.PriceOverflow, result.Failure);
            Assert.AreEqual(100, sellerInventory.CountItem(apple));
            Assert.AreEqual(100, buyerWallet.Balance);
        }

        [Test]
        public void Successful_Trade_Records_The_Market_Ledger()
        {
            sellerInventory.AddItem(apple, 10);
            int before = TradeService.CompletedTrades.Count;

            TradeResult result = TradeService.TryExecute(Request(2, 5));

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(before + 1, TradeService.CompletedTrades.Count);
            MarketTransaction last = TradeService.CompletedTrades[TradeService.CompletedTrades.Count - 1];
            Assert.AreEqual(10, last.totalPrice);
            Assert.AreEqual("apple", last.itemId);
            Assert.IsNotNull(result.Transaction);
        }

        [Test]
        public void Inventory_Gift_Conserves_Items()
        {
            sellerInventory.AddItem(apple, 5);
            var destination = new InventoryModel(2, 2);

            GiftTransferResult result = InventoryTransferService.TryGive(sellerInventory, destination, apple, 2);

            Assert.IsTrue(result.Success, result.Message);
            Assert.AreEqual(2, result.QuantityTransferred);
            Assert.AreEqual(3, sellerInventory.CountItem(apple));
            Assert.AreEqual(2, destination.CountItem(apple));
            Assert.AreEqual(5, sellerInventory.CountItem(apple) + destination.CountItem(apple));
        }

        [Test]
        public void Inventory_Gift_To_Self_Is_Rejected_Without_Duplication()
        {
            sellerInventory.AddItem(apple, 5);

            GiftTransferResult result = InventoryTransferService.TryGive(sellerInventory, sellerInventory, apple, 2);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(5, sellerInventory.CountItem(apple));
        }

        [Test]
        public void Inventory_Gift_Without_Stock_Fails_Atomically()
        {
            sellerInventory.AddItem(apple, 1);
            var destination = new InventoryModel(2, 2);

            GiftTransferResult result = InventoryTransferService.TryGive(sellerInventory, destination, apple, 3);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, sellerInventory.CountItem(apple));
            Assert.AreEqual(0, destination.CountItem(apple));
        }

        private TradeRequest Request(int quantity, int unitPrice) => new TradeRequest
        {
            buyer = buyer,
            seller = seller,
            item = apple,
            quantity = quantity,
            unitPrice = unitPrice,
        };

        private ItemData MakeItem(string id, bool stackable, int maxStack = 99)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = id;
            item.itemName = id;
            item.maxStackSize = stackable ? maxStack : 1;
            created.Add(item);
            return item;
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
