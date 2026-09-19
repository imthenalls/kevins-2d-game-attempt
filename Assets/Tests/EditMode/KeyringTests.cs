using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the authoritative keyring model that the Unity PlayerKeyring facade
    /// forwards to.
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class KeyringTests
    {
        private sealed class FakeItem : IItem
        {
            public string ItemId { get; }
            public string ItemName => ItemId;
            public ItemType Type => ItemType.Misc;
            public ItemFlags Flags { get; }
            public ItemScope Scope => ItemScope.Shared;
            public int MaxStackSize => 1;
            public bool IsStackable => false;
            public bool IsEquip => false;
            public EquipSlotType EquipSlot => EquipSlotType.Weapon;

            public FakeItem(string id, ItemFlags flags)
            {
                ItemId = id;
                Flags = flags;
            }
        }

        [Test]
        public void Only_KeyItems_Are_Accepted()
        {
            var keyring = new Keyring();
            var ordinary = new FakeItem("apple", ItemFlags.None);
            var key = new FakeItem("golden_key", ItemFlags.KeyItem);

            Assert.IsFalse(keyring.CanAddKey(ordinary, 1));
            Assert.AreEqual(1, keyring.AddKey(ordinary, 1));
            Assert.AreEqual(0, keyring.CountKey("apple"));

            Assert.IsTrue(keyring.CanAddKey(key, 1));
            Assert.AreEqual(0, keyring.AddKey(key, 1));
            Assert.IsTrue(keyring.HasKey("golden_key"));
        }

        [Test]
        public void Unique_Key_Can_Only_Be_Held_Once()
        {
            var keyring = new Keyring();
            var uniqueKey = new FakeItem("arena_key", ItemFlags.KeyItem | ItemFlags.Unique);

            Assert.AreEqual(0, keyring.AddKey(uniqueKey, 1));
            Assert.AreEqual(1, keyring.AddKey(uniqueKey, 1), "a second copy must be refused");
            Assert.AreEqual(1, keyring.CountKey("arena_key"));
            Assert.IsFalse(keyring.CanAddKey(uniqueKey, 2));
        }

        [Test]
        public void Remove_And_Clear_Track_Ownership()
        {
            var keyring = new Keyring();
            var key = new FakeItem("token", ItemFlags.KeyItem | ItemFlags.Unique);
            keyring.AddKey(key, 1);

            Assert.IsTrue(keyring.RemoveKey("token"));
            Assert.IsFalse(keyring.HasKey("token"));
            Assert.IsFalse(keyring.RemoveKey("token"));
        }

        [Test]
        public void Id_Lookup_Is_Case_Insensitive()
        {
            var keyring = new Keyring();
            var key = new FakeItem("Golden_Key", ItemFlags.KeyItem | ItemFlags.Unique);
            keyring.AddKey(key, 1);

            Assert.IsTrue(keyring.HasKey("golden_key"));
            Assert.AreEqual(1, keyring.CountKey("GOLDEN_KEY"));
        }

        [Test]
        public void GetEntries_Returns_A_Snapshot_And_Changed_Fires()
        {
            var keyring = new Keyring();
            int changes = 0;
            keyring.Changed += () => changes++;

            keyring.AddKey(new FakeItem("a", ItemFlags.KeyItem | ItemFlags.Unique), 1);
            keyring.AddKey(new FakeItem("b", ItemFlags.KeyItem | ItemFlags.Unique), 1);
            List<KeyValuePair<string, int>> entries = keyring.GetEntries();

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(2, changes);

            keyring.Clear();
            Assert.AreEqual(0, keyring.GetEntries().Count);
            Assert.AreEqual(3, changes);
        }
    }
}
