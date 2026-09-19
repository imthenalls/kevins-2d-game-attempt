using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Engine-free key-item store: the player's keys, indexed by item id, kept separate from the
    /// slot-based inventory so keys never consume capacity. Uniqueness follows the item's Unique
    /// flag and only KeyItem-flagged items are accepted.
    ///
    /// This is the authoritative key state. The Unity <c>PlayerKeyring</c> MonoBehaviour is a thin
    /// facade (singleton + IKeyHolder/OnChanged events) forwarding to an instance of this class, so
    /// key rules can be tested without a scene.
    ///
    /// Runtime API: HasKey, CountKey, CanAddKey, AddKey, RemoveKey, GetEntries, Clear.
    /// </summary>
    public sealed class Keyring
    {
        private readonly Dictionary<string, int> keys =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised after any successful add, remove, or clear.</summary>
        public event Action Changed;

        /// <summary>True when the keyring holds at least <paramref name="quantity"/> of the key.</summary>
        public bool HasKey(string itemId, int quantity = 1) =>
            quantity > 0 && !string.IsNullOrWhiteSpace(itemId) &&
            keys.TryGetValue(itemId, out int owned) && owned >= quantity;

        /// <summary>Number of copies of the key currently held (0 when absent).</summary>
        public int CountKey(string itemId) =>
            !string.IsNullOrWhiteSpace(itemId) && keys.TryGetValue(itemId, out int owned) ? owned : 0;

        /// <summary>
        /// True when the key can be accepted: it must be a KeyItem, and a Unique key may only be
        /// held once.
        /// </summary>
        public bool CanAddKey(IItem item, int quantity = 1)
        {
            if (item == null || quantity <= 0 || (item.Flags & ItemFlags.KeyItem) == 0)
                return false;
            return (item.Flags & ItemFlags.Unique) == 0 || (!HasKey(item.ItemId) && quantity == 1);
        }

        /// <summary>Stores keys and returns the amount that could not be accepted.</summary>
        public int AddKey(IItem item, int quantity = 1)
        {
            if (!CanAddKey(item, quantity))
                return quantity;

            keys[item.ItemId] = CountKey(item.ItemId) + quantity;
            Changed?.Invoke();
            return 0;
        }

        /// <summary>Removes the given quantity, returning false when it is not held.</summary>
        public bool RemoveKey(string itemId, int quantity = 1)
        {
            if (!HasKey(itemId, quantity))
                return false;

            int remaining = keys[itemId] - quantity;
            if (remaining > 0)
                keys[itemId] = remaining;
            else
                keys.Remove(itemId);

            Changed?.Invoke();
            return true;
        }

        /// <summary>Snapshot of the held keys (id → quantity).</summary>
        public List<KeyValuePair<string, int>> GetEntries() =>
            new List<KeyValuePair<string, int>>(keys);

        /// <summary>Remove every key.</summary>
        public void Clear()
        {
            keys.Clear();
            Changed?.Invoke();
        }
    }
}
