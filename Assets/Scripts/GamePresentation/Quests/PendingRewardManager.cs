using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Unity adapter for pending quest rewards. The reward identities, merging, remaining quantities,
/// and save snapshot live in the engine-free <see cref="PendingRewardLedger"/> owned by
/// <see cref="GameSession"/>; this component only resolves <see cref="ItemData"/> and bridges to
/// <see cref="InventoryHelper.GiveItem"/>. Delivery respects keyring routing and world restrictions.
///
/// Unity setup: none. A persistent instance is created on first use; SaveManager restores its state.
///
/// Runtime API:
///   GrantReward(questId, itemId, quantity) — deliver now, retain any leftover.
///   ClaimPending() — retry delivery of everything still pending; returns the total delivered.
///   HasPendingRewards, Pending — query/status.
/// </summary>
[DisallowMultipleComponent]
public sealed class PendingRewardManager : MonoBehaviour
{
    public static PendingRewardManager Instance { get; private set; }

    private PendingRewardLedger ledger;

    public IReadOnlyList<PendingRewardEntry> Pending => ledger.Pending;
    public bool HasPendingRewards => ledger.HasPendingRewards;

    public event Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (Instance != null)
            return;
        var go = new GameObject("Pending Reward Manager");
        DontDestroyOnLoad(go);
        go.AddComponent<PendingRewardManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        GameSessionHost.EnsureExists();
        ledger = GameSessionHost.Session.PendingRewards;
        ledger.Changed += HandleChanged;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (ledger != null)
            ledger.Changed -= HandleChanged;
        Instance = null;
    }

    private void HandleChanged() => OnChanged?.Invoke();

    /// <summary>
    /// Delivers a quest reward now, retaining any undelivered quantity as a pending reward.
    /// A reward is identified by quest + item, so repeated grants accumulate rather than duplicating.
    /// </summary>
    public void GrantReward(string questId, string itemId, int quantity)
    {
        if (quantity <= 0 || string.IsNullOrWhiteSpace(itemId))
            return;

        ItemData item = Resolve(itemId);
        if (item == null)
        {
            Debug.LogWarning($"[PendingRewardManager] Unknown reward item '{itemId}'.");
            return;
        }

        int taken = InventoryHelper.GiveItem(item, quantity);
        int leftover = quantity - taken;
        if (leftover <= 0)
            return;

        ledger.Record(questId, itemId, leftover);
        Debug.Log($"[PendingRewardManager] {leftover}x '{itemId}' pending (inventory full).");
    }

    /// <summary>
    /// Records an already-undelivered remainder as a pending reward. Callers that deliver what they
    /// can (e.g. WorldObject) use this so a full inventory cannot lose the rest of a reward.
    /// Returns true when the remainder is accounted for (including a non-positive quantity), or
    /// false when there is nothing to record it with.
    /// </summary>
    public bool RecordLeftover(string rewardId, string itemId, int quantity)
    {
        if (quantity <= 0)
            return true;
        if (ledger == null || string.IsNullOrWhiteSpace(itemId))
            return false;

        ledger.Record(rewardId, itemId, quantity);
        Debug.Log($"[PendingRewardManager] {quantity}x '{itemId}' pending (inventory full).");
        return true;
    }

    /// <summary>Retries delivery of every pending reward and returns the total delivered.</summary>
    public int ClaimPending()
    {
        PendingRewardClaimResult result = ledger.Claim(Deliver);
        return result.Delivered;
    }

    private static PendingRewardDelivery Deliver(string itemId, int requested)
    {
        ItemData item = Resolve(itemId);
        if (item == null)
        {
            Debug.LogWarning($"[PendingRewardManager] Unknown pending item '{itemId}'; dropping.");
            return PendingRewardDelivery.Unknown();
        }

        return PendingRewardDelivery.Accepted(InventoryHelper.GiveItem(item, requested));
    }

    private static ItemData Resolve(string itemId)
    {
        ItemData item = ItemDatabase.Instance != null ? ItemDatabase.Instance.Get(itemId) : null;
        if (item == null)
            item = Resources.Load<ItemData>(itemId);
        return item;
    }

    // ── Save / load ──────────────────────────────────────────────────────────

    public List<PendingRewardEntry> GetSaveData() => ledger.Snapshot();

    public void LoadSaveData(List<PendingRewardEntry> entries) => ledger.Load(entries);
}
