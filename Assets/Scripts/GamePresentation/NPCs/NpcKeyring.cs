using Game.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity facade over the engine-free <see cref="Keyring"/> for an NPC. It owns the serialized
/// starting keys and the change event; all key storage and the KeyItem/Unique rules live in
/// Game.Data and are unit-tested there.
///
/// Implements <see cref="IKeyHolder"/> so doors and locks resolve the interacting NPC's own keys.
///
/// Unity setup:
///   1. Add to an NPC GameObject (typically the root).
///   2. Optionally list Starting Key Ids the NPC owns on spawn.
///   3. Grant or remove keys at runtime with AddKey / RemoveKey.
///
/// Runtime API: HasKey, RemoveKey, AddKey(item/id, quantity), CountKey, Clear, GetEntries,
/// and OnKeysChanged.
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcKeyring : MonoBehaviour, IKeyHolder
{
    [Tooltip("Key item ids this NPC owns when the scene starts.")]
    [SerializeField] private List<string> startingKeyIds = new List<string>();

    private readonly Keyring keyring = new Keyring();

    public event Action OnKeysChanged;

    private void Awake()
    {
        keyring.Changed += HandleChanged;

        for (int i = 0; i < startingKeyIds.Count; i++)
        {
            string id = startingKeyIds[i];
            if (!string.IsNullOrWhiteSpace(id))
                keyring.AddKey(id);
        }
    }

    private void OnDestroy() => keyring.Changed -= HandleChanged;

    private void HandleChanged() => OnKeysChanged?.Invoke();

    public bool HasKey(string itemId, int quantity = 1) => keyring.HasKey(itemId, quantity);

    public int CountKey(string itemId) => keyring.CountKey(itemId);

    public bool CanAddKey(ItemData item, int quantity = 1) => keyring.CanAddKey(item, quantity);

    /// <summary>Adds a key item, validating the KeyItem flag and Unique rule. Returns the leftover.</summary>
    public int AddKey(ItemData item, int quantity = 1) => keyring.AddKey(item, quantity);

    /// <summary>Adds keys by raw id. Returns the amount that could not be accepted.</summary>
    public int AddKey(string itemId, int quantity = 1) => keyring.AddKey(itemId, quantity);

    public bool RemoveKey(string itemId, int quantity = 1) => keyring.RemoveKey(itemId, quantity);

    public IEnumerable<KeyValuePair<string, int>> GetEntries() => keyring.GetEntries();

    public void Clear() => keyring.Clear();
}
