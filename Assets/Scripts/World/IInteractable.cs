using UnityEngine;

/// <summary>
/// Contract for any world object the player can interact with by pressing the interact key.
/// Implement on a MonoBehaviour, place the GameObject on the Interactable physics layer,
/// and PlayerInteractionController will discover and drive it automatically.
///
/// Unity setup: none — this is an interface. See WorldObject for the built-in implementation.
/// </summary>
public interface IInteractable
{
    /// <summary>Returns true when the interactor is close enough and interaction is available.</summary>
    bool CanInteract(Vector3 worldPosition);

    /// <summary>Display name shown in the dialogue box header while the player is reading.</summary>
    string GetDisplayName();

    /// <summary>
    /// Fills <paramref name="line"/> with the current page of text and returns true.
    /// Returns false when all lines have been shown — the controller will then call EndInteraction.
    /// </summary>
    bool TryGetCurrentLine(out string line);

    /// <summary>Advance to the next line. Called after the player confirms the current one.</summary>
    void Advance();

    /// <summary>
    /// Called once when the full interaction sequence finishes (rewards, events, one-time disable, etc.).
    /// Also called when the player cancels mid-way.
    /// </summary>
    void EndInteraction(GameObject interactor);
}
