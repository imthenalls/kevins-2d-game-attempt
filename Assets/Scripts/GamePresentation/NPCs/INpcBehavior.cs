/// <summary>
/// Implement this interface on a MonoBehaviour to create an NPC behavior
/// that can be driven by NpcBehaviorManager.
/// </summary>
public interface INpcBehavior
{
    /// <summary>Relative chance this behavior is chosen (0–100).</summary>
    float Weight { get; }

    /// <summary>Called once when this behavior becomes active.</summary>
    void OnEnter();

    /// <summary>Called every frame while this behavior is active.</summary>
    void Tick();

    /// <summary>Called once when this behavior is deactivated.</summary>
    void OnExit();

    /// <summary>Return true to signal the manager to pick the next behavior.</summary>
    bool IsComplete();
}
