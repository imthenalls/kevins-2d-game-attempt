using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the NPC use-door phase machine: approach, locked/busy/opening gates,
    /// stalls, unreachable gates, and a successful pass-through.
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class NpcUseDoorModelTests
    {
        private static NpcDoorObservation Obs(
            bool gateOpen = false,
            bool gateMoving = false,
            bool inRange = false,
            bool routeExhausted = false,
            bool arrivedPass = false,
            bool stalled = false,
            bool missing = false)
        {
            return new NpcDoorObservation
            {
                GateMissing = missing,
                GateOpen = gateOpen,
                GateMoving = gateMoving,
                InRange = inRange,
                RouteExhausted = routeExhausted,
                ArrivedAtPassTarget = arrivedPass,
                Stalled = stalled,
            };
        }

        [Test]
        public void Missing_Gate_Completes()
        {
            var model = new NpcUseDoorModel();
            model.Enter();

            Assert.AreEqual(NpcDoorCommand.Complete, model.Tick(Obs(missing: true)));
            Assert.IsTrue(model.IsComplete);
        }

        [Test]
        public void Opening_Gate_Waits_While_Moving()
        {
            var model = new NpcUseDoorModel();
            model.Enter();

            Assert.AreEqual(NpcDoorCommand.Wait, model.Tick(Obs(gateMoving: true)));
            Assert.AreEqual(NpcDoorPhase.Approach, model.Phase);
        }

        [Test]
        public void Open_Gate_Begins_Pass_Through()
        {
            var model = new NpcUseDoorModel();
            model.Enter();

            Assert.AreEqual(NpcDoorCommand.BeginPass, model.Tick(Obs(gateOpen: true)));
            Assert.AreEqual(NpcDoorPhase.PassThrough, model.Phase);
        }

        [Test]
        public void In_Range_Attempts_Use()
        {
            var model = new NpcUseDoorModel();
            model.Enter();

            Assert.AreEqual(NpcDoorCommand.AttemptUse, model.Tick(Obs(inRange: true)));
        }

        [Test]
        public void Out_Of_Range_Follows_The_Route()
        {
            var model = new NpcUseDoorModel();
            model.Enter();

            Assert.AreEqual(NpcDoorCommand.FollowRoute, model.Tick(Obs()));
        }

        [Test]
        public void Exhausted_Route_Stops_Short_Of_The_Gate()
        {
            var model = new NpcUseDoorModel();
            model.Enter();

            Assert.AreEqual(NpcDoorCommand.None, model.Tick(Obs(routeExhausted: true)));
        }

        [Test]
        public void Stalled_In_Range_Retries_The_Use()
        {
            var model = new NpcUseDoorModel();
            model.Enter();

            Assert.AreEqual(NpcDoorCommand.AttemptUse, model.Tick(Obs(inRange: true, stalled: true)));
        }

        [Test]
        public void Stalled_Out_Of_Range_Gives_Up()
        {
            var model = new NpcUseDoorModel();
            model.Enter();

            Assert.AreEqual(NpcDoorCommand.Complete, model.Tick(Obs(stalled: true)));
            Assert.IsTrue(model.IsComplete);
        }

        [Test]
        public void Locked_Gate_Completes()
        {
            var model = new NpcUseDoorModel();
            model.Enter();

            Assert.AreEqual(NpcDoorCommand.Complete, model.ResolveUse(GateUseResult.Locked));
            Assert.IsTrue(model.IsComplete);
        }

        [Test]
        public void Busy_Gate_Waits()
        {
            var model = new NpcUseDoorModel();
            model.Enter();

            Assert.AreEqual(NpcDoorCommand.Wait, model.ResolveUse(GateUseResult.Busy));
            Assert.IsFalse(model.IsComplete);
        }

        [Test]
        public void Unavailable_Gate_Completes()
        {
            var model = new NpcUseDoorModel();
            model.Enter();

            Assert.AreEqual(NpcDoorCommand.Complete, model.ResolveUse(GateUseResult.Unavailable));
        }

        [Test]
        public void Successful_Open_Passes_Through_And_Completes()
        {
            var model = new NpcUseDoorModel();
            model.Enter();

            Assert.AreEqual(NpcDoorCommand.AttemptUse, model.Tick(Obs(inRange: true)));
            Assert.AreEqual(NpcDoorCommand.BeginPass, model.ResolveUse(GateUseResult.Opened));
            Assert.AreEqual(NpcDoorPhase.PassThrough, model.Phase);

            Assert.AreEqual(NpcDoorCommand.FollowRoute, model.Tick(Obs()));
            Assert.AreEqual(NpcDoorCommand.Complete, model.Tick(Obs(arrivedPass: true)));
            Assert.IsTrue(model.IsComplete);
        }

        [Test]
        public void Pass_Through_Stall_Gives_Up()
        {
            var model = new NpcUseDoorModel();
            model.Enter();
            model.Tick(Obs(gateOpen: true)); // now PassThrough

            Assert.AreEqual(NpcDoorCommand.Complete, model.Tick(Obs(stalled: true)));
        }

        [Test]
        public void Commands_After_Completion_Are_NoOps()
        {
            var model = new NpcUseDoorModel();
            model.Enter();
            model.Tick(Obs(missing: true));

            Assert.AreEqual(NpcDoorCommand.None, model.Tick(Obs(inRange: true)));
            Assert.AreEqual(NpcDoorCommand.None, model.ResolveUse(GateUseResult.Opened));
        }

        [Test]
        public void ReEnter_Resets_Phase_And_Completion()
        {
            var model = new NpcUseDoorModel();
            model.Enter();
            model.Tick(Obs(missing: true));

            model.Enter();

            Assert.IsFalse(model.IsComplete);
            Assert.AreEqual(NpcDoorPhase.Approach, model.Phase);
        }
    }
}
