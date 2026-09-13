using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An NPC-owned key inventory. Implements <see cref="IKeyHolder"/> so doors and locks resolve
/// the interacting NPC's own keys instead of the player's. Keys are indexed by ItemData.itemId
/// and are independent of the NPC's trade inventory.
///
/// Unity setup:
///   1. Add to an NPC GameObject (typically the root, where SlidingDoor.ResolveKeyHolder can
///      find it via GetComponentInParent).
///   2. Optionally list Starting Key Ids the NPC owns on spawn. These ids should match items
///      flagged ItemFlags.KeyItem in ItemDatabase.
///   3. Grant or remove keys at runtime with AddKey / RemoveKey (for example from quest actions).
///
/// Runtime API:
///   HasKey(id, quantity), RemoveKey(id, quantity), AddKey(item/id, quantity),
///   CountKey(id), Clear(), GetEntries(), and OnKeysChanged.
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcKeyring : MonoBehaviour, IKeyHolder
{
    [Tooltip("Key item ids this NPC owns when the scene starts.")]
    [SerializeField] private List<string> startingKeyIds = new List<string>();

    private readonly Dictionary<string, int> keys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public event Action OnKeysChanged;

    private void Awake()
    {
        for (int i = 0; i < startingKeyIds.Count; i++)
        {
            string id = startingKeyIds[i];
            if (!string.IsNullOrWhiteSpace(id))
                keys[id] = CountKey(id) + 1;
        }
    }

    public bool HasKey(string itemId, int quantity = 1) =>
        quantity > 0 && !string.IsNullOrWhiteSpace(itemId) &&
        keys.TryGetValue(itemId, out int owned) && owned >= quantity;

    public int CountKey(string itemId) =>
        !string.IsNullOrWhiteSpace(itemId) && keys.TryGetValue(itemId, out int owned) ? owned : 0;

    /// <summary>Adds keys by raw id. Returns the amount that could not be accepted.</summary>
    public int AddKey(string itemId, int quantity = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
            return Mathf.Max(0, quantity);

        keys[itemId] = CountKey(itemId) + quantity;
        OnKeysChanged?.Invoke();
        return 0;
    }

    /// <summary>Adds a key item, validating the KeyItem flag and Unique rule. Returns the leftover.</summary>
    public int AddKey(ItemData item, int quantity = 1)
    {
        if (item == null || quantity <= 0 || (item.flags & ItemFlags.KeyItem) == 0)
            return quantity;
        if ((item.flags & ItemFlags.Unique) != 0 && HasKey(item.itemId))
            return quantity;

        return AddKey(item.itemId, quantity);
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
        OnKeysChanged?.Invoke();
        return true;
    }

    public IEnumerable<KeyValuePair<string, int>> GetEntries() => keys;

    public void Clear()
    {
        keys.Clear();
        OnKeysChanged?.Invoke();
    }
}
