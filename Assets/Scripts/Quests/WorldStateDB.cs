using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global key-value store for all persistent game state.
/// Quest conditions read from here; quest actions write to here.
/// Survives scene changes via DontDestroyOnLoad.
///
/// Values are stored as objects and compared as strings by FactCondition.
/// Supported value types: bool, int, float, string.
///
/// Add to a GameObject in your first/bootstrap scene (one instance only).
/// </summary>
[DisallowMultipleComponent]
public class WorldStateDB : MonoBehaviour
{
    private static WorldStateDB _instance;
    public static WorldStateDB Instance => _instance;

    private readonly Dictionary<string, object> _facts = new();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Write or overwrite a fact.</summary>
    public void SetFact(string key, object value) => _facts[key] = value;

    /// <summary>Read a fact. Returns null if not present.</summary>
    public object GetFact(string key) => _facts.TryGetValue(key, out var v) ? v : null;

    /// <summary>True if the key has ever been set.</summary>
    public bool HasFact(string key) => _facts.ContainsKey(key);

    /// <summary>Remove a fact entry.</summary>
    public void ClearFact(string key) => _facts.Remove(key);

    // -------------------------------------------------------------------------
    // Save / load support
    // -------------------------------------------------------------------------

    /// <summary>Returns a shallow copy of the facts dictionary for serialization.</summary>
    public Dictionary<string, object> GetSnapshot() => new(_facts);

    /// <summary>Restores facts from a previously captured snapshot.</summary>
    public void LoadSnapshot(Dictionary<string, object> snapshot)
    {
        _facts.Clear();
        foreach (var kv in snapshot)
            _facts[kv.Key] = kv.Value;
    }
}
