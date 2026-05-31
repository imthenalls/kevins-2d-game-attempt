using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies per-scene gameplay rules defined in a <see cref="SceneRules"/> ScriptableObject.
/// Supports three layers of control applied in priority order (highest first):
///
///   1. Individual Setters  — direct calls from scripts, items, quests, etc.
///   2. Zone Overrides      — pushed/popped at runtime by <see cref="RuleZone2D"/>; top of stack wins.
///   3. Base Rules          — SceneRules asset assigned in Inspector; applied at scene load.
///
/// Unity setup:
///   1. Create a SceneRules asset (Assets → Create → Game → Scene Rules).
///   2. Add SceneRulesManager to a manager GameObject in the scene.
///   3. Assign the SceneRules asset to the Rules field in the Inspector.
///   4. Optionally add <see cref="RuleZone2D"/> trigger zones or call setters at runtime.
///
/// Runtime API (call from items, quests, triggers):
///   SceneRulesManager.Instance.PushOverride(rulesAsset);
///   SceneRulesManager.Instance.PopOverride(rulesAsset);
///   SceneRulesManager.Instance.SetInventoryLocked(true);
///   SceneRulesManager.Instance.SetPlayerInvincible(false);
/// </summary>
[DisallowMultipleComponent]
public class SceneRulesManager : MonoBehaviour
{
    public static SceneRulesManager Instance { get; private set; }

    [SerializeField] private SceneRules rules;

    // ── Cached references ────────────────────────────────────────────────────

    private PlayerController2D            _player;
    private CombatReceiver                _playerReceiver;
    private CombatAttacker                _playerAttacker;

    private readonly List<CombatReceiver> _npcReceivers = new();
    private readonly List<CombatAttacker> _npcAttackers = new();
    private readonly List<NpcController>  _npcs         = new();
    private readonly List<NpcDialogue>    _npcDialogues = new();
    private readonly List<PortalTrigger2D> _portals      = new();

    private Coroutine _dotCoroutine;
    private Coroutine _hotCoroutine;
    private float     _originalPlayerSpeed;
    private bool      _deathHandlerSubscribed;

    private readonly Dictionary<NpcController, NpcBehaviorState> _originalNpcStates  = new();
    private readonly Dictionary<NpcController, float>            _originalAggroRanges = new();

    // ── Override stack ───────────────────────────────────────────────────────

    private readonly List<SceneRules> _overrideStack = new();

    /// <summary>Top of the override stack, or the base rules if the stack is empty.</summary>
    private SceneRules EffectiveRules =>
        _overrideStack.Count > 0 ? _overrideStack[_overrideStack.Count - 1] : rules;

    /// <summary>Snapshot of which rules are currently applied — used by RestoreRules.</summary>
    private SceneRules _appliedRules;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (rules == null)
        {
            Debug.LogWarning("[SceneRulesManager] No SceneRules asset assigned — no rules will be applied.");
            return;
        }

        GatherReferences();
        _appliedRules = EffectiveRules;
        ApplyRules();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_appliedRules != null)
            RestoreRules();
    }

    // ── Override stack API ───────────────────────────────────────────────────

    /// <summary>
    /// Push a SceneRules asset onto the override stack.
    /// Its rules immediately replace the current active rules until popped.
    /// </summary>
    public void PushOverride(SceneRules overrideRules)
    {
        if (overrideRules == null) return;
        RestoreRules();
        _overrideStack.Add(overrideRules);
        _appliedRules = EffectiveRules;
        ApplyRules();
    }

    /// <summary>
    /// Remove a specific SceneRules asset from the override stack.
    /// The next item down (or the base rules) becomes active immediately.
    /// Supports out-of-order removal — only removes the first matching entry.
    /// </summary>
    public void PopOverride(SceneRules overrideRules)
    {
        if (overrideRules == null) return;
        if (!_overrideStack.Remove(overrideRules)) return;
        RestoreRules();
        _appliedRules = EffectiveRules;
        ApplyRules();
    }

    // ── Individual setters ───────────────────────────────────────────────────
    // These bypass the rule stack entirely and directly set component state.
    // The caller is responsible for reversing changes when the effect ends.

    /// <summary>Lock or unlock the inventory panel from player input.</summary>
    public void SetInventoryLocked(bool locked)
    {
        if (InventoryUI.Instance == null) return;
        if (locked) InventoryUI.Instance.Close();
        InventoryUI.Instance.InputLocked = locked;
    }

    /// <summary>Enable or disable saving. When false, SaveManager.Save() is a no-op.</summary>
    public void SetSavingEnabled(bool enabled)
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveEnabled = enabled;
    }

    /// <summary>Toggle whether the player takes damage.</summary>
    public void SetPlayerInvincible(bool invincible)
    {
        if (_playerReceiver != null)
            _playerReceiver.Invincible = invincible;
    }

    /// <summary>Toggle the player's ability to attack.</summary>
    public void SetPlayerAttackEnabled(bool enabled)
    {
        if (_playerAttacker != null)
            _playerAttacker.enabled = enabled;
    }

    /// <summary>Toggle the player's ability to move.</summary>
    public void SetPlayerMovementEnabled(bool enabled)
    {
        _player?.SetMovementEnabled(enabled);
    }

    /// <summary>Toggle whether all enemy NPCs take damage.</summary>
    public void SetEnemiesInvincible(bool invincible)
    {
        foreach (var r in _npcReceivers)
            r.Invincible = invincible;
    }

    /// <summary>Toggle attack on all NPC CombatAttackers in the scene.</summary>
    public void SetNpcAttackEnabled(bool enabled)
    {
        foreach (var a in _npcAttackers)
            a.enabled = enabled;
    }

    /// <summary>Toggle movement on all NPCs in the scene.</summary>
    public void SetNpcMovementEnabled(bool enabled)
    {
        foreach (var npc in _npcs)
            npc.SetMovementEnabled(enabled);
    }

    /// <summary>Toggle NpcDialogue components on all NPCs in the scene.</summary>
    public void SetNpcDialogueEnabled(bool enabled)
    {
        foreach (var d in _npcDialogues)
            d.enabled = enabled;
    }

    /// <summary>Block or unblock all PortalTrigger2D components in the scene.</summary>
    public void SetPortalsBlocked(bool blocked)
    {
        foreach (var p in _portals)
            p.enabled = !blocked;
    }

    /// <summary>
    /// Start or stop the damage-over-time tick.
    /// Parameters (damage, interval, targets) are read from the current effective rules.
    /// </summary>
    public void SetDotEnabled(bool enabled)
    {
        if (enabled && _dotCoroutine == null && _appliedRules != null)
            _dotCoroutine = StartCoroutine(DotTick());
        else if (!enabled && _dotCoroutine != null)
        {
            StopCoroutine(_dotCoroutine);
            _dotCoroutine = null;
        }
    }

    /// <summary>
    /// Start or stop the heal-over-time tick.
    /// Parameters (heal, interval, targets) are read from the current effective rules.
    /// </summary>
    public void SetHotEnabled(bool enabled)
    {
        if (enabled && _hotCoroutine == null && _appliedRules != null)
            _hotCoroutine = StartCoroutine(HotTick());
        else if (!enabled && _hotCoroutine != null)
        {
            StopCoroutine(_hotCoroutine);
            _hotCoroutine = null;
        }
    }

    // ── Reference gathering ──────────────────────────────────────────────────

    private void GatherReferences()
    {
        _player         = FindFirstObjectByType<PlayerController2D>();
        _playerReceiver = _player != null ? _player.CombatReceiver                 : null;
        _playerAttacker = _player != null ? _player.GetComponent<CombatAttacker>() : null;

        if (_player != null)
            _originalPlayerSpeed = _player.MoveSpeed;

        foreach (var npc in FindObjectsByType<NpcController>(FindObjectsSortMode.None))
        {
            _npcs.Add(npc);

            if (npc.CombatReceiver != null)
                _npcReceivers.Add(npc.CombatReceiver);

            var attacker = npc.GetComponent<CombatAttacker>();
            if (attacker != null)
                _npcAttackers.Add(attacker);

            var dialogue = npc.GetComponent<NpcDialogue>();
            if (dialogue != null)
                _npcDialogues.Add(dialogue);
        }

        foreach (var portal in FindObjectsByType<PortalTrigger2D>(FindObjectsSortMode.None))
            _portals.Add(portal);
    }

    // ── Apply ────────────────────────────────────────────────────────────────

    private void ApplyRules()
    {
        var r = _appliedRules;
        if (r == null) return;

        // --- Inventory -------------------------------------------------------
        if (r.lockInventory && InventoryUI.Instance != null)
        {
            InventoryUI.Instance.Close();
            InventoryUI.Instance.InputLocked = true;
        }

        // --- Saving ----------------------------------------------------------
        if (r.disableSaving && SaveManager.Instance != null)
            SaveManager.Instance.SaveEnabled = false;

        // --- Player combat ---------------------------------------------------
        if (_playerReceiver != null)
        {
            _playerReceiver.Invincible       = r.playerInvincible;
            _playerReceiver.DamageMultiplier = r.playerDamageMultiplier;
        }

        if (_playerAttacker != null)
            _playerAttacker.enabled = !r.disablePlayerAttack;

        // --- NPC combat ------------------------------------------------------
        foreach (var rec in _npcReceivers)
        {
            rec.Invincible       = r.enemiesInvincible;
            rec.DamageMultiplier = r.enemyDamageMultiplier;
        }

        foreach (var a in _npcAttackers)
            a.enabled = !r.disableNpcAttack;

        // --- Movement --------------------------------------------------------
        if (r.lockPlayerMovement && _player != null)
            _player.SetMovementEnabled(false);

        if (_player != null && r.playerSpeedMultiplier != 1f)
            _player.MoveSpeed = _originalPlayerSpeed * r.playerSpeedMultiplier;

        if (r.lockNpcMovement)
            foreach (var npc in _npcs)
                npc.SetMovementEnabled(false);

        // --- NPC behavior state ----------------------------------------------
        if (r.forceNpcBehaviorState)
        {
            foreach (var npc in _npcs)
            {
                _originalNpcStates[npc] = npc.BehaviorState;
                npc.SetBehaviorState(r.forcedNpcState);
            }
        }

        // --- NPC dialogue ----------------------------------------------------
        if (r.disableNpcDialogue)
            foreach (var d in _npcDialogues)
                d.enabled = false;

        // --- Portal blocking -------------------------------------------------
        if (r.blockPortals)
            foreach (var p in _portals)
                p.enabled = false;

        // --- NPC aggro range -------------------------------------------------
        if (r.npcAggroRangeMultiplier != 1f)
        {
            foreach (var npc in _npcs)
            {
                if (npc.NpcType == NpcType.Enemy)
                {
                    _originalAggroRanges[npc] = npc.AggroRange;
                    npc.AggroRange             = npc.AggroRange * r.npcAggroRangeMultiplier;
                }
            }
        }

        // --- Player death ----------------------------------------------------
        if (_playerReceiver != null && r.playerDeathBehavior != PlayerDeathBehavior.Default)
        {
            _playerReceiver.OnDeath += HandlePlayerDeath;
            _deathHandlerSubscribed  = true;
        }

        // --- Damage over time ------------------------------------------------
        if (r.dotEnabled)
            _dotCoroutine = StartCoroutine(DotTick());

        // --- Heal over time --------------------------------------------------
        if (r.hotEnabled)
            _hotCoroutine = StartCoroutine(HotTick());
    }

    // ── Restore ──────────────────────────────────────────────────────────────

    private void RestoreRules()
    {
        var r = _appliedRules;

        // Inventory
        if (InventoryUI.Instance != null)
            InventoryUI.Instance.InputLocked = false;

        // Saving
        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveEnabled = true;

        // Player combat
        if (_playerReceiver != null)
        {
            _playerReceiver.Invincible       = false;
            _playerReceiver.DamageMultiplier = 1f;
        }

        if (_playerAttacker != null)
            _playerAttacker.enabled = true;

        // NPC combat
        foreach (var rec in _npcReceivers)
        {
            rec.Invincible       = false;
            rec.DamageMultiplier = 1f;
        }

        foreach (var a in _npcAttackers)
            a.enabled = true;

        // Movement — only undo what was actually locked by the applied rules
        if (r != null && r.lockPlayerMovement && _player != null)
            _player.SetMovementEnabled(true);

        if (_player != null && r != null && r.playerSpeedMultiplier != 1f)
            _player.MoveSpeed = _originalPlayerSpeed;

        if (r != null && r.lockNpcMovement)
            foreach (var npc in _npcs)
                npc.SetMovementEnabled(true);

        // NPC behavior state
        foreach (var kvp in _originalNpcStates)
            kvp.Key.SetBehaviorState(kvp.Value);
        _originalNpcStates.Clear();

        // NPC dialogue
        foreach (var d in _npcDialogues)
            d.enabled = true;

        // Portals
        foreach (var p in _portals)
            p.enabled = true;

        // NPC aggro range
        foreach (var kvp in _originalAggroRanges)
            kvp.Key.AggroRange = kvp.Value;
        _originalAggroRanges.Clear();

        // Player death
        if (_playerReceiver != null && _deathHandlerSubscribed)
        {
            _playerReceiver.OnDeath -= HandlePlayerDeath;
            _deathHandlerSubscribed  = false;
        }

        // DOT / HOT
        if (_dotCoroutine != null) { StopCoroutine(_dotCoroutine); _dotCoroutine = null; }
        if (_hotCoroutine != null) { StopCoroutine(_hotCoroutine); _hotCoroutine = null; }
    }

    // ── Damage over time ─────────────────────────────────────────────────────

    private IEnumerator DotTick()
    {
        var r    = _appliedRules;
        var wait = new WaitForSeconds(r.dotInterval);

        while (true)
        {
            yield return wait;

            if (r.dotAffectsPlayer && _playerReceiver != null)
                _playerReceiver.ReceiveHit(new DamageInfo(r.dotDamagePerTick, null));

            if (r.dotAffectsEnemies)
                foreach (var rec in _npcReceivers)
                    rec.ReceiveHit(new DamageInfo(r.dotDamagePerTick, null));
        }
    }

    // ── Heal over time ───────────────────────────────────────────────────────

    private IEnumerator HotTick()
    {
        var r    = _appliedRules;
        var wait = new WaitForSeconds(r.hotInterval);

        while (true)
        {
            yield return wait;

            if (r.hotAffectsPlayer && _player != null)
                _player.Stats?.Heal(r.hotHealPerTick);

            if (r.hotAffectsEnemies)
                foreach (var npc in _npcs)
                    npc.Stats?.Heal(r.hotHealPerTick);
        }
    }

    // ── Player death ──────────────────────────────────────────────────────

    private void HandlePlayerDeath(CombatReceiver _)
    {
        switch (_appliedRules.playerDeathBehavior)
        {
            case PlayerDeathBehavior.RespawnInPlace:
                _player.Stats?.Heal(_player.Stats.MaxHp);
                break;

            case PlayerDeathBehavior.SendToScene:
                if (!string.IsNullOrEmpty(_appliedRules.deathScene))
                    SceneLoader.Instance?.LoadScene(_appliedRules.deathScene);
                break;

            case PlayerDeathBehavior.GameOver:
                SceneLoader.Instance?.LoadScene("GameOver");
                break;
        }
    }
}
