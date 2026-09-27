using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the chase navigation policy: direct-step fallback, repath cadence, and
    /// target-moved repathing. Line-of-sight is supplied by the adapter, so this runs without a scene.
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class NpcChaseNavigationPolicyTests
    {
        private static NpcChaseNavigationConfig Config() => new NpcChaseNavigationConfig
        {
            MaxPathDistance = 25f,
            RepathTargetMoved = 1.5f,
            MinRepathInterval = 0.05f,
            WaypointReached = 0.35f,
        };

        [Test]
        public void Clear_Line_Drives_Direct()
        {
            var policy = new NpcChaseNavigationPolicy(Config());

            NpcChaseNavigationDecision decision = policy.Evaluate(
                now: 0f, selfX: 0f, selfZ: 0f, targetX: 5f, targetZ: 0f,
                hasClearLine: true, repathInterval: 0.5f);

            Assert.IsTrue(decision.DriveDirect);
            Assert.IsFalse(decision.Repath);
        }

        [Test]
        public void Far_Target_Drives_Direct()
        {
            var policy = new NpcChaseNavigationPolicy(Config());

            NpcChaseNavigationDecision decision = policy.Evaluate(
                now: 0f, selfX: 0f, selfZ: 0f, targetX: 100f, targetZ: 0f,
                hasClearLine: false, repathInterval: 0.5f);

            Assert.IsTrue(decision.DriveDirect);
        }

        [Test]
        public void First_Blocked_Tick_Requests_A_Repath()
        {
            var policy = new NpcChaseNavigationPolicy(Config());

            NpcChaseNavigationDecision decision = policy.Evaluate(
                now: 0f, selfX: 0f, selfZ: 0f, targetX: 5f, targetZ: 0f,
                hasClearLine: false, repathInterval: 0.5f);

            Assert.IsTrue(decision.Repath);
            Assert.IsFalse(decision.DriveDirect);
        }

        [Test]
        public void Within_The_Interval_With_A_Steady_Target_Does_Not_Repath()
        {
            var policy = new NpcChaseNavigationPolicy(Config());
            policy.Evaluate(0f, 0f, 0f, 5f, 0f, false, 0.5f);

            NpcChaseNavigationDecision decision = policy.Evaluate(
                now: 0.25f, selfX: 0f, selfZ: 0f, targetX: 5f, targetZ: 0f,
                hasClearLine: false, repathInterval: 0.5f);

            Assert.IsFalse(decision.Repath);
            Assert.IsFalse(decision.DriveDirect);
        }

        [Test]
        public void Interval_Expiry_Requests_A_Repath()
        {
            var policy = new NpcChaseNavigationPolicy(Config());
            policy.Evaluate(0f, 0f, 0f, 5f, 0f, false, 0.5f);

            NpcChaseNavigationDecision decision = policy.Evaluate(
                now: 0.5f, selfX: 0f, selfZ: 0f, targetX: 5f, targetZ: 0f,
                hasClearLine: false, repathInterval: 0.5f);

            Assert.IsTrue(decision.Repath);
        }

        [Test]
        public void Target_Moving_Far_Requests_An_Immediate_Repath()
        {
            var policy = new NpcChaseNavigationPolicy(Config());
            policy.Evaluate(0f, 0f, 0f, 5f, 0f, false, 2f);

            NpcChaseNavigationDecision decision = policy.Evaluate(
                now: 0.1f, selfX: 0f, selfZ: 0f, targetX: 7f, targetZ: 0f,
                hasClearLine: false, repathInterval: 2f);

            Assert.IsTrue(decision.Repath);
        }

        [Test]
        public void Repath_Interval_Is_Clamped_To_The_Minimum()
        {
            var policy = new NpcChaseNavigationPolicy(Config());
            policy.Evaluate(0f, 0f, 0f, 5f, 0f, false, 0f); // clamped to 0.05

            Assert.IsFalse(policy.Evaluate(0.04f, 0f, 0f, 5f, 0f, false, 0f).Repath);
            Assert.IsTrue(policy.Evaluate(0.05f, 0f, 0f, 5f, 0f, false, 0f).Repath);
        }

        [Test]
        public void After_A_Direct_Step_The_Next_Blocked_Tick_Repaths()
        {
            var policy = new NpcChaseNavigationPolicy(Config());
            policy.Evaluate(0f, 0f, 0f, 5f, 0f, true, 0.5f); // direct

            NpcChaseNavigationDecision decision = policy.Evaluate(
                now: 0.01f, selfX: 0f, selfZ: 0f, targetX: 5f, targetZ: 0f,
                hasClearLine: false, repathInterval: 0.5f);

            Assert.IsTrue(decision.Repath);
        }

        [Test]
        public void A_Failed_Path_Waits_Instead_Of_Falling_Back_To_Direct()
        {
            var policy = new NpcChaseNavigationPolicy(Config());
            NpcChaseNavigationDecision decision = policy.Evaluate(
                0f, 0f, 0f, 5f, 0f, hasClearLine: false, repathInterval: 0.5f);
            Assert.IsTrue(decision.Repath);

            NpcChaseNavigationDecision afterFailure = policy.ReportPathResult(foundPath: false);
            Assert.IsTrue(afterFailure.Wait);
            Assert.IsFalse(afterFailure.DriveDirect);

            // Within the cadence, the adapter must keep waiting, not blindly step at the target.
            NpcChaseNavigationDecision next = policy.Evaluate(
                0.1f, 0f, 0f, 5f, 0f, hasClearLine: false, repathInterval: 0.5f);
            Assert.IsTrue(next.Wait);
            Assert.IsFalse(next.DriveDirect);
        }

        [Test]
        public void A_Failed_Path_Repaths_Again_When_The_Interval_Expires()
        {
            var policy = new NpcChaseNavigationPolicy(Config());
            policy.Evaluate(0f, 0f, 0f, 5f, 0f, hasClearLine: false, repathInterval: 0.5f);
            policy.ReportPathResult(foundPath: false);

            Assert.IsTrue(policy.Evaluate(0.25f, 0f, 0f, 5f, 0f, false, 0.5f).Wait);
            Assert.IsTrue(policy.Evaluate(0.5f, 0f, 0f, 5f, 0f, false, 0.5f).Repath);
        }

        [Test]
        public void A_Successful_Path_Is_Followed_Until_Exhausted()
        {
            var policy = new NpcChaseNavigationPolicy(Config());
            policy.Evaluate(0f, 0f, 0f, 5f, 0f, hasClearLine: false, repathInterval: 0.5f);

            NpcChaseNavigationDecision afterSuccess = policy.ReportPathResult(foundPath: true);
            Assert.IsTrue(afterSuccess.FollowRoute);

            // Still following the committed route before the cadence expires.
            NpcChaseNavigationDecision following = policy.Evaluate(
                0.1f, 0f, 0f, 5f, 0f, hasClearLine: false, repathInterval: 0.5f);
            Assert.IsTrue(following.FollowRoute);
        }

        [Test]
        public void An_Exhausted_Route_Waits_Until_The_Next_Repath_Instead_Of_Falling_Back()
        {
            var policy = new NpcChaseNavigationPolicy(Config());
            policy.Evaluate(0f, 0f, 0f, 5f, 0f, hasClearLine: false, repathInterval: 0.5f);
            policy.ReportPathResult(foundPath: true);

            policy.ReportRouteExhausted();

            NpcChaseNavigationDecision held = policy.Evaluate(
                0.1f, 0f, 0f, 5f, 0f, hasClearLine: false, repathInterval: 0.5f);
            Assert.IsTrue(held.Wait);
            Assert.IsFalse(held.DriveDirect);

            NpcChaseNavigationDecision repath = policy.Evaluate(
                0.5f, 0f, 0f, 5f, 0f, hasClearLine: false, repathInterval: 0.5f);
            Assert.IsTrue(repath.Repath);
        }

        [Test]
        public void Clear_Line_Resets_Any_Blocked_State()
        {
            var policy = new NpcChaseNavigationPolicy(Config());
            policy.Evaluate(0f, 0f, 0f, 5f, 0f, hasClearLine: false, repathInterval: 0.5f);
            policy.ReportPathResult(foundPath: false);

            NpcChaseNavigationDecision direct = policy.Evaluate(
                0.1f, 0f, 0f, 5f, 0f, hasClearLine: true, repathInterval: 0.5f);
            Assert.IsTrue(direct.DriveDirect);
        }
    }
}
