using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the NPC home-schedule state machine (events in, commands out).</summary>
    public class NpcScheduleStateTests
    {
        private static NpcScheduleConfig Config() => new NpcScheduleConfig
        {
            AwaySeconds = 20f,
            HomeSeconds = 10f,
            RetrySeconds = 5f,
            InitialAwayMinFraction = 0.3f,
            HomeDoorEnterRadius = 1.5f,
            WaypointThreshold = 0.35f,
        };

        private static NpcScheduleState Away()
        {
            NpcScheduleConfig config = Config();
            return new NpcScheduleState("npc", NpcSchedulePhase.Away, config.AwaySeconds);
        }

        [Test]
        public void Away_Timer_Expiry_Requests_A_Route_And_Enters_ToHome()
        {
            NpcScheduleConfig config = Config();
            NpcScheduleState state = Away();

            NpcScheduleCommand command = state.Tick(config.AwaySeconds, config);

            Assert.AreEqual(NpcScheduleCommand.RequestRoute, command);
            Assert.AreEqual(NpcSchedulePhase.ToHome, state.Phase);
        }

        [Test]
        public void ReachedEntrance_Requests_Entering_The_Portal()
        {
            NpcScheduleConfig config = Config();
            NpcScheduleState state = Away();
            state.Tick(config.AwaySeconds, config);

            Assert.AreEqual(NpcScheduleCommand.EnterHomePortal,
                state.Handle(NpcScheduleEvent.ReachedEntrance, config));
            Assert.AreEqual(NpcSchedulePhase.ToHome, state.Phase);
        }

        [Test]
        public void Portal_Success_From_ToHome_Enters_Home_And_Opens_The_Door()
        {
            NpcScheduleConfig config = Config();
            NpcScheduleState state = Away();
            state.Tick(config.AwaySeconds, config);
            state.Handle(NpcScheduleEvent.ReachedEntrance, config);

            NpcScheduleCommand command = state.Handle(NpcScheduleEvent.PortalSucceeded, config);

            Assert.AreEqual(NpcScheduleCommand.OpenHomeDoor, command);
            Assert.AreEqual(NpcSchedulePhase.Home, state.Phase);
            Assert.AreEqual(config.HomeSeconds, state.SecondsRemaining, 0.0001f);
        }

        [Test]
        public void Portal_Failure_From_ToHome_Returns_Away_And_Closes_The_Door()
        {
            NpcScheduleConfig config = Config();
            NpcScheduleState state = Away();
            state.Tick(config.AwaySeconds, config);
            state.Handle(NpcScheduleEvent.ReachedEntrance, config);

            NpcScheduleCommand command = state.Handle(NpcScheduleEvent.PortalFailed, config);

            Assert.AreEqual(NpcScheduleCommand.CloseHomeDoor, command);
            Assert.AreEqual(NpcSchedulePhase.Away, state.Phase);
            Assert.AreEqual(config.AwaySeconds, state.SecondsRemaining, 0.0001f);
        }

        [Test]
        public void Route_Failure_Returns_To_Away_For_The_Retry_Delay()
        {
            NpcScheduleConfig config = Config();
            NpcScheduleState state = Away();
            state.Tick(config.AwaySeconds, config);

            NpcScheduleCommand command = state.Handle(NpcScheduleEvent.RouteFailed, config);

            Assert.AreEqual(NpcScheduleCommand.CloseHomeDoor, command);
            Assert.AreEqual(NpcSchedulePhase.Away, state.Phase);
            Assert.AreEqual(config.RetrySeconds, state.SecondsRemaining, 0.0001f);
        }

        [Test]
        public void Home_Timer_Expiry_Requests_Leaving()
        {
            NpcScheduleConfig config = Config();
            NpcScheduleState state = new NpcScheduleState("npc", NpcSchedulePhase.Home, config.HomeSeconds);

            NpcScheduleCommand command = state.Tick(config.HomeSeconds, config);

            Assert.AreEqual(NpcScheduleCommand.LeaveHomePortal, command);
            Assert.AreEqual(NpcSchedulePhase.Home, state.Phase);
        }

        [Test]
        public void Leaving_Success_Returns_Away_And_Closes_The_Door()
        {
            NpcScheduleConfig config = Config();
            NpcScheduleState state = new NpcScheduleState("npc", NpcSchedulePhase.Home, config.HomeSeconds);
            state.Tick(config.HomeSeconds, config);

            NpcScheduleCommand command = state.Handle(NpcScheduleEvent.PortalSucceeded, config);

            Assert.AreEqual(NpcScheduleCommand.CloseHomeDoor, command);
            Assert.AreEqual(NpcSchedulePhase.Away, state.Phase);
            Assert.AreEqual(config.AwaySeconds, state.SecondsRemaining, 0.0001f);
        }

        [Test]
        public void Leaving_Failure_Stays_Home_For_The_Retry_Delay()
        {
            NpcScheduleConfig config = Config();
            NpcScheduleState state = new NpcScheduleState("npc", NpcSchedulePhase.Home, config.HomeSeconds);
            state.Tick(config.HomeSeconds, config);

            NpcScheduleCommand command = state.Handle(NpcScheduleEvent.PortalFailed, config);

            Assert.AreEqual(NpcScheduleCommand.None, command);
            Assert.AreEqual(NpcSchedulePhase.Home, state.Phase);
            Assert.AreEqual(config.RetrySeconds, state.SecondsRemaining, 0.0001f);
        }

        [Test]
        public void Non_Idle_Behavior_Pauses_The_Timer_And_Returns_Pause()
        {
            NpcScheduleConfig config = Config();
            NpcScheduleState state = Away();
            float before = state.SecondsRemaining;

            NpcScheduleCommand command = state.Tick(1f, config, NpcBehaviorState.Combat);

            Assert.AreEqual(NpcScheduleCommand.Pause, command);
            Assert.AreEqual(before, state.SecondsRemaining, 0.0001f);
            Assert.AreEqual(NpcSchedulePhase.Away, state.Phase);
        }

        [Test]
        public void Talking_And_Disabled_Also_Pause()
        {
            NpcScheduleConfig config = Config();

            NpcScheduleState talking = Away();
            Assert.AreEqual(
                NpcScheduleCommand.Pause, talking.Tick(1f, config, NpcBehaviorState.Talking));

            NpcScheduleState disabled = Away();
            Assert.AreEqual(
                NpcScheduleCommand.Pause, disabled.Tick(1f, config, NpcBehaviorState.Disabled));
        }

        [Test]
        public void Idle_Behavior_Resumes_The_Schedule()
        {
            NpcScheduleConfig config = Config();
            NpcScheduleState state = Away();

            state.Tick(1f, config, NpcBehaviorState.Combat);
            NpcScheduleCommand command = state.Tick(config.AwaySeconds, config, NpcBehaviorState.Idle);

            Assert.AreEqual(NpcScheduleCommand.RequestRoute, command);
            Assert.AreEqual(NpcSchedulePhase.ToHome, state.Phase);
        }

        [Test]
        public void Initial_Away_Seconds_Stays_Within_The_Configured_Range()
        {
            var config = new NpcScheduleConfig { AwaySeconds = 40f, InitialAwayMinFraction = 0.25f };

            for (int seed = 0; seed < 50; seed++)
            {
                float seconds = config.InitialAwaySeconds(seed);
                Assert.GreaterOrEqual(seconds, 10f - 0.0001f);
                Assert.LessOrEqual(seconds, 40f + 0.0001f);
            }
        }

        [Test]
        public void Events_That_Do_Not_Apply_Are_Ignored()
        {
            NpcScheduleConfig config = Config();
            NpcScheduleState state = Away();

            // Away ignores everything reported by the facade.
            Assert.AreEqual(NpcScheduleCommand.None, state.Handle(NpcScheduleEvent.PortalSucceeded, config));
            Assert.AreEqual(NpcSchedulePhase.Away, state.Phase);
        }
    }
}
