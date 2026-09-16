using UnityEngine;

/// <summary>
/// ScriptableObject that represents a single named world state key.
/// Assign to any World State component instead of typing raw strings so that
/// keys are discoverable in the Project window, refactor-safe, and impossible to typo.
///
/// Unity setup:
///   1. Right-click in the Project window → Create → Game → World State Key.
///   2. Name the asset after its key (e.g. "WSKey_Boss.GoblinKing.Defeated").
///   3. Type the exact key string in the Key field.
///   4. Drag this asset into the "Flag Key" field on any World State component.
///
/// Naming convention (recommended):
///   Category.Subject.State
///   Examples: Boss.GoblinKing.Defeated  |  Quest.Main.001Completed
///             Village.BlacksmithUnlocked |  Bridge.Fixed  |  Merchant.Rescued
///
/// Runtime API:
///   // Implicit conversion — pass a WorldStateKey wherever a string is expected:
///   string key = myWorldStateKey;
///   WorldStateManager.Instance.SetFlag(myWorldStateKey);
/// </summary>
[CreateAssetMenu(menuName = "Game/World State Key", fileName = "WSKey_New")]
public class WorldStateKey : ScriptableObject
{
    [Tooltip("The exact string stored in WorldStateManager. Use dot-notation: Category.Subject.State")]
    [SerializeField] private string key;

    /// <summary>The raw string key stored in WorldStateManager.</summary>
    public string Key => key;

    /// <summary>
    /// Implicit conversion so WorldStateKey assets can be passed directly wherever
    /// a string key is expected without calling .Key explicitly.
    /// Returns an empty string when the asset reference is null.
    /// </summary>
    public static implicit operator string(WorldStateKey asset) =>
        asset != null ? asset.key : string.Empty;
}
