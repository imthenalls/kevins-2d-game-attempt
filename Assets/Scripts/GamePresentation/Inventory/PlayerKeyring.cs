using Game.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the player's key items separately from the slot-based inventory so keys never consume
/// inventory capacity. Keys are indexed by ItemData.itemId and changes notify the keyring UI.
///
/// Unity setup:
///   1. No manual setup is required; InventoryUI adds this component to its persistent object.
///   2. Items must include the ItemFlags.KeyItem flag to enter the keyring.
///
/// Runtime API:
///   PlayerKeyring.Instance.HasKey(id), AddKey(item, quantity), RemoveKey(id, quantity),
///   Clear(), GetEntries(), and OnChanged.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerKeyring : MonoBehaviour, IKeyHolder
{
    private static PlayerKeyring instance;
    private readonly Dictionary<string, int> keys = new(StringComparer.OrdinalIgnoreCase);

    public static PlayerKeyring Instance => instance;
    public event Action OnChanged;

    // IKeyHolder forwards to the existing OnChanged event so keyring UI keeps working.
    event Action IKeyHolder.OnKeysChanged
    {
        add => OnChanged += value;
        remove => OnChanged -= value;
    }

    /// <summary>Finds or adds the persistent player keyring to the supplied owner.</summary>
    public static PlayerKeyring GetOrCreate(GameObject owner = null)
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<PlayerKeyring>();
        if (instance != null)
            return instance;

        owner ??= InventoryUI.Instance != null ? InventoryUI.Instance.gameObject : null;
        if (owner == null)
        {
            owner = new GameObject("Player Keyring");
            DontDestroyOnLoad(owner);
        }

        return owner.AddComponent<PlayerKeyring>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public bool HasKey(string itemId, int quantity = 1) =>
        quantity > 0 && !string.IsNullOrWhiteSpace(itemId) &&
        keys.TryGetValue(itemId, out int owned) && owned >= quantity;

    public int CountKey(string itemId) =>
        !string.IsNullOrWhiteSpace(itemId) && keys.TryGetValue(itemId, out int owned) ? owned : 0;

    public bool CanAddKey(ItemData item, int quantity = 1)
    {
        if (item == null || quantity <= 0 || (item.flags & ItemFlags.KeyItem) == 0)
            return false;
        return (item.flags & ItemFlags.Unique) == 0 || (!HasKey(item.itemId) && quantity == 1);
    }

    /// <summary>Stores keys and returns the amount that could not be accepted.</summary>
    public int AddKey(ItemData item, int quantity = 1)
    {
        if (!CanAddKey(item, quantity))
            return quantity;

        keys[item.itemId] = CountKey(item.itemId) + quantity;
        OnChanged?.Invoke();
        return 0;
    }

    public bool RemoveKey(string itemId, int quantity = 1)
    {
        if (!HasKey(itemId, quantity))
            return false;

        int remaining = keys[itemId] - quantity;
        if (remaining > 0)
            keys[itemId] = remaining;
        else
            keys.Remove(itemId);
        OnChanged?.Invoke();
        return true;
    }

    public IEnumerable<KeyValuePair<string, int>> GetEntries() => keys;

    public void Clear()
    {
        keys.Clear();
        OnChanged?.Invoke();
    }
}
