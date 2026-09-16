using System;

namespace Game.Core
{
    /// <summary>
    /// Authoritative, saveable state for a single NPC: identity, health, and logical map cell.
    /// This is a plain C# class — it does not inherit from MonoBehaviour or ScriptableObject and
    /// has no Unity dependency, so it can be exercised in tests without loading a scene.
    ///
    /// Unity setup: none. Created and owned by GameSession (via NpcStateRepository); a Unity
    /// adapter (NpcStateView) binds it to EntityStats and the NPC transform.
    ///
    /// Runtime API: IHealthModel members plus MoveToCell / Restore, and the Changed event.
    /// </summary>
    public sealed class NpcState : IHealthModel
    {
        public string NpcId { get; }
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public int CellX { get; private set; }
        public int CellY { get; private set; }
        public bool IsAlive => Hp > 0;

        /// <summary>Fired when Hp or MaxHp changes. Args: (hp, maxHp).</summary>
        public event Action<int, int> HpChanged;

        /// <summary>Fired on any change, including logical cell moves.</summary>
        public event Action<NpcState> Changed;

        public NpcState(string npcId, int maxHp, int hp, int cellX, int cellY)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                throw new ArgumentException("A stable npcId is required.", nameof(npcId));

            NpcId = npcId;
            MaxHp = Math.Max(1, maxHp);
            Hp = Clamp(hp, 0, MaxHp);
            CellX = cellX;
            CellY = cellY;
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

        /// <summary>Moves the NPC to a new logical grid cell.</summary>
        public void MoveToCell(int cellX, int cellY)
        {
            if (cellX == CellX && cellY == CellY)
                return;

            CellX = cellX;
            CellY = cellY;
            Changed?.Invoke(this);
        }

        /// <summary>Restores a full snapshot (used by load). Raises one change notification.</summary>
        public void Restore(int hp, int maxHp, int cellX, int cellY)
        {
            MaxHp = Math.Max(1, maxHp);
            Hp = Clamp(hp, 0, MaxHp);
            CellX = cellX;
            CellY = cellY;
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

        private void RaiseChanged()
        {
            HpChanged?.Invoke(Hp, MaxHp);
            Changed?.Invoke(this);
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : (value > max ? max : value);
    }
}
