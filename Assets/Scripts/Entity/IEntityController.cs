/// <summary>
/// Shared contract for any entity that can be controlled — player or NPC.
///
/// Implemented by PlayerController2D and NpcController.
/// Use this type for fields or parameters that need to accept either,
/// so prefabs and systems don't have to know which one they're working with.
///
/// Unity setup: no component needed. Declare variables as IEntityController when code
/// must work for both the player and an NPC (e.g. dialogue targets, AI targets, cutscenes).
///
/// Note: Stats and CombatReceiver may be null on non-combat entities (e.g. generic NPCs).
/// Always null-check before use.
/// </summary>
public interface IEntityController
{
    /// <summary>Human-readable name. Falls back to the GameObject name if not set.</summary>
    string DisplayName { get; }

    /// <summary>HP/MP component. Non-null on the player and on Enemy NPCs.</summary>
    EntityStats Stats { get; }

    /// <summary>Combat layer component. Non-null on the player (if added) and on Enemy NPCs.</summary>
    CombatReceiver CombatReceiver { get; }

    /// <summary>Whether this entity is currently allowed to move.</summary>
    bool MovementEnabled { get; }

    /// <summary>Lock or unlock movement. Used by dialogue, cutscenes, combat stuns, etc.</summary>
    void SetMovementEnabled(bool enabled);
}
