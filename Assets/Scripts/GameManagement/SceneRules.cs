using UnityEngine;

/// <summary>
/// ScriptableObject defining per-scene gameplay rules applied by SceneRulesManager.
/// Each field is a toggle or parameter that overrides default behaviour while the player
/// is in the scene that owns this asset.
///
/// Unity setup: none — this is a data asset only.
///   Create via: Assets → Create → Game → Scene Rules
///   Then assign the asset to the Scene Rules Manager in the relevant scene.
/// </summary>
[CreateAssetMenu(menuName = "Game/Scene Rules", fileName = "SceneRules_New")]
public class SceneRules : ScriptableObject
{
    [Header("Inventory")]
    [Tooltip("Prevent the player from opening or toggling the inventory in this scene.")]
    public bool lockInventory = false;

    [Tooltip("Prevent the game from saving while in this scene.")]
    public bool disableSaving = false;

    [Header("Player Combat")]
    [Tooltip("The player cannot take damage in this scene (sets Invincible on their CombatReceiver).")]
    public bool playerInvincible = false;

    [Tooltip("Multiplies all damage the player receives. 2.0 = double, 0.5 = half. 1.0 = no change.")]
    [Min(0f)] public float playerDamageMultiplier = 1f;

    [Tooltip("The player cannot attack in this scene (disables their CombatAttacker component).")]
    public bool disablePlayerAttack = false;

    [Header("NPC Combat")]
    [Tooltip("Enemy NPCs cannot take damage in this scene.")]
    public bool enemiesInvincible = false;

    [Tooltip("Multiplies all damage enemy NPCs receive. 2.0 = double, 0.5 = half. 1.0 = no change.")]
    [Min(0f)] public float enemyDamageMultiplier = 1f;

    [Tooltip("Enemy NPCs cannot attack in this scene (disables their CombatAttacker components).")]
    public bool disableNpcAttack = false;

    [Header("Damage Over Time")]
    [Tooltip("Repeatedly deal damage to affected entities on a fixed interval.")]
    public bool dotEnabled = false;

    [Tooltip("Damage dealt per tick.")]
    [Min(1)] public int dotDamagePerTick = 5;

    [Tooltip("Seconds between damage ticks.")]
    [Min(0.1f)] public float dotInterval = 2f;

    [Tooltip("DOT damages the player.")]
    public bool dotAffectsPlayer = true;

    [Tooltip("DOT damages enemy NPCs.")]
    public bool dotAffectsEnemies = false;

    [Header("Heal Over Time")]
    [Tooltip("Repeatedly restore HP to affected entities on a fixed interval.")]
    public bool hotEnabled = false;

    [Tooltip("HP restored per tick.")]
    [Min(1)] public int hotHealPerTick = 5;

    [Tooltip("Seconds between heal ticks.")]
    [Min(0.1f)] public float hotInterval = 2f;

    [Tooltip("HOT heals the player.")]
    public bool hotAffectsPlayer = true;

    [Tooltip("HOT heals enemy NPCs.")]
    public bool hotAffectsEnemies = false;

    [Header("Movement")]
    [Tooltip("The player cannot move in this scene.")]
    public bool lockPlayerMovement = false;

    [Tooltip("Multiplies the player's move speed. 0.5 = half speed, 2.0 = double speed. 1.0 = no change.")]
    [Min(0f)] public float playerSpeedMultiplier = 1f;

    [Tooltip("All NPCs are frozen in this scene.")]
    public bool lockNpcMovement = false;

    [Header("NPC Behavior")]
    [Tooltip("Override the behavior state of every NPC in this scene.")]
    public bool forceNpcBehaviorState = false;

    [Tooltip("The state to force on all NPCs when forceNpcBehaviorState is enabled.")]
    public NpcBehaviorState forcedNpcState = NpcBehaviorState.Idle;

    [Tooltip("Disable all NpcDialogue components — NPCs cannot be talked to in this scene.")]
    public bool disableNpcDialogue = false;

    [Tooltip("Disable all PortalTrigger2D components — the player cannot use portals in this scene.")]
    public bool blockPortals = false;

    [Tooltip("Multiplies the aggro range of all enemy NPCs. 2.0 = twice the detection radius, 0.25 = near-blind. 1.0 = no change.")]
    [Min(0f)] public float npcAggroRangeMultiplier = 1f;

    [Header("Player Death")]
    [Tooltip("What happens when the player dies in this scene.")]
    public PlayerDeathBehavior playerDeathBehavior = PlayerDeathBehavior.Default;

    [Tooltip("Scene to load when playerDeathBehavior is set to SendToScene.")]
    public string deathScene = "";
}

public enum PlayerDeathBehavior
{
    Default,         // do nothing — let existing subscribers handle it
    RespawnInPlace,  // restore player HP to full and stay in the scene
    SendToScene,     // load deathScene via SceneLoader
    GameOver         // load the "GameOver" scene
}
