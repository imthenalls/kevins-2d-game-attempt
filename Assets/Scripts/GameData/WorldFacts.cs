using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Core
{
    /// <summary>
    /// Engine-free key-value store for persistent world state. Quest conditions read from here and
    /// quest actions write to here. Values are stored as objects and compared as strings by fact
    /// conditions; supported value types are bool, int, float, and string.
    ///
    /// This is the authoritative world-state model. The Unity <c>WorldStateManager</c> MonoBehaviour
    /// is a thin facade (singleton + static change event) that forwards to an instance of this class,
    /// so world-state logic can be tested without a scene.
    ///
    /// Runtime API: SetFact, GetFact, HasFact, ClearFact, SetFlag, ClearFlag, ToggleFlag, HasFlag,
    /// SetInt / GetInt, SetFloat / GetFloat, SetString / GetString, GetSnapshot, LoadSnapshot.
    /// </summary>
    public sealed class WorldFacts
    {
        private readonly Dictionary<string, object> facts = new Dictionary<string, object>();

        /// <summary>Fired whenever a fact is set or cleared. Not fired during LoadSnapshot.</summary>
        public event Action<string> Changed;

        /// <summary>Suppresses <see cref="Changed"/> during bulk snapshot restore.</summary>
        public bool SuppressEvents { get; set; }

        /// <summary>Write or overwrite a fact and raise <see cref="Changed"/>.</summary>
        public void SetFact(string key, object value)
        {
            facts[key] = value;
            if (!SuppressEvents)
                Changed?.Invoke(key);
        }

        /// <summary>Read a fact. Returns null if not present.</summary>
        public object GetFact(string key) => facts.TryGetValue(key, out object value) ? value : null;

        /// <summary>True if the key has ever been set (regardless of value).</summary>
        public bool HasFact(string key) => facts.ContainsKey(key);

        /// <summary>Remove a fact entry and raise <see cref="Changed"/>.</summary>
        public void ClearFact(string key)
        {
            if (facts.Remove(key) && !SuppressEvents)
                Changed?.Invoke(key);
        }

        /// <summary>Mark a flag as set. Equivalent to SetFact(key, true).</summary>
        public void SetFlag(string key) => SetFact(key, true);

        /// <summary>Remove a flag so HasFlag returns false.</summary>
        public void ClearFlag(string key) => ClearFact(key);

        /// <summary>Toggle a flag: sets it if absent, clears it if present.</summary>
        public void ToggleFlag(string key)
        {
            if (HasFlag(key))
                ClearFlag(key);
            else
                SetFlag(key);
        }

        /// <summary>
        /// True if the key exists and its value is truthy. Accepts bool true, string "true"
        /// (case-insensitive), or any non-null value that is not bool false or string "false".
        /// </summary>
        public bool HasFlag(string key)
        {
            if (!facts.TryGetValue(key, out object value))
                return false;
            if (value is bool boolean)
                return boolean;
            if (value is string text)
                return !string.Equals(text, "false", StringComparison.OrdinalIgnoreCase);
            return value != null;
        }

        /// <summary>Store an integer value.</summary>
        public void SetInt(string key, int value) => SetFact(key, value);

        /// <summary>Read an integer value, returning the fallback when absent or the wrong type.</summary>
        public int GetInt(string key, int fallback = 0)
        {
            if (!facts.TryGetValue(key, out object value))
                return fallback;
            if (value is int integer)
                return integer;
            if (value is string text && int.TryParse(text, out int parsed))
                return parsed;
            return fallback;
        }

        /// <summary>Store a float value.</summary>
        public void SetFloat(string key, float value) => SetFact(key, value);

        /// <summary>Read a float value, returning the fallback when absent or the wrong type.</summary>
        public float GetFloat(string key, float fallback = 0f)
        {
            if (!facts.TryGetValue(key, out object value))
                return fallback;
            if (value is float number)
                return number;
            if (value is string text && float.TryParse(
                    text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                return parsed;
            return fallback;
        }

        /// <summary>Store a string value.</summary>
        public void SetString(string key, string value) => SetFact(key, value);

        /// <summary>Read a string value, returning the fallback when absent.</summary>
        public string GetString(string key, string fallback = "")
        {
            if (!facts.TryGetValue(key, out object value))
                return fallback;
            return value != null ? value.ToString() : fallback;
        }

        /// <summary>Returns a shallow copy of the facts dictionary for serialization.</summary>
        public Dictionary<string, object> GetSnapshot() => new Dictionary<string, object>(facts);

        /// <summary>Restores facts from a snapshot with change events suppressed.</summary>
        public void LoadSnapshot(Dictionary<string, object> snapshot)
        {
            SuppressEvents = true;
            facts.Clear();
            if (snapshot != null)
            {
                foreach (KeyValuePair<string, object> pair in snapshot)
                    facts[pair.Key] = pair.Value;
            }
            SuppressEvents = false;
        }
    }
}
