using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Unity facade over the engine-free <see cref="WorldFacts"/> model. Global key-value store for all
/// persistent game state: quest conditions read from here, quest actions write to here, and world
/// state components subscribe to <see cref="OnFlagChanged"/> to react immediately.
///
/// The fact logic lives in Game.Data (and is unit tested without a scene); this component owns the
/// singleton lifetime and the static change event only.
///
/// Values are stored as objects and compared as strings by fact conditions.
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

    private readonly WorldFacts facts = new WorldFacts();

    /// <summary>
    /// Fired whenever any fact is set, cleared, or toggled. Passes the key that changed.
    /// Not fired during LoadSnapshot (bulk restore).
    /// Subscribe in OnEnable, unsubscribe in OnDisable.
    /// </summary>
    public static event Action<string> OnFlagChanged;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        facts.Changed += key => OnFlagChanged?.Invoke(key);
        DontDestroyOnLoad(gameObject);
    }

    // -------------------------------------------------------------------------
    // Core fact API  (raw object values — used by quest system)
    // -------------------------------------------------------------------------

    /// <summary>Write or overwrite a fact and fire OnFlagChanged.</summary>
    public void SetFact(string key, object value) => facts.SetFact(key, value);

    /// <summary>Read a fact. Returns null if not present.</summary>
    public object GetFact(string key) => facts.GetFact(key);

    /// <summary>True if the key has ever been set (regardless of value).</summary>
    public bool HasFact(string key) => facts.HasFact(key);

    /// <summary>Remove a fact entry and fire OnFlagChanged.</summary>
    public void ClearFact(string key) => facts.ClearFact(key);

    // -------------------------------------------------------------------------
    // Boolean flag API  (primary API for World State Components)
    // -------------------------------------------------------------------------

    /// <summary>Mark a flag as set. Equivalent to SetFact(key, true).</summary>
    public void SetFlag(string key) => facts.SetFlag(key);

    /// <summary>Remove a flag so HasFlag returns false.</summary>
    public void ClearFlag(string key) => facts.ClearFlag(key);

    /// <summary>Toggle a flag: sets it if absent, clears it if present.</summary>
    public void ToggleFlag(string key) => facts.ToggleFlag(key);

    /// <summary>True if the key exists and its value is truthy.</summary>
    public bool HasFlag(string key) => facts.HasFlag(key);

    // -------------------------------------------------------------------------
    // Typed convenience accessors
    // -------------------------------------------------------------------------

    /// <summary>Store an integer value.</summary>
    public void SetInt(string key, int value) => facts.SetInt(key, value);

    /// <summary>Read an integer value. Returns the fallback if not present or the wrong type.</summary>
    public int GetInt(string key, int fallback = 0) => facts.GetInt(key, fallback);

    /// <summary>Store a float value.</summary>
    public void SetFloat(string key, float value) => facts.SetFloat(key, value);

    /// <summary>Read a float value. Returns the fallback if not present or the wrong type.</summary>
    public float GetFloat(string key, float fallback = 0f) => facts.GetFloat(key, fallback);

    /// <summary>Store a string value.</summary>
    public void SetString(string key, string value) => facts.SetString(key, value);

    /// <summary>Read a string value. Returns the fallback if not present.</summary>
    public string GetString(string key, string fallback = "") => facts.GetString(key, fallback);

    // -------------------------------------------------------------------------
    // Save / load support
    // -------------------------------------------------------------------------

    /// <summary>Returns a shallow copy of the facts dictionary for serialization.</summary>
    public Dictionary<string, object> GetSnapshot() => facts.GetSnapshot();

    /// <summary>
    /// Restores facts from a previously captured snapshot. OnFlagChanged is suppressed during
    /// restore — world state components read current state in their own Start() after scene load.
    /// </summary>
    public void LoadSnapshot(Dictionary<string, object> snapshot) => facts.LoadSnapshot(snapshot);
}
