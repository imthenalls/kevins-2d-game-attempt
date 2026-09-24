using System;

namespace Game.Core
{
    /// <summary>
    /// Engine-free accumulation of equipment stat bonuses (attack/defense) for an entity. Owned by
    /// EntityStats so the authoritative bonus values live in Game.Data. Plain C#, unit-testable.
    ///
    /// Unity setup: none.
    /// </summary>
    public sealed class StatBonuses
    {
        public int Attack { get; private set; }
        public int Defense { get; private set; }

        /// <summary>Accumulates bonuses from an equipped item.</summary>
        public void Add(int attack, int defense)
        {
            Attack += attack;
            Defense += defense;
        }

        /// <summary>Removes bonuses from an unequipped item, never going below zero.</summary>
        public void Remove(int attack, int defense)
        {
            Attack = Math.Max(0, Attack - attack);
            Defense = Math.Max(0, Defense - defense);
        }

        public void Clear()
        {
            Attack = 0;
            Defense = 0;
        }
    }
}
