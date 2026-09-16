using System;

namespace Game.Core
{
    /// <summary>
    /// Authoritative melee-attack tuning shared by the player and NPCs. Plain C# (no UnityEngine),
    /// lives in Game.Data. The Unity-only hit-mask (LayerMask) stays on CombatAttacker.
    ///
    /// Unity setup: none. Held as a [SerializeField] CombatAttackerConfig field by CombatAttacker.
    ///
    /// Runtime API: plain public fields. The legacy attack key is a KeyCode value as int.
    /// </summary>
    [Serializable]
    public class CombatAttackerConfig
    {
        public int AttackDamage = 10;
        public float AttackRange = 1.5f;
        public float AttackCooldown = 0.5f;
        /// <summary>Legacy timing value retained for existing scenes and visual listeners.</summary>
        public float AttackWindup = 0.15f;
        /// <summary>Visual swing length and weapon-contact damage-window duration.</summary>
        public float AttackDuration = 0.3f;
        /// <summary>Final fraction of the swing during which another press queues a follow-up.</summary>
        public float AttackBufferWindow = 0.5f;
        public bool CanHitSelf = false;
        public int SelfRecoilDamage = 0;
        public bool UsePlayerInput = true;
        /// <summary>UnityEngine.KeyCode value used by the legacy input fallback. 32 = Space.</summary>
        public int LegacyAttackKeyCode = 32;
    }
}
