using System;

/// <summary>
/// Contract for anything that can own and spend key items. Doors and other locks resolve this
/// from the interacting entity, so a key check works for the player or an NPC without knowing
/// which one it is.
///
/// Unity setup: none — this is an interface. Implement it on a MonoBehaviour:
///   - PlayerKeyring (the persistent player singleton) already implements it.
///   - Add an NPC-side implementation (for example NpcKeyring) to let NPCs carry keys.
///
/// Runtime API:
///   holder.HasKey(id, quantity)   — true when the holder owns the key.
///   holder.RemoveKey(id, quantity) — spends the key; false when missing.
///   holder.OnKeysChanged          — fires on add / remove / clear.
/// </summary>
public interface IKeyHolder
{
    /// <summary>True when the holder owns at least <paramref name="quantity"/> of the key id.</summary>
    bool HasKey(string itemId, int quantity = 1);

    /// <summary>Removes the key when owned. Returns false when it is missing.</summary>
    bool RemoveKey(string itemId, int quantity = 1);

    /// <summary>Fires whenever the held keys change (add, remove, or clear).</summary>
    event Action OnKeysChanged;
}
