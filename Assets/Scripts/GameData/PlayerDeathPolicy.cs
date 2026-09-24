namespace Game.Core
{
    /// <summary>What happens when the player dies in a scene. Owned by the Core so the rule is shared
    /// and testable. Unity setup: none.</summary>
    public enum PlayerDeathBehavior
    {
        /// <summary>Do nothing — let existing subscribers handle it.</summary>
        Default,
        /// <summary>Restore the player's HP to full and stay in the scene.</summary>
        RespawnInPlace,
        /// <summary>Load the configured death scene.</summary>
        SendToScene,
        /// <summary>Load the "GameOver" scene.</summary>
        GameOver
    }

    /// <summary>Resolved action for a player death.</summary>
    public enum PlayerDeathOutcome
    {
        None,
        RespawnToFull,
        LoadScene,
    }

    /// <summary>Engine-free result of resolving a player-death rule.</summary>
    public readonly struct PlayerDeathResolution
    {
        public readonly PlayerDeathOutcome Outcome;
        public readonly string SceneName;

        public PlayerDeathResolution(PlayerDeathOutcome outcome, string sceneName)
        {
            Outcome = outcome;
            SceneName = sceneName;
        }
    }

    /// <summary>
    /// Engine-free player-death policy: maps the scene's PlayerDeathBehavior (plus its death scene
    /// name) to an outcome the Unity adapter performs. Plain C#, lives in Game.Data, unit-tested.
    /// </summary>
    public static class PlayerDeathPolicy
    {
        public static PlayerDeathResolution Resolve(PlayerDeathBehavior behavior, string deathScene)
        {
            switch (behavior)
            {
                case PlayerDeathBehavior.RespawnInPlace:
                    return new PlayerDeathResolution(PlayerDeathOutcome.RespawnToFull, null);

                case PlayerDeathBehavior.SendToScene:
                    return string.IsNullOrEmpty(deathScene)
                        ? new PlayerDeathResolution(PlayerDeathOutcome.None, null)
                        : new PlayerDeathResolution(PlayerDeathOutcome.LoadScene, deathScene);

                case PlayerDeathBehavior.GameOver:
                    return new PlayerDeathResolution(PlayerDeathOutcome.LoadScene, "GameOver");

                default:
                    return new PlayerDeathResolution(PlayerDeathOutcome.None, null);
            }
        }
    }
}
