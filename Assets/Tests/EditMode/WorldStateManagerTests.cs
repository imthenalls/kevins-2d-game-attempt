using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode unit tests for WorldStateManager.
/// Covers all flag operations, typed accessors, event firing, and snapshot behaviour.
///
/// Run via: Window → General → Test Runner → EditMode tab → Run All.
/// </summary>
[Category("WorldState")]
public class WorldStateManagerTests
{
    private GameObject         _go;
    private WorldStateManager  _wsm;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        _go  = new GameObject("WSM_TestHost");
        _wsm = _go.AddComponent<WorldStateManager>();
        // AddComponent calls Awake(), which sets the static Instance.
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_go);
        // Reset static singleton so the next test gets a clean instance.
        typeof(WorldStateManager)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, null);
    }

    // ── Boolean flag API ─────────────────────────────────────────────────────

    [Test]
    public void SetFlag_HasFlag_ReturnsTrue()
    {
        _wsm.SetFlag("TestFlag");
        Assert.IsTrue(_wsm.HasFlag("TestFlag"));
    }

    [Test]
    public void ClearFlag_AfterSet_ReturnsFalse()
    {
        _wsm.SetFlag("TestFlag");
        _wsm.ClearFlag("TestFlag");
        Assert.IsFalse(_wsm.HasFlag("TestFlag"));
    }

    [Test]
    public void HasFlag_UnknownKey_ReturnsFalse()
    {
        Assert.IsFalse(_wsm.HasFlag("DoesNotExist"));
    }

    [Test]
    public void ToggleFlag_WhenAbsent_SetsFlag()
    {
        _wsm.ToggleFlag("Toggleable");
        Assert.IsTrue(_wsm.HasFlag("Toggleable"));
    }

    [Test]
    public void ToggleFlag_WhenPresent_ClearsFlag()
    {
        _wsm.SetFlag("Toggleable");
        _wsm.ToggleFlag("Toggleable");
        Assert.IsFalse(_wsm.HasFlag("Toggleable"));
    }

    [Test]
    public void ToggleFlag_Twice_RestoredToOriginalState()
    {
        _wsm.ToggleFlag("Double");
        _wsm.ToggleFlag("Double");
        Assert.IsFalse(_wsm.HasFlag("Double"));
    }

    // ── HasFlag compatibility with legacy quest-JSON string facts ─────────────

    [Test]
    public void HasFlag_BoolTrue_ReturnsTrue()
    {
        _wsm.SetFact("F", true);
        Assert.IsTrue(_wsm.HasFlag("F"));
    }

    [Test]
    public void HasFlag_BoolFalse_ReturnsFalse()
    {
        _wsm.SetFact("F", false);
        Assert.IsFalse(_wsm.HasFlag("F"));
    }

    [Test]
    public void HasFlag_StringTrue_ReturnsTrue()
    {
        _wsm.SetFact("F", "True");    // set by quest JSON  { "type": "SetFact", "value": "True" }
        Assert.IsTrue(_wsm.HasFlag("F"));
    }

    [Test]
    public void HasFlag_StringFalse_ReturnsFalse()
    {
        _wsm.SetFact("F", "false");
        Assert.IsFalse(_wsm.HasFlag("F"));
    }

    [Test]
    public void HasFlag_StringFalseUppercase_ReturnsFalse()
    {
        _wsm.SetFact("F", "FALSE");
        Assert.IsFalse(_wsm.HasFlag("F"));
    }

    [Test]
    public void HasFlag_NonNullNonBoolValue_ReturnsTrue()
    {
        _wsm.SetFact("F", 42);
        Assert.IsTrue(_wsm.HasFlag("F"));
    }

    // ── Typed int ────────────────────────────────────────────────────────────

    [Test]
    public void SetInt_GetInt_Roundtrip()
    {
        _wsm.SetInt("Score", 99);
        Assert.AreEqual(99, _wsm.GetInt("Score"));
    }

    [Test]
    public void GetInt_MissingKey_ReturnsFallback()
    {
        Assert.AreEqual(-1, _wsm.GetInt("Missing", -1));
    }

    [Test]
    public void GetInt_StringValue_ParsesCorrectly()
    {
        _wsm.SetFact("AsString", "77");
        Assert.AreEqual(77, _wsm.GetInt("AsString"));
    }

    // ── Typed float ──────────────────────────────────────────────────────────

    [Test]
    public void SetFloat_GetFloat_Roundtrip()
    {
        _wsm.SetFloat("Speed", 2.5f);
        Assert.AreEqual(2.5f, _wsm.GetFloat("Speed"), 0.0001f);
    }

    [Test]
    public void GetFloat_MissingKey_ReturnsFallback()
    {
        Assert.AreEqual(-1f, _wsm.GetFloat("Missing", -1f), 0.0001f);
    }

    // ── Typed string ─────────────────────────────────────────────────────────

    [Test]
    public void SetString_GetString_Roundtrip()
    {
        _wsm.SetString("Name", "Thorin");
        Assert.AreEqual("Thorin", _wsm.GetString("Name"));
    }

    [Test]
    public void GetString_MissingKey_ReturnsFallback()
    {
        Assert.AreEqual("default", _wsm.GetString("Missing", "default"));
    }

    // ── OnFlagChanged event ──────────────────────────────────────────────────

    [Test]
    public void SetFlag_FiresOnFlagChanged_WithCorrectKey()
    {
        string received = null;
        void Handler(string k) => received = k;
        WorldStateManager.OnFlagChanged += Handler;

        _wsm.SetFlag("EventKey");

        WorldStateManager.OnFlagChanged -= Handler;
        Assert.AreEqual("EventKey", received);
    }

    [Test]
    public void ClearFlag_FiresOnFlagChanged_WhenKeyExisted()
    {
        _wsm.SetFlag("EventClear");
        string received = null;
        void Handler(string k) => received = k;
        WorldStateManager.OnFlagChanged += Handler;

        _wsm.ClearFlag("EventClear");

        WorldStateManager.OnFlagChanged -= Handler;
        Assert.AreEqual("EventClear", received);
    }

    [Test]
    public void ClearFlag_DoesNotFireEvent_WhenKeyAbsent()
    {
        int count = 0;
        void Handler(string k) => count++;
        WorldStateManager.OnFlagChanged += Handler;

        _wsm.ClearFlag("NeverSet");

        WorldStateManager.OnFlagChanged -= Handler;
        Assert.AreEqual(0, count);
    }

    [Test]
    public void SetFact_FiresOnFlagChanged()
    {
        string received = null;
        void Handler(string k) => received = k;
        WorldStateManager.OnFlagChanged += Handler;

        _wsm.SetFact("FactKey", "someValue");

        WorldStateManager.OnFlagChanged -= Handler;
        Assert.AreEqual("FactKey", received);
    }

    // ── Snapshot ─────────────────────────────────────────────────────────────

    [Test]
    public void GetSnapshot_ContainsSetFacts()
    {
        _wsm.SetFlag("A");
        _wsm.SetInt("B", 5);

        var snap = _wsm.GetSnapshot();

        Assert.IsTrue(snap.ContainsKey("A"));
        Assert.IsTrue(snap.ContainsKey("B"));
    }

    [Test]
    public void LoadSnapshot_RestoresFacts()
    {
        var source = new Dictionary<string, object> { { "Restored", true }, { "Count", 3 } };
        _wsm.LoadSnapshot(source);

        Assert.IsTrue(_wsm.HasFlag("Restored"));
        Assert.AreEqual(3, _wsm.GetInt("Count"));
    }

    [Test]
    public void LoadSnapshot_ClearsPreviousFacts()
    {
        _wsm.SetFlag("OldFlag");
        _wsm.LoadSnapshot(new Dictionary<string, object>());
        Assert.IsFalse(_wsm.HasFlag("OldFlag"));
    }

    [Test]
    public void LoadSnapshot_SuppressesOnFlagChanged()
    {
        int count = 0;
        void Handler(string k) => count++;
        WorldStateManager.OnFlagChanged += Handler;

        _wsm.LoadSnapshot(new Dictionary<string, object> { { "X", true }, { "Y", true } });

        WorldStateManager.OnFlagChanged -= Handler;
        Assert.AreEqual(0, count, "OnFlagChanged must not fire during LoadSnapshot.");
    }
}
