using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Holds quest rewards that could not be delivered because the inventory was full, and lets the
/// player claim them once space is available. Delivery reuses <see cref="InventoryHelper.GiveItem"/>
/// so keyring routing and item world restrictions are respected. Pending rewards survive save/load.
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

    private readonly List<PendingRewardEntry> _pending = new();

    public IReadOnlyList<PendingRewardEntry> Pending => _pending;
    public bool HasPendingRewards => _pending.Count > 0;

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
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

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

        PendingRewardEntry entry = _pending.Find(p => p.questId == questId && p.itemId == itemId);
        if (entry == null)
        {
            entry = new PendingRewardEntry
            {
                rewardId = string.IsNullOrWhiteSpace(questId) ? itemId : questId + ":" + itemId,
                questId = questId,
                itemId = itemId,
                remaining = 0,
            };
            _pending.Add(entry);
        }
        entry.remaining += leftover;
        Debug.Log($"[PendingRewardManager] {leftover}x '{itemId}' pending (inventory full).");
        OnChanged?.Invoke();
    }

    /// <summary>Retries delivery of every pending reward and returns the total delivered.</summary>
    public int ClaimPending()
    {
        int delivered = 0;
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            PendingRewardEntry entry = _pending[i];
            ItemData item = Resolve(entry.itemId);
            if (item == null)
            {
                Debug.LogWarning($"[PendingRewardManager] Unknown pending item '{entry.itemId}'; dropping.");
                _pending.RemoveAt(i);
                continue;
            }

            int taken = InventoryHelper.GiveItem(item, entry.remaining);
            entry.remaining -= taken;
            delivered += taken;
            if (entry.remaining <= 0)
                _pending.RemoveAt(i);
        }

        if (delivered > 0)
            OnChanged?.Invoke();
        return delivered;
    }

    private static ItemData Resolve(string itemId)
    {
        ItemData item = ItemDatabase.Instance != null ? ItemDatabase.Instance.Get(itemId) : null;
        if (item == null)
            item = Resources.Load<ItemData>(itemId);
        return item;
    }

    // ── Save / load ──────────────────────────────────────────────────────────

    public List<PendingRewardEntry> GetSaveData()
    {
        var result = new List<PendingRewardEntry>(_pending.Count);
        foreach (PendingRewardEntry entry in _pending)
        {
            result.Add(new PendingRewardEntry
            {
                rewardId = entry.rewardId,
                questId = entry.questId,
                itemId = entry.itemId,
                remaining = entry.remaining,
            });
        }
        return result;
    }

    public void LoadSaveData(List<PendingRewardEntry> entries)
    {
        _pending.Clear();
        if (entries == null)
            return;
        foreach (PendingRewardEntry entry in entries)
        {
            if (entry == null || entry.remaining <= 0 || string.IsNullOrWhiteSpace(entry.itemId))
                continue;
            _pending.Add(new PendingRewardEntry
            {
                rewardId = entry.rewardId,
                questId = entry.questId,
                itemId = entry.itemId,
                remaining = entry.remaining,
            });
        }
        OnChanged?.Invoke();
    }
}
