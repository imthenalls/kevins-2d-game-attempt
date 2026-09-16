using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global key-value store for all persistent game state.
/// Quest conditions read from here; quest actions write to here.
/// World State Components (WorldStateActivator, WorldStateDestroyer, etc.) subscribe to
/// OnFlagChanged to react immediately when any flag changes at runtime.
/// Survives scene changes via DontDestroyOnLoad.
///
/// Values are stored as objects and compared as strings by FactCondition.
/// Supported value types: bool, int, float, string.
///
/// Add to a GameObject in your first/bootstrap scene (one instance only).
///
/// Runtime API:
///   WorldStateManager.Instance.SetFlag("Boss.GoblinKing.Defeated");
///   WorldStateManager.Instance.HasFlag("Bridge.Fixed");
///   WorldStateManager.Instance.SetInt("Player.Reputation", 10);
///   WorldStateManager.OnFlagChanged += key => Debug.Log(key + " changed");
/// </summary>
[DisallowMultipleComponent]
public class WorldStateManager : MonoBehaviour
{
    private static WorldStateManager _instance;
    public static WorldStateManager Instance => _instance;

    private readonly Dictionary<string, object> _facts = new();

    /// <summary>
    /// Fired whenever any fact is set, cleared, or toggled.
    /// Passes the key that changed. Not fired during LoadSnapshot (bulk restore).
    /// Subscribe in OnEnable, unsubscribe in OnDisable.
    /// </summary>
    public static event Action<string> OnFlagChanged;

    // Suppresses OnFlagChanged during bulk snapshot restore to avoid
    // spurious reactions while scene objects are not yet initialized.
    private bool _suppressEvents;

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

    // -------------------------------------------------------------------------
    // Core fact API  (raw object values — used by quest system)
    // -------------------------------------------------------------------------

    /// <summary>Write or overwrite a fact and fire OnFlagChanged.</summary>
    public void SetFact(string key, object value)
    {
        _facts[key] = value;
        if (!_suppressEvents) OnFlagChanged?.Invoke(key);
    }

    /// <summary>Read a fact. Returns null if not present.</summary>
    public object GetFact(string key) => _facts.TryGetValue(key, out var v) ? v : null;

    /// <summary>True if the key has ever been set (regardless of value).</summary>
    public bool HasFact(string key) => _facts.ContainsKey(key);

    /// <summary>Remove a fact entry and fire OnFlagChanged.</summary>
    public void ClearFact(string key)
    {
        if (_facts.Remove(key) && !_suppressEvents)
            OnFlagChanged?.Invoke(key);
    }

    // -------------------------------------------------------------------------
    // Boolean flag API  (primary API for World State Components)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Mark a flag as set. Equivalent to SetFact(key, true).
    /// HasFlag will return true until ClearFlag is called.
    /// </summary>
    public void SetFlag(string key) => SetFact(key, true);

    /// <summary>Remove a flag so HasFlag returns false.</summary>
    public void ClearFlag(string key) => ClearFact(key);

    /// <summary>
    /// Toggle a flag: sets it if absent, clears it if present.
    /// </summary>
    public void ToggleFlag(string key)
    {
        if (HasFlag(key)) ClearFlag(key);
        else              SetFlag(key);
    }

    /// <summary>
    /// True if the key exists and its value is truthy.
    /// Accepts bool true, string "true" (case-insensitive), or any non-null value
    /// that is not bool false or string "false". Compatible with SetFact("key","True")
    /// written by the quest system's SetFact action.
    /// </summary>
    public bool HasFlag(string key)
    {
        if (!_facts.TryGetValue(key, out var v)) return false;
        if (v is bool  b) return b;
        if (v is string s)
            return !string.Equals(s, "false", StringComparison.OrdinalIgnoreCase);
        return v != null;
    }

    // -------------------------------------------------------------------------
    // Typed convenience accessors
    // -------------------------------------------------------------------------

    /// <summary>Store an integer value.</summary>
    public void SetInt(string key, int value) => SetFact(key, value);

    /// <summary>Read an integer value. Returns <paramref name="fallback"/> if not present or wrong type.</summary>
    public int GetInt(string key, int fallback = 0)
    {
        if (!_facts.TryGetValue(key, out var v)) return fallback;
        if (v is int   i) return i;
        if (v is string s && int.TryParse(s, out int parsed)) return parsed;
        return fallback;
    }

    /// <summary>Store a float value.</summary>
    public void SetFloat(string key, float value) => SetFact(key, value);

    /// <summary>Read a float value. Returns <paramref name="fallback"/> if not present or wrong type.</summary>
    public float GetFloat(string key, float fallback = 0f)
    {
        if (!_facts.TryGetValue(key, out var v)) return fallback;
        if (v is float f) return f;
        if (v is string s && float.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float parsed)) return parsed;
        return fallback;
    }

    /// <summary>Store a string value.</summary>
    public void SetString(string key, string value) => SetFact(key, value);

    /// <summary>Read a string value. Returns <paramref name="fallback"/> if not present.</summary>
    public string GetString(string key, string fallback = "")
    {
        if (!_facts.TryGetValue(key, out var v)) return fallback;
        return v != null ? v.ToString() : fallback;
    }

    // -------------------------------------------------------------------------
    // Save / load support
    // -------------------------------------------------------------------------

    /// <summary>Returns a shallow copy of the facts dictionary for serialization.</summary>
    public Dictionary<string, object> GetSnapshot() => new(_facts);

    /// <summary>
    /// Restores facts from a previously captured snapshot.
    /// OnFlagChanged is suppressed during restore — world state components
    /// read current state in their own Start() after the scene loads.
    /// </summary>
    public void LoadSnapshot(Dictionary<string, object> snapshot)
    {
        _suppressEvents = true;
        _facts.Clear();
        foreach (var kv in snapshot)
            _facts[kv.Key] = kv.Value;
        _suppressEvents = false;
    }
}
