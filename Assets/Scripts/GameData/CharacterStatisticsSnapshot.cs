using System;

namespace Game.Core
{
    /// <summary>
    /// Serializable snapshot of a character's cumulative gameplay statistics. Owned by
    /// <see cref="CharacterStatisticsModel"/>, persisted in the save file for the player.
    ///
    /// Unity setup: none.
    /// </summary>
    [Serializable]
    public class CharacterStatisticsSnapshot
    {
        public int totalAttacks;
        public int totalDamageDealt;
        public int totalKills;
        public int criticalHits;
        public int totalItemsGathered;
        public int totalMoneyGained;

        public CharacterStatisticsSnapshot Clone() => new CharacterStatisticsSnapshot
        {
            totalAttacks = totalAttacks,
            totalDamageDealt = totalDamageDealt,
            totalKills = totalKills,
            criticalHits = criticalHits,
            totalItemsGathered = totalItemsGathered,
            totalMoneyGained = totalMoneyGained,
        };
    }
}
