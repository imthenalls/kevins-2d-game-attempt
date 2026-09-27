using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Engine-free authority for quest rewards that could not be fully delivered because the
    /// inventory was full. Owns reward identities (quest + item), merging of repeated grants,
    /// remaining quantities, claim results, and the save snapshot.
    ///
    /// Owned by <see cref="GameSession"/>. The presentation adapter resolves item ids and bridges to
    /// the inventory; the ledger never touches ItemData or Unity.
    ///
    /// Unity setup: none.
    /// </summary>
    public sealed class PendingRewardLedger
    {
        private readonly List<PendingRewardEntry> pending = new();

        /// <summary>Raised whenever the pending set or a remaining quantity changes.</summary>
        public event Action Changed;

        public IReadOnlyList<PendingRewardEntry> Pending => pending;

        public bool HasPendingRewards => pending.Count > 0;

        /// <summary>
        /// Records <paramref name="leftover"/> undelivered units of an item for a quest. Repeated
        /// grants for the same quest + item merge into a single entry. Returns the affected entry,
        /// or null when there is nothing to record.
        /// </summary>
        public PendingRewardEntry Record(string questId, string itemId, int leftover)
        {
            if (leftover <= 0 || string.IsNullOrWhiteSpace(itemId))
                return null;

            PendingRewardEntry entry = Find(questId, itemId);
            if (entry == null)
            {
                entry = new PendingRewardEntry
                {
                    rewardId = BuildRewardId(questId, itemId),
                    questId = questId,
                    itemId = itemId,
                    remaining = 0,
                };
                pending.Add(entry);
            }

            entry.remaining += leftover;
            Changed?.Invoke();
            return entry;
        }

        /// <summary>
        /// Retries delivery of every pending reward through <paramref name="deliver"/>, which returns
        /// how much the inventory accepted and whether the item was known. Completed entries are
        /// removed; unknown items are dropped. Returns a summary of the pass.
        /// </summary>
        public PendingRewardClaimResult Claim(Func<string, int, PendingRewardDelivery> deliver)
        {
            var result = new PendingRewardClaimResult();
            if (deliver == null) return result;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                PendingRewardEntry entry = pending[i];
                PendingRewardDelivery delivery = deliver(entry.itemId, entry.remaining);

                if (!delivery.ItemKnown)
                {
                    result.Dropped++;
                    pending.RemoveAt(i);
                    continue;
                }

                int taken = delivery.Delivered;
                if (taken < 0) taken = 0;
                if (taken > entry.remaining) taken = entry.remaining;

                entry.remaining -= taken;
                result.Delivered += taken;
                if (entry.remaining <= 0)
                    pending.RemoveAt(i);
            }

            if (result.ChangedAnything)
                Changed?.Invoke();

            return result;
        }

        /// <summary>Returns an independent copy of the pending entries for saving.</summary>
        public List<PendingRewardEntry> Snapshot()
        {
            var result = new List<PendingRewardEntry>(pending.Count);
            foreach (PendingRewardEntry entry in pending)
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

        /// <summary>Replaces the pending set from saved entries, skipping invalid records.</summary>
        public void Load(List<PendingRewardEntry> entries)
        {
            pending.Clear();
            if (entries != null)
            {
                foreach (PendingRewardEntry entry in entries)
                {
                    if (entry == null || entry.remaining <= 0 || string.IsNullOrWhiteSpace(entry.itemId))
                        continue;

                    pending.Add(new PendingRewardEntry
                    {
                        rewardId = entry.rewardId,
                        questId = entry.questId,
                        itemId = entry.itemId,
                        remaining = entry.remaining,
                    });
                }
            }

            Changed?.Invoke();
        }

        public void Clear()
        {
            if (pending.Count == 0) return;
            pending.Clear();
            Changed?.Invoke();
        }

        private PendingRewardEntry Find(string questId, string itemId)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                PendingRewardEntry entry = pending[i];
                if (entry.questId == questId && entry.itemId == itemId)
                    return entry;
            }
            return null;
        }

        private static string BuildRewardId(string questId, string itemId)
            => string.IsNullOrWhiteSpace(questId) ? itemId : questId + ":" + itemId;
    }
}
