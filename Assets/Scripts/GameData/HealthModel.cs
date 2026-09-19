using System;

namespace Game.Core
{
    /// <summary>
    /// Engine-free authoritative health for an entity: current and maximum HP, with the same
    /// clamping and death semantics the Unity EntityStats facade uses. Implements
    /// <see cref="IHealthModel"/>, so EntityStats delegates to it while bound.
    ///
    /// The player's model is owned by <see cref="GameSession"/> (shared across avatars and scenes);
    /// NPCs use NpcState instead. Unity setup: none.
    ///
    /// Runtime API: Hp, MaxHp, IsAlive, ApplyDamage, Heal, SetHp, SetMaxHp, HpChanged.
    /// </summary>
    public sealed class HealthModel : IHealthModel
    {
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public bool IsAlive => Hp > 0;

        /// <summary>Fired whenever HP or MaxHp changes. Args: (hp, maxHp).</summary>
        public event Action<int, int> HpChanged;

        public HealthModel(int maxHp, int hp)
        {
            MaxHp = Math.Max(1, maxHp);
            Hp = Clamp(hp, 0, MaxHp);
        }

        public int ApplyDamage(int amount)
        {
            if (amount <= 0 || !IsAlive)
                return 0;

            int applied = Math.Min(amount, Hp);
            SetHpInternal(Hp - applied);
            return applied;
        }

        public int Heal(int amount)
        {
            if (amount <= 0 || !IsAlive)
                return 0;

            int before = Hp;
            SetHpInternal(Hp + amount);
            return Hp - before;
        }

        public void SetHp(int value) => SetHpInternal(value);

        public void SetMaxHp(int value)
        {
            int next = Math.Max(1, value);
            if (next == MaxHp)
                return;

            MaxHp = next;
            if (Hp > MaxHp)
                Hp = MaxHp;
            RaiseChanged();
        }

        private void SetHpInternal(int value)
        {
            int next = Clamp(value, 0, MaxHp);
            if (next == Hp)
                return;

            Hp = next;
            RaiseChanged();
        }

        private void RaiseChanged() => HpChanged?.Invoke(Hp, MaxHp);

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : (value > max ? max : value);
    }
}
