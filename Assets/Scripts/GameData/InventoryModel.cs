using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Pure-data inventory container — no MonoBehaviour, no Unity dependencies, so it lives in
    /// Game.Data and can be tested without a scene.
    /// Holds a flat array of InventorySlot entries and exposes high-level operations:
    /// AddItem, RemoveItem, CanAddItem, MoveSlot, SplitStack, Sort, HasItem, CountItem.
    ///
    /// Items are held as the engine-free <see cref="IItem"/> contract; the Unity ItemData
    /// ScriptableObject implements it at runtime.
    ///
    /// Not a component — create with: new InventoryModel(rows, columns)
    ///   - InventoryUI owns and displays the player's model.
    ///   - NpcController creates one automatically when Has Inventory is enabled.
    ///
    /// Subscribe to OnChanged to react to any modification (UI refresh, autosave, etc.).
    /// Raise ForceRefresh() after externally writing slot data (e.g. after a save load).
    /// </summary>
    public class InventoryModel
    {
        public readonly int Rows;
        public readonly int Columns;
        public int SlotCount => Rows * Columns;

        private readonly InventorySlot[] slots;
        private readonly bool restrictItemScope;
        private readonly ItemScope acceptedScope;

        public event Action OnChanged;

        public InventoryModel(int rows, int columns)
        {
            Rows = rows;
            Columns = columns;
            slots = new InventorySlot[rows * columns];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new InventorySlot();
        }

        public InventoryModel(int rows, int columns, ItemScope worldScope)
            : this(rows, columns)
        {
            restrictItemScope = true;
            acceptedScope = worldScope;
        }

        public InventorySlot GetSlot(int index) => slots[index];

        /// <summary>
        /// Forces all UI subscribers to re-render from current slot state.
        /// Call after directly setting slots (e.g. on save load).
        /// </summary>
        public void ForceRefresh() => OnChanged?.Invoke();

        /// <summary>
        /// Adds items to the inventory. Returns the leftover amount that did not fit.
        /// </summary>
        public int AddItem(IItem item, int amount = 1)
        {
            if (item == null || amount <= 0) return amount;
            if (!Accepts(item)) return amount;
            int leftover = AddItemWithoutNotification(item, amount);
            OnChanged?.Invoke();
            return leftover;
        }

        /// <summary>
        /// True when the complete amount can be added without changing any slots.
        /// Used by atomic trades and other transfers that must not accept partial placement.
        /// </summary>
        public bool CanAddItem(IItem item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;
            if (!Accepts(item)) return false;
            if ((item.Flags & ItemFlags.Unique) != 0 &&
                (amount > 1 || CountItem(item) > 0))
            {
                return false;
            }

            long available = 0;
            if (item.IsStackable)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot.IsEmpty)
                        available += item.MaxStackSize;
                    else if (ReferenceEquals(slot.item, item) && slot.quantity < item.MaxStackSize)
                        available += item.MaxStackSize - slot.quantity;

                    if (available >= amount) return true;
                }
            }
            else
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].IsEmpty)
                        available++;
                    if (available >= amount) return true;
                }
            }

            return false;
        }

        public bool Accepts(IItem item)
        {
            return item != null && (!restrictItemScope ||
                   item.Scope == ItemScope.Shared ||
                   item.Scope == acceptedScope);
        }

        private int AddItemWithoutNotification(IItem item, int amount)
        {
            // Fill partial stacks first
            if (item.IsStackable)
            {
                for (int i = 0; i < slots.Length && amount > 0; i++)
                {
                    var s = slots[i];
                    if (!s.IsEmpty && ReferenceEquals(s.item, item) && s.quantity < item.MaxStackSize)
                    {
                        int space = item.MaxStackSize - s.quantity;
                        int take = Math.Min(space, amount);
                        s.quantity += take;
                        amount -= take;
                    }
                }
            }

            // Fill empty slots
            for (int i = 0; i < slots.Length && amount > 0; i++)
            {
                var s = slots[i];
                if (s.IsEmpty)
                {
                    int toAdd = item.IsStackable ? Math.Min(item.MaxStackSize, amount) : 1;
                    s.Set(item, toAdd);
                    amount -= toAdd;
                    if ((item.Flags & ItemFlags.Unique) != 0) break;
                }
            }

            return amount;
        }

        /// <summary>
        /// Removes the specified amount of an item. Returns false if not enough is held.
        /// </summary>
        public bool RemoveItem(IItem item, int amount = 1)
        {
            if (!RemoveItemWithoutNotification(item, amount)) return false;
            OnChanged?.Invoke();
            return true;
        }

        private bool RemoveItemWithoutNotification(IItem item, int amount)
        {
            if (item == null || amount <= 0 || CountItem(item) < amount) return false;
            int remaining = amount;
            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                var s = slots[i];
                if (!s.IsEmpty && ReferenceEquals(s.item, item))
                {
                    int take = Math.Min(s.quantity, remaining);
                    s.quantity -= take;
                    remaining -= take;
                    if (s.quantity <= 0) s.Clear();
                }
            }

            return true;
        }

        public bool HasItem(IItem item, int amount = 1) => CountItem(item) >= amount;

        public int CountItem(IItem item)
        {
            int total = 0;
            foreach (var s in slots)
                if (!s.IsEmpty && ReferenceEquals(s.item, item)) total += s.quantity;
            return total;
        }

        /// <summary>
        /// Commit an exact item transfer without notifying observers until both inventories have
        /// changed. Public so Game.Core.TradeService remains the authority for item-for-mana exchanges.
        /// </summary>
        public bool TryTransferItemTo(
            InventoryModel destination,
            IItem item,
            int amount,
            out InventorySnapshot sourceBefore,
            out InventorySnapshot destinationBefore)
        {
            sourceBefore = null;
            destinationBefore = null;

            if (destination == null || destination == this || item == null || amount <= 0)
                return false;
            if (CountItem(item) < amount || !destination.CanAddItem(item, amount))
                return false;

            sourceBefore = CaptureSnapshot();
            destinationBefore = destination.CaptureSnapshot();

            if (!RemoveItemWithoutNotification(item, amount))
                return false;

            int leftover = destination.AddItemWithoutNotification(item, amount);
            if (leftover == 0)
                return true;

            RestoreSnapshot(sourceBefore);
            destination.RestoreSnapshot(destinationBefore);
            return false;
        }

        public void NotifyChanged() => OnChanged?.Invoke();

        public void RestoreSnapshot(InventorySnapshot snapshot)
        {
            if (snapshot == null || snapshot.items.Length != slots.Length) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (snapshot.items[i] == null || snapshot.quantities[i] <= 0)
                    slots[i].Clear();
                else
                    slots[i].Set(snapshot.items[i], snapshot.quantities[i]);
            }
        }

        private InventorySnapshot CaptureSnapshot()
        {
            var snapshot = new InventorySnapshot(slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                snapshot.items[i] = slots[i].item;
                snapshot.quantities[i] = slots[i].quantity;
            }
            return snapshot;
        }

        /// <summary>
        /// Moves an item between slots. Merges stacks when both slots hold the same stackable item;
        /// otherwise swaps.
        /// </summary>
        public void MoveSlot(int from, int to)
        {
            if (from == to) return;
            var a = slots[from];
            var b = slots[to];

            if (!a.IsEmpty && !b.IsEmpty && ReferenceEquals(a.item, b.item) && a.item.IsStackable)
            {
                int space = a.item.MaxStackSize - b.quantity;
                int moving = Math.Min(space, a.quantity);
                b.quantity += moving;
                a.quantity -= moving;
                if (a.quantity <= 0) a.Clear();
            }
            else
            {
                (a.item, b.item) = (b.item, a.item);
                (a.quantity, b.quantity) = (b.quantity, a.quantity);
                if (a.IsEmpty) a.Clear();
                if (b.IsEmpty) b.Clear();
            }

            OnChanged?.Invoke();
        }

        /// <summary>
        /// Splits a specific amount from a stack into the first available empty slot.
        /// Returns false if there is no empty slot, the stack cannot be split, or amount is invalid.
        /// </summary>
        public bool SplitStack(int slotIndex, int amount)
        {
            var s = slots[slotIndex];
            if (s.IsEmpty || !s.item.IsStackable || s.quantity < 2) return false;
            if (amount < 1 || amount >= s.quantity) return false;

            for (int i = 0; i < slots.Length; i++)
            {
                if (i == slotIndex || !slots[i].IsEmpty) continue;
                slots[i].Set(s.item, amount);
                s.quantity -= amount;
                OnChanged?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Splits half of a stack into the first available empty slot.
        /// Returns false if there is no empty slot or the stack cannot be split.
        /// </summary>
        public bool SplitStack(int slotIndex)
        {
            var s = slots[slotIndex];
            if (s.IsEmpty || !s.item.IsStackable || s.quantity < 2) return false;

            int half = s.quantity / 2;
            return SplitStack(slotIndex, half);
        }

        /// <summary>
        /// Sorts slots by ItemType then alphabetically by name.
        /// </summary>
        public void Sort()
        {
            var items = new List<(IItem item, int qty)>();
            foreach (var s in slots)
                if (!s.IsEmpty) items.Add((s.item, s.quantity));

            items.Sort((a, b) =>
            {
                int t = a.item.Type.CompareTo(b.item.Type);
                return t != 0 ? t : string.Compare(a.item.ItemName, b.item.ItemName, StringComparison.Ordinal);
            });

            for (int i = 0; i < slots.Length; i++)
            {
                if (i < items.Count) slots[i].Set(items[i].item, items[i].qty);
                else slots[i].Clear();
            }

            OnChanged?.Invoke();
        }

        public sealed class InventorySnapshot
        {
            public readonly IItem[] items;
            public readonly int[] quantities;

            public InventorySnapshot(int slotCount)
            {
                items = new IItem[slotCount];
                quantities = new int[slotCount];
            }
        }
    }
}
