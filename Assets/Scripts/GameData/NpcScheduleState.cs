using System;

namespace Game.Core
{
    /// <summary>
    /// Authoritative, saveable schedule state for one NPC: the current home-schedule phase and the
    /// seconds left in it. Plain C# — no Unity dependency — so it can be tested without a scene.
    ///
    /// Unity setup: none. Created and owned by GameSession (via NpcScheduleRepository); the
    /// NpcSchedule3D MonoBehaviour is a thin facade that reads/writes it.
    ///
    /// Runtime API: Phase, SecondsRemaining, Tick, SetPhase, Restore, IsHome, Changed.
    /// </summary>
    public sealed class NpcScheduleState
    {
        public string NpcId { get; }
        public NpcSchedulePhase Phase { get; private set; }
        public float SecondsRemaining { get; private set; }

        /// <summary>Fired when the phase (or its timer) changes. Tick does not raise it.</summary>
        public event Action<NpcScheduleState> Changed;

        public NpcScheduleState(string npcId, NpcSchedulePhase phase, float secondsRemaining)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                throw new ArgumentException("A stable npcId is required.", nameof(npcId));

            NpcId = npcId;
            Phase = phase;
            SecondsRemaining = Math.Max(0f, secondsRemaining);
        }

        public bool IsHome => Phase == NpcSchedulePhase.Home;

        /// <summary>Advances the phase timer. Called every frame, so it does not raise Changed.</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || SecondsRemaining <= 0f)
                return;

            SecondsRemaining = Math.Max(0f, SecondsRemaining - deltaSeconds);
        }

        /// <summary>Moves to a phase and resets its timer.</summary>
        public void SetPhase(NpcSchedulePhase phase, float secondsRemaining)
        {
            float next = Math.Max(0f, secondsRemaining);
            if (phase == Phase && Math.Abs(next - SecondsRemaining) < 0.0001f)
                return;

            Phase = phase;
            SecondsRemaining = next;
            Changed?.Invoke(this);
        }

        /// <summary>Restores a saved snapshot (used by load).</summary>
        public void Restore(NpcSchedulePhase phase, float secondsRemaining)
        {
            Phase = phase;
            SecondsRemaining = Math.Max(0f, secondsRemaining);
            Changed?.Invoke(this);
        }
    }
}
