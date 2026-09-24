using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the player-death rule resolution.</summary>
    public class PlayerDeathPolicyTests
    {
        [Test]
        public void Default_Does_Nothing() =>
            Assert.AreEqual(
                PlayerDeathOutcome.None,
                PlayerDeathPolicy.Resolve(PlayerDeathBehavior.Default, null).Outcome);

        [Test]
        public void Respawn_In_Place_Restores_Full() =>
            Assert.AreEqual(
                PlayerDeathOutcome.RespawnToFull,
                PlayerDeathPolicy.Resolve(PlayerDeathBehavior.RespawnInPlace, null).Outcome);

        [Test]
        public void Send_To_Scene_Loads_The_Named_Scene()
        {
            PlayerDeathResolution result = PlayerDeathPolicy.Resolve(PlayerDeathBehavior.SendToScene, "Village");
            Assert.AreEqual(PlayerDeathOutcome.LoadScene, result.Outcome);
            Assert.AreEqual("Village", result.SceneName);
        }

        [Test]
        public void Send_To_Scene_Without_A_Scene_Does_Nothing() =>
            Assert.AreEqual(
                PlayerDeathOutcome.None,
                PlayerDeathPolicy.Resolve(PlayerDeathBehavior.SendToScene, "").Outcome);

        [Test]
        public void Game_Over_Loads_The_GameOver_Scene()
        {
            PlayerDeathResolution result = PlayerDeathPolicy.Resolve(PlayerDeathBehavior.GameOver, null);
            Assert.AreEqual(PlayerDeathOutcome.LoadScene, result.Outcome);
            Assert.AreEqual("GameOver", result.SceneName);
        }
    }
}
