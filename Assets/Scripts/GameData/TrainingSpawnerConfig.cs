using System;

namespace Game.Core
{
    /// <summary>
    /// Tuning for TrainingEnemySpawner: interaction distance and the world-space arena bounds the
    /// summoned challenger is confined to. Plain C#, lives in Game.Data. Arena bounds are stored
    /// as x/y/width/height.
    ///
    /// Unity setup: none. Held as a [SerializeField] TrainingSpawnerConfig field by
    /// TrainingEnemySpawner.
    /// Runtime API: plain public fields.
    /// </summary>
    [Serializable]
    public class TrainingSpawnerConfig
    {
        public float InteractionRange = 2f;
        public float ArenaX = 61f;
        public float ArenaY = -7f;
        public float ArenaWidth = 18f;
        public float ArenaHeight = 14f;
    }
}
