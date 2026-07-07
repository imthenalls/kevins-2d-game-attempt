using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Edit Mode tests for World State scene components:
/// WorldStateActivator, WorldStateDestroyer, and WorldStateInteractable.
///
/// Uses [UnityTest] with yield return null where Unity's lifecycle (Start, Destroy)
/// must process before assertions can be made.
///
/// Run via: Window → General → Test Runner → EditMode tab → Run All.
/// </summary>
[Category("WorldState")]
public class WorldStateComponentTests
{
    private GameObject        _managerGo;
    private WorldStateManager _wsm;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    [SetUp]
    public void SetUp()
    {
        _managerGo = new GameObject("WSM_TestHost");
        _wsm       = _managerGo.AddComponent<WorldStateManager>();
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up any remaining test GameObjects.
        foreach (var go in Object.FindObjectsByType<WorldStateActivator>(FindObjectsSortMode.None))
            Object.DestroyImmediate(go.gameObject);
        foreach (var go in Object.FindObjectsByType<WorldStateDestroyer>(FindObjectsSortMode.None))
            Object.DestroyImmediate(go.gameObject);
        foreach (var go in Object.FindObjectsByType<WorldStateInteractable>(FindObjectsSortMode.None))
            Object.DestroyImmediate(go.gameObject);

        Object.DestroyImmediate(_managerGo);
        typeof(WorldStateManager)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, null);
    }

    // ── WorldStateActivator: event-driven (no Start() needed) ─────────────────

    [Test]
    public void Activator_WhenFlagSetAfterSubscription_EnablesTarget()
    {
        var go        = new GameObject("Activator");
        var target    = new GameObject("Target");
        target.SetActive(false);

        var activator = go.AddComponent<WorldStateActivator>();
        SetPrivateField(activator, "flagKey",  null);
        SetPrivateField(activator, "rawKey",   "Test.ActivatorFlag");
        SetPrivateField(activator, "invert",   false);
        SetPrivateField(activator, "target",   target);

        // OnEnable has subscribed to OnFlagChanged — setting the flag triggers Refresh().
        _wsm.SetFlag("Test.ActivatorFlag");

        Assert.IsTrue(target.activeSelf);

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(target);
    }

    [Test]
    public void Activator_WhenFlagClearedAfterSet_DisablesTarget()
    {
        var go     = new GameObject("Activator");
        var target = new GameObject("Target");

        var activator = go.AddComponent<WorldStateActivator>();
        SetPrivateField(activator, "flagKey", null);
        SetPrivateField(activator, "rawKey",  "Test.ActivatorClear");
        SetPrivateField(activator, "invert",  false);
        SetPrivateField(activator, "target",  target);

        _wsm.SetFlag("Test.ActivatorClear");
        Assert.IsTrue(target.activeSelf);

        _wsm.ClearFlag("Test.ActivatorClear");
        Assert.IsFalse(target.activeSelf);

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(target);
    }

    [Test]
    public void Activator_Invert_DisablesTargetWhenFlagIsSet()
    {
        var go     = new GameObject("Activator");
        var target = new GameObject("Target");

        var activator = go.AddComponent<WorldStateActivator>();
        SetPrivateField(activator, "flagKey", null);
        SetPrivateField(activator, "rawKey",  "Test.InvertFlag");
        SetPrivateField(activator, "invert",  true);
        SetPrivateField(activator, "target",  target);

        _wsm.SetFlag("Test.InvertFlag");
        Assert.IsFalse(target.activeSelf);

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(target);
    }

    // ── WorldStateActivator: Start() dependent (initial scene-load state) ─────

    [UnityTest]
    public IEnumerator Activator_OnStart_EnablesTargetWhenFlagAlreadySet()
    {
        _wsm.SetFlag("Test.PreexistingFlag");

        var go     = new GameObject("Activator");
        var target = new GameObject("Target");
        target.SetActive(false);

        var activator = go.AddComponent<WorldStateActivator>();
        SetPrivateField(activator, "flagKey", null);
        SetPrivateField(activator, "rawKey",  "Test.PreexistingFlag");
        SetPrivateField(activator, "invert",  false);
        SetPrivateField(activator, "target",  target);

        yield return null; // allow Start() to run

        Assert.IsTrue(target.activeSelf);

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(target);
    }

    [UnityTest]
    public IEnumerator Activator_OnStart_LeavesTargetInactiveWhenFlagAbsent()
    {
        var go     = new GameObject("Activator");
        var target = new GameObject("Target");

        var activator = go.AddComponent<WorldStateActivator>();
        SetPrivateField(activator, "flagKey", null);
        SetPrivateField(activator, "rawKey",  "Test.AbsentFlag");
        SetPrivateField(activator, "invert",  false);
        SetPrivateField(activator, "target",  target);

        yield return null;

        Assert.IsFalse(target.activeSelf);

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(target);
    }

    // ── WorldStateDestroyer ──────────────────────────────────────────────────

    [UnityTest]
    public IEnumerator Destroyer_OnStart_DestroysWhenFlagAlreadySet()
    {
        _wsm.SetFlag("Test.DestroyFlag");

        var go = new GameObject("Victim");
        go.AddComponent<WorldStateDestroyer>();
        SetPrivateField(go.GetComponent<WorldStateDestroyer>(), "flagKey", null);
        SetPrivateField(go.GetComponent<WorldStateDestroyer>(), "rawKey",  "Test.DestroyFlag");

        yield return null; // allow Start() and Destroy() to process

        Assert.IsTrue(go == null, "GameObject should have been destroyed.");
    }

    [UnityTest]
    public IEnumerator Destroyer_OnFlagSet_DestroysObject()
    {
        var go = new GameObject("Victim");
        go.AddComponent<WorldStateDestroyer>();
        SetPrivateField(go.GetComponent<WorldStateDestroyer>(), "flagKey", null);
        SetPrivateField(go.GetComponent<WorldStateDestroyer>(), "rawKey",  "Test.DestroyRuntime");

        yield return null; // Start() runs — flag absent, no destroy yet
        Assert.IsFalse(go == null, "Should still exist before flag is set.");

        _wsm.SetFlag("Test.DestroyRuntime");

        yield return null; // Destroy() processes

        Assert.IsTrue(go == null, "Should be destroyed after flag was set.");
    }

    // ── WorldStateInteractable ───────────────────────────────────────────────

    [Test]
    public void Interactable_EnablesGuardedComponent_WhenFlagSet()
    {
        var go        = new GameObject("Interactable");
        var worldObj  = go.AddComponent<WorldObject>(); // implements IInteractable; starts enabled
        worldObj.enabled = false;

        var gate = go.AddComponent<WorldStateInteractable>();
        SetPrivateField(gate, "flagKey",           null);
        SetPrivateField(gate, "rawKey",            "Test.GateFlag");
        SetPrivateField(gate, "requireFlagAbsent", false);
        SetPrivateField(gate, "guardedComponents", new MonoBehaviour[] { worldObj });

        _wsm.SetFlag("Test.GateFlag");

        Assert.IsTrue(worldObj.enabled);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void Interactable_DisablesGuardedComponent_WhenFlagNotSet()
    {
        var go       = new GameObject("Interactable");
        var worldObj = go.AddComponent<WorldObject>();

        var gate = go.AddComponent<WorldStateInteractable>();
        SetPrivateField(gate, "flagKey",           null);
        SetPrivateField(gate, "rawKey",            "Test.GateFlagOff");
        SetPrivateField(gate, "requireFlagAbsent", false);
        SetPrivateField(gate, "guardedComponents", new MonoBehaviour[] { worldObj });

        // Flag not set — fire a Clear to ensure event path runs Refresh
        _wsm.ClearFlag("Test.GateFlagOff");

        Assert.IsFalse(worldObj.enabled);

        Object.DestroyImmediate(go);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Set a private serialized field by name using reflection.</summary>
    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(target, value);
    }
}
