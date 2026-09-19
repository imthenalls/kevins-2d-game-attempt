using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Unity facade over the engine-free <see cref="Keyring"/>. Owns the player's key items separately
/// from the slot-based inventory so keys never consume inventory capacity. Keys are indexed by item
/// id and changes notify the keyring UI.
///
/// The key rules live in Game.Data and are unit tested without a scene; this component owns the
/// singleton lifetime and the change events only.
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
    private readonly Keyring keyring = new Keyring();
    private bool subscribed;

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
        Subscribe();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public bool HasKey(string itemId, int quantity = 1) => keyring.HasKey(itemId, quantity);

    public int CountKey(string itemId) => keyring.CountKey(itemId);

    public bool CanAddKey(ItemData item, int quantity = 1) => keyring.CanAddKey(item, quantity);

    /// <summary>Stores keys and returns the amount that could not be accepted.</summary>
    public int AddKey(ItemData item, int quantity = 1) => keyring.AddKey(item, quantity);

    public bool RemoveKey(string itemId, int quantity = 1) => keyring.RemoveKey(itemId, quantity);

    public IEnumerable<KeyValuePair<string, int>> GetEntries() => keyring.GetEntries();

    public void Clear() => keyring.Clear();

    private void Subscribe()
    {
        if (subscribed)
            return;
        subscribed = true;
        keyring.Changed += () => OnChanged?.Invoke();
    }
}
