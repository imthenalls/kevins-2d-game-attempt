using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the shared NPC route follower (waypoint advance and completion).</summary>
    public class RouteFollowerTests
    {
        private static List<PathPoint> Route(params (float x, float y)[] points)
        {
            var route = new List<PathPoint>(points.Length);
            foreach ((float x, float y) in points)
                route.Add(new PathPoint(x, y));
            return route;
        }

        [Test]
        public void Empty_Route_Is_Complete_And_Has_No_Current()
        {
            var follower = new RouteFollower();
            Assert.IsFalse(follower.HasRoute);
            Assert.IsTrue(follower.IsComplete);
            Assert.IsFalse(follower.TryCurrent(out _, out _));
        }

        [Test]
        public void SetRoute_Exposes_The_First_Waypoint()
        {
            var follower = new RouteFollower();
            follower.SetRoute(Route((1f, 2f), (3f, 4f)));

            Assert.IsTrue(follower.HasRoute);
            Assert.IsFalse(follower.IsComplete);
            Assert.AreEqual(2, follower.Remaining);
            Assert.IsTrue(follower.TryCurrent(out float x, out float y));
            Assert.AreEqual(1f, x, 0.0001f);
            Assert.AreEqual(2f, y, 0.0001f);
        }

        [Test]
        public void Advance_Consumes_Only_Reached_Waypoints()
        {
            var follower = new RouteFollower();
            follower.SetRoute(Route((0f, 0f), (0.1f, 0f), (5f, 0f)));

            int consumed = follower.Advance(0f, 0f, 0.35f);

            Assert.AreEqual(2, consumed);
            Assert.IsTrue(follower.TryCurrent(out float x, out float y));
            Assert.AreEqual(5f, x, 0.0001f);
            Assert.AreEqual(1, follower.Remaining);
        }

        [Test]
        public void Advance_Completes_The_Route_And_Reports_Done()
        {
            var follower = new RouteFollower();
            follower.SetRoute(Route((1f, 0f), (1.05f, 0f)));

            follower.Advance(1.1f, 0f, 0.35f);

            Assert.IsTrue(follower.IsComplete);
            Assert.AreEqual(0, follower.Remaining);
            Assert.IsFalse(follower.TryCurrent(out _, out _));
        }

        [Test]
        public void Advance_Without_Progress_Consumes_Nothing()
        {
            var follower = new RouteFollower();
            follower.SetRoute(Route((10f, 10f)));

            Assert.AreEqual(0, follower.Advance(0f, 0f, 0.35f));
            Assert.AreEqual(1, follower.Remaining);
        }

        [Test]
        public void Clear_Unloads_The_Route()
        {
            var follower = new RouteFollower();
            follower.SetRoute(Route((1f, 1f)));
            follower.Clear();

            Assert.IsFalse(follower.HasRoute);
            Assert.IsTrue(follower.IsComplete);
        }

        [Test]
        public void Walking_Every_Waypoint_Reaches_The_Goal()
        {
            var follower = new RouteFollower();
            follower.SetRoute(Route((1f, 0f), (2f, 0f), (3f, 0f)));

            float x = 0f;
            while (follower.TryCurrent(out float waypointX, out _))
            {
                x = waypointX;
                follower.Advance(x, 0f, 0.1f);
            }

            Assert.IsTrue(follower.IsComplete);
            Assert.AreEqual(3f, x, 0.0001f);
        }
    }
}
