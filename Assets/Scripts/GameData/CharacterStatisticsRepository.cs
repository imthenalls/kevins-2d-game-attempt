using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Engine-free registry of per-character statistics models, keyed by a stable character id.
    /// Owned by <see cref="GameSession"/>, so a character's totals survive component destruction and
    /// scene loads.
    ///
    /// Unity setup: none.
    /// </summary>
    public sealed class CharacterStatisticsRepository
    {
        private readonly Dictionary<string, CharacterStatisticsModel> models =
            new Dictionary<string, CharacterStatisticsModel>(StringComparer.Ordinal);

        /// <summary>Returns the model for the id, creating it on first use.</summary>
        public CharacterStatisticsModel GetOrCreate(string characterId)
        {
            string id = string.IsNullOrWhiteSpace(characterId) ? "default" : characterId.Trim();
            if (!models.TryGetValue(id, out CharacterStatisticsModel model))
            {
                model = new CharacterStatisticsModel();
                models[id] = model;
            }
            return model;
        }

        public bool TryGet(string characterId, out CharacterStatisticsModel model)
            => models.TryGetValue(characterId ?? string.Empty, out model);
    }
}
