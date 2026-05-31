using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure-data inventory container — no MonoBehaviour, no Unity dependencies.
/// Holds a flat array of InventorySlot entries and exposes high-level operations:
/// AddItem, RemoveItem, MoveSlot, SplitStack, Sort, HasItem, CountItem.
///
/// Not a component — create with: new InventoryModel(rows, columns)
///   - InventoryUI owns and displays the player's model.
///   - NpcController creates one automatically when Has Inventory is enabled.
///
/// Subscribe to OnChanged to react to any modification (UI refresh, autosave, etc.).
/// Raise ForceRefresh() after externally writing slot data (e.g. after a save load).
/// </summary>
{
    public readonly int Rows;
    public readonly int Columns;
    public int SlotCount => Rows * Columns;

    private readonly InventorySlot[] slots;

    public event Action OnChanged;

    public InventoryModel(int rows, int columns)
    {
        Rows = rows;
        Columns = columns;
        slots = new InventorySlot[rows * columns];
        for (int i = 0; i < slots.Length; i++)
            slots[i] = new InventorySlot();
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
    public int AddItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0) return amount;

        // Fill partial stacks first
        if (item.IsStackable)
        {
            for (int i = 0; i < slots.Length && amount > 0; i++)
            {
                var s = slots[i];
                if (!s.IsEmpty && s.item == item && s.quantity < item.maxStackSize)
                {
                    int space = item.maxStackSize - s.quantity;
                    int take = Mathf.Min(space, amount);
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
                int toAdd = item.IsStackable ? Mathf.Min(item.maxStackSize, amount) : 1;
                s.Set(item, toAdd);
                amount -= toAdd;
                if ((item.flags & ItemFlags.Unique) != 0) break;
            }
        }

        OnChanged?.Invoke();
        return amount;
    }

    /// <summary>
    /// Removes the specified amount of an item. Returns false if not enough is held.
    /// </summary>
    public bool RemoveItem(ItemData item, int amount = 1)
    {
        if (item == null || amount <= 0 || CountItem(item) < amount) return false;

        int remaining = amount;
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            var s = slots[i];
            if (!s.IsEmpty && s.item == item)
            {
                int take = Mathf.Min(s.quantity, remaining);
                s.quantity -= take;
                remaining -= take;
                if (s.quantity <= 0) s.Clear();
            }
        }

        OnChanged?.Invoke();
        return true;
    }

    public bool HasItem(ItemData item, int amount = 1) => CountItem(item) >= amount;

    public int CountItem(ItemData item)
    {
        int total = 0;
        foreach (var s in slots)
            if (!s.IsEmpty && s.item == item) total += s.quantity;
        return total;
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

        if (!a.IsEmpty && !b.IsEmpty && a.item == b.item && a.item.IsStackable)
        {
            int space = a.item.maxStackSize - b.quantity;
            int moving = Mathf.Min(space, a.quantity);
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
    /// Splits half of a stack into the first available empty slot.
    /// Returns false if there is no empty slot or the stack cannot be split.
    /// </summary>
    public bool SplitStack(int slotIndex)
    {
        var s = slots[slotIndex];
        if (s.IsEmpty || !s.item.IsStackable || s.quantity < 2) return false;

        int half = s.quantity / 2;
        for (int i = 0; i < slots.Length; i++)
        {
            if (i == slotIndex || !slots[i].IsEmpty) continue;
            slots[i].Set(s.item, half);
            s.quantity -= half;
            OnChanged?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Sorts slots by ItemType then alphabetically by name.
    /// </summary>
    public void Sort()
    {
        var items = new List<(ItemData item, int qty)>();
        foreach (var s in slots)
            if (!s.IsEmpty) items.Add((s.item, s.quantity));

        items.Sort((a, b) =>
        {
            int t = a.item.type.CompareTo(b.item.type);
            return t != 0 ? t : string.Compare(a.item.itemName, b.item.itemName, StringComparison.Ordinal);
        });

        for (int i = 0; i < slots.Length; i++)
        {
            if (i < items.Count) slots[i].Set(items[i].item, items[i].qty);
            else slots[i].Clear();
        }

        OnChanged?.Invoke();
    }
}
