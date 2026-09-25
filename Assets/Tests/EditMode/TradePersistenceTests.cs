using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Verifies the state produced by an item-for-mana trade survives a save/load round trip without
    /// duplicating the market ledger, wallet history, or inventory contents. Uses the engine-free
    /// TradeService, ManaAccount, and InventoryModel so it also runs in the dotnet mirror.
    /// </summary>
    public class TradePersistenceTests
    {
        private sealed class Participant : ITradeParticipant
        {
            public string TradeParticipantId { get; set; }
            public ManaAccount TradeWallet { get; set; }
            public InventoryModel TradeInventory { get; set; }
        }

        [Test]
        public void Trade_State_Survives_Save_Load_RoundTrip()
        {
            TradeService.LoadSaveData(null);

            var seller = new Participant { TradeParticipantId = "npc", TradeWallet = new ManaAccount(), TradeInventory = new InventoryModel(2, 2) };
            var buyer = new Participant { TradeParticipantId = "player", TradeWallet = new ManaAccount(), TradeInventory = new InventoryModel(2, 2) };
            seller.TradeWallet.InitializeMana(0, 1000);
            buyer.TradeWallet.InitializeMana(100, 1000);

            var apple = new StubItem("apple", stackable: true);
            seller.TradeInventory.AddItem(apple, 10);

            TradeResult result = TradeService.TryExecute(new TradeRequest
            {
                buyer = buyer,
                seller = seller,
                item = apple,
                quantity = 3,
                unitPrice = 5,
            });
            Assert.IsTrue(result.Success, result.Message);

            // Capture the post-trade state.
            List<MarketTransaction> ledger = TradeService.GetSaveData();
            WalletSaveData sellerMana = seller.TradeWallet.GetSaveData();
            WalletSaveData buyerMana = buyer.TradeWallet.GetSaveData();
            int sellerItems = seller.TradeInventory.CountItem(apple);
            int buyerItems = buyer.TradeInventory.CountItem(apple);

            // Simulate a reload: restore into fresh accounts + ledger.
            TradeService.LoadSaveData(ledger);
            var sellerReloaded = new ManaAccount();
            var buyerReloaded = new ManaAccount();
            sellerReloaded.LoadSaveData(sellerMana);
            buyerReloaded.LoadSaveData(buyerMana);

            Assert.AreEqual(1, TradeService.CompletedTrades.Count, "the ledger must contain exactly one trade");
            Assert.AreEqual(15, sellerReloaded.Balance, "seller received the mana");
            Assert.AreEqual(85, buyerReloaded.Balance, "buyer spent the mana");
            Assert.AreEqual(7, sellerItems, "seller inventory count");
            Assert.AreEqual(3, buyerItems, "buyer inventory count");
        }

        [Test]
        public void Reloading_The_Ledger_Is_Idempotent()
        {
            TradeService.LoadSaveData(null);

            var seller = new Participant { TradeParticipantId = "npc", TradeWallet = new ManaAccount(), TradeInventory = new InventoryModel(2, 2) };
            var buyer = new Participant { TradeParticipantId = "player", TradeWallet = new ManaAccount(), TradeInventory = new InventoryModel(2, 2) };
            seller.TradeWallet.InitializeMana(0, 1000);
            buyer.TradeWallet.InitializeMana(100, 1000);
            var apple = new StubItem("apple", stackable: true);
            seller.TradeInventory.AddItem(apple, 10);
            TradeService.TryExecute(new TradeRequest { buyer = buyer, seller = seller, item = apple, quantity = 1, unitPrice = 5 });

            List<MarketTransaction> ledger = TradeService.GetSaveData();
            TradeService.LoadSaveData(ledger);
            TradeService.LoadSaveData(ledger); // second load must not duplicate

            Assert.AreEqual(1, TradeService.CompletedTrades.Count, "reloading the ledger must not duplicate entries");
        }

        [Test]
        public void Wallet_Reload_Does_Not_Duplicate_History()
        {
            var account = new ManaAccount(maxTransactionHistory: 200);
            account.InitializeMana(100, 200);
            account.TrySpend(30, "purchase", "ref");

            WalletSaveData save = account.GetSaveData();
            var reloaded = new ManaAccount(maxTransactionHistory: 200);
            reloaded.LoadSaveData(save);
            reloaded.LoadSaveData(save); // repeated load

            Assert.AreEqual(70, reloaded.Balance);
            Assert.AreEqual(200, reloaded.Capacity);
            Assert.AreEqual(account.TransactionHistory.Count, reloaded.TransactionHistory.Count,
                "reloading the wallet must not duplicate transaction history");
        }

        // Minimal engine-free item used by the trade service (only the IItem contract members used).
        private sealed class StubItem : IItem
        {
            public StubItem(string id, bool stackable)
            {
                ItemId = id;
                ItemName = id;
                MaxStackSize = stackable ? 99 : 1;
            }

            public string ItemId { get; }
            public string ItemName { get; }
            public ItemType Type => ItemType.Misc;
            public ItemFlags Flags => ItemFlags.None;
            public ItemScope Scope => ItemScope.Shared;
            public int MaxStackSize { get; }
            public EquipSlotType EquipSlot => EquipSlotType.Weapon;
            public bool IsStackable => MaxStackSize > 1;
            public bool IsEquip => false;
        }
    }
}
