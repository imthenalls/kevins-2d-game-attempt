using Game.Core;
using UnityEngine;

/// <summary>
/// Shared base for the player controller in every presentation dimension (2D top-down and 3D
/// planar-isometric). Systems that only need to talk to "the player" — inventory, save, world
/// travel, scene rules, bootstrap — depend on this type instead of a concrete controller, so the
/// same systems work in 2D and 3D scenes.
///
/// Implementations:
///   • <see cref="PlayerController2D"/> — Rigidbody2D on the XY plane (legacy scenes).
///   • <see cref="PlayerController3D"/> — Rigidbody on the XZ plane (3D isometric scenes).
///
/// Unity setup: this is an abstract class; add a concrete subclass, never this type.
///
/// Runtime API: DisplayName, Stats, ManaWallet, CombatReceiver, MovementEnabled,
///   SetMovementEnabled, MoveSpeed, ApplyAvatarProfile, CapturePositionModel, ApplyPositionModel.
/// </summary>
[RequireComponent(typeof(EntityStats))]
public abstract class PlayerControllerBase : MonoBehaviour, IEntityController, ITradeParticipant
{
    /// <summary>HP/MP component. Non-null: the base requires EntityStats.</summary>
    public abstract EntityStats Stats { get; }

    /// <summary>Canonical mana account used for trading and spellcasting.</summary>
    public abstract Wallet ManaWallet { get; }

    /// <summary>Combat layer component, or null when the player cannot receive damage.</summary>
    public abstract CombatReceiver CombatReceiver { get; }

    /// <summary>Whether movement is currently allowed (dialogue/inventory/cutscenes lock it).</summary>
    public abstract bool MovementEnabled { get; }

    /// <summary>Movement speed in units/s. R/W delegates to the controller's tuning.</summary>
    public abstract float MoveSpeed { get; set; }

    /// <summary>Locks or unlocks player movement.</summary>
    public abstract void SetMovementEnabled(bool enabled);

    /// <summary>Applies movement/dash tuning from the active world's avatar profile.</summary>
    public abstract void ApplyAvatarProfile(PlayerAvatarProfile profile);

    /// <summary>Mirrors the physics transform into the authoritative session position model.</summary>
    public abstract void CapturePositionModel();

    /// <summary>Applies the authoritative session position model back onto the physics body.</summary>
    public abstract void ApplyPositionModel();

    /// <summary>Human-readable name. Falls back to the GameObject name.</summary>
    public virtual string DisplayName => gameObject.name;

    public virtual string TradeParticipantId => "player";
    public virtual ManaAccount TradeWallet => ManaWallet != null ? ManaWallet.AccountModel : null;
    public virtual InventoryModel TradeInventory => InventoryUI.Model;
}
