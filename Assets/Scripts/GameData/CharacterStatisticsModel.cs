using System;

namespace Game.Core
{
    /// <summary>
    /// Engine-free cumulative gameplay statistics for one character: attacks, damage dealt, kills,
    /// critical hits, items gathered, and money gained. Owned per character by
    /// <see cref="GameSession"/> (via <see cref="CharacterStatisticsRepository"/>), so the values
    /// outlive the component and can be saved.
    ///
    /// Invalid negative additions are ignored so counters can never run backwards.
    ///
    /// Unity setup: none.
    /// </summary>
    public sealed class CharacterStatisticsModel
    {
        public int TotalAttacks { get; private set; }
        public int TotalDamageDealt { get; private set; }
        public int TotalKills { get; private set; }
        public int CriticalHits { get; private set; }
        public int TotalItemsGathered { get; private set; }
        public int TotalMoneyGained { get; private set; }

        /// <summary>Fired with the new total whenever attacks increment.</summary>
        public event Action<int> AttacksChanged;

        /// <summary>Fired with the new total whenever damage dealt increases.</summary>
        public event Action<int> DamageDealtChanged;

        /// <summary>Fired with the new total whenever kills increment.</summary>
        public event Action<int> KillsChanged;

        /// <summary>Fired with the new total whenever critical hits increment.</summary>
        public event Action<int> CriticalHitsChanged;

        /// <summary>Fired with the new total whenever items gathered increases.</summary>
        public event Action<int> ItemsGatheredChanged;

        /// <summary>Fired with the new total whenever money gained increases.</summary>
        public event Action<int> MoneyGainedChanged;

        /// <summary>
        /// Records a landed attack. The attack count always increments; non-positive damage is ignored.
        /// </summary>
        public void RecordAttack(int damage)
        {
            TotalAttacks++;
            AttacksChanged?.Invoke(TotalAttacks);

            if (damage <= 0)
                return;

            TotalDamageDealt += damage;
            DamageDealtChanged?.Invoke(TotalDamageDealt);
        }

        public void RecordKill()
        {
            TotalKills++;
            KillsChanged?.Invoke(TotalKills);
        }

        public void RecordCriticalHit()
        {
            CriticalHits++;
            CriticalHitsChanged?.Invoke(CriticalHits);
        }

        /// <summary>Records items gathered. Non-positive counts are ignored.</summary>
        public void RecordItemGathered(int count)
        {
            if (count <= 0)
                return;

            TotalItemsGathered += count;
            ItemsGatheredChanged?.Invoke(TotalItemsGathered);
        }

        /// <summary>Records money gained. Non-positive amounts are ignored.</summary>
        public void RecordMoneyGained(int amount)
        {
            if (amount <= 0)
                return;

            TotalMoneyGained += amount;
            MoneyGainedChanged?.Invoke(TotalMoneyGained);
        }

        /// <summary>Returns an independent snapshot for saving.</summary>
        public CharacterStatisticsSnapshot GetSnapshot() => new CharacterStatisticsSnapshot
        {
            totalAttacks = TotalAttacks,
            totalDamageDealt = TotalDamageDealt,
            totalKills = TotalKills,
            criticalHits = CriticalHits,
            totalItemsGathered = TotalItemsGathered,
            totalMoneyGained = TotalMoneyGained,
        };

        /// <summary>
        /// Replaces the counters from a saved snapshot. Missing/negative values are treated as zero.
        /// Does not raise change events (bulk restore).
        /// </summary>
        public void Load(CharacterStatisticsSnapshot snapshot)
        {
            TotalAttacks = NonNegative(snapshot?.totalAttacks);
            TotalDamageDealt = NonNegative(snapshot?.totalDamageDealt);
            TotalKills = NonNegative(snapshot?.totalKills);
            CriticalHits = NonNegative(snapshot?.criticalHits);
            TotalItemsGathered = NonNegative(snapshot?.totalItemsGathered);
            TotalMoneyGained = NonNegative(snapshot?.totalMoneyGained);
        }

        private static int NonNegative(int? value)
            => value.HasValue && value.Value > 0 ? value.Value : 0;
    }
}
