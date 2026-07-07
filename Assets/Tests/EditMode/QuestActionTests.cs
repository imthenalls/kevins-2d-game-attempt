using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode unit tests for the IQuestAction implementations that write to WorldStateManager:
/// SetFactAction, ClearFlagAction, and ToggleFlagAction.
///
/// Run via: Window → General → Test Runner → EditMode tab → Run All.
/// </summary>
[Category("WorldState")]
public class QuestActionTests
{
    private GameObject        _go;
    private WorldStateManager _wsm;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        _go  = new GameObject("WSM_TestHost");
        _wsm = _go.AddComponent<WorldStateManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_go);
        typeof(WorldStateManager)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, null);
    }

    // ── SetFactAction ────────────────────────────────────────────────────────

    [Test]
    public void SetFactAction_WritesValueToManager()
    {
        new SetFactAction("Quest.Done", "True").Execute();
        Assert.IsTrue(_wsm.HasFlag("Quest.Done"));
    }

    [Test]
    public void SetFactAction_OverwritesExistingValue()
    {
        _wsm.SetFact("Key", "Old");
        new SetFactAction("Key", "New").Execute();
        Assert.AreEqual("New", _wsm.GetString("Key"));
    }

    // ── ClearFlagAction ──────────────────────────────────────────────────────

    [Test]
    public void ClearFlagAction_RemovesExistingKey()
    {
        _wsm.SetFlag("FlagToClear");
        new ClearFlagAction("FlagToClear").Execute();
        Assert.IsFalse(_wsm.HasFlag("FlagToClear"));
    }

    [Test]
    public void ClearFlagAction_OnAbsentKey_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => new ClearFlagAction("NeverSet").Execute());
    }

    // ── ToggleFlagAction ─────────────────────────────────────────────────────

    [Test]
    public void ToggleFlagAction_SetsAbsentFlag()
    {
        new ToggleFlagAction("Toggle.A").Execute();
        Assert.IsTrue(_wsm.HasFlag("Toggle.A"));
    }

    [Test]
    public void ToggleFlagAction_ClearsPresentFlag()
    {
        _wsm.SetFlag("Toggle.B");
        new ToggleFlagAction("Toggle.B").Execute();
        Assert.IsFalse(_wsm.HasFlag("Toggle.B"));
    }

    [Test]
    public void ToggleFlagAction_Twice_RestoredToOriginalState()
    {
        new ToggleFlagAction("Toggle.C").Execute();
        new ToggleFlagAction("Toggle.C").Execute();
        Assert.IsFalse(_wsm.HasFlag("Toggle.C"));
    }

    [Test]
    public void ToggleFlagAction_FiresOnFlagChangedEvent()
    {
        int count = 0;
        void Handler(string k) => count++;
        WorldStateManager.OnFlagChanged += Handler;

        new ToggleFlagAction("Toggle.D").Execute();

        WorldStateManager.OnFlagChanged -= Handler;
        Assert.AreEqual(1, count);
    }
}
