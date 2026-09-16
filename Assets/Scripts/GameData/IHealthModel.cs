using System;

namespace Game.Core
{
    /// <summary>
    /// Contract for a pure-C# health model that a Unity facade (EntityStats) can delegate to.
    /// Binding this to EntityStats keeps the authoritative HP value in the model instead of the
    /// MonoBehaviour.
    ///
    /// Unity setup: none — this is an interface. Implement it in the core assembly (see NpcState)
    /// and bind it from a Unity adapter such as NpcStateView.
    /// </summary>
    public interface IHealthModel
    {
        int Hp { get; }
        int MaxHp { get; }
        bool IsAlive { get; }

        /// <summary>Fired whenever HP or MaxHp changes. Args: (hp, maxHp).</summary>
        event Action<int, int> HpChanged;

        /// <summary>Reduces HP and returns the amount actually applied.</summary>
        int ApplyDamage(int amount);

        /// <summary>Restores HP and returns the amount actually restored.</summary>
        int Heal(int amount);

        void SetHp(int value);
        void SetMaxHp(int value);
    }
}
