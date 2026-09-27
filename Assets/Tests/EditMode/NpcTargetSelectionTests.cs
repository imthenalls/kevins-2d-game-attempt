using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the shared NPC target ranking. The selection rule (nearest eligible
    /// candidate within range, open/invalid filtered) lives in Game.Core, so it runs without a scene
    /// and is mirrored by `dotnet test`.
    ///
    /// Unity setup: none.
    /// </summary>
    public class NpcTargetSelectionTests
    {
        private static List<NpcTargetCandidate> Candidates(params NpcTargetCandidate[] items) =>
            new List<NpcTargetCandidate>(items);

        [Test]
        public void NearestIndex_Picks_The_Nearest_Eligible_Candidate()
        {
            var candidates = Candidates(
                new NpcTargetCandidate(3f, 0f, true),
                new NpcTargetCandidate(1f, 0f, true),
                new NpcTargetCandidate(2f, 0f, true));

            Assert.AreEqual(1, NpcTargetSelection.NearestIndex(0f, 0f, candidates, 10f));
        }

        [Test]
        public void NearestIndex_Filters_Ineligible_Candidates()
        {
            var candidates = Candidates(
                new NpcTargetCandidate(1f, 0f, false), // e.g. an open gate
                new NpcTargetCandidate(4f, 0f, true));

            Assert.AreEqual(1, NpcTargetSelection.NearestIndex(0f, 0f, candidates, 10f));
        }

        [Test]
        public void NearestIndex_Ignores_Candidates_Outside_Range()
        {
            var candidates = Candidates(new NpcTargetCandidate(9f, 0f, true));
            Assert.AreEqual(-1, NpcTargetSelection.NearestIndex(0f, 0f, candidates, 5f));
        }

        [Test]
        public void NearestIndex_Uses_Planar_Distance()
        {
            var candidates = Candidates(
                new NpcTargetCandidate(0f, 3f, true),
                new NpcTargetCandidate(4f, 0f, true));

            Assert.AreEqual(0, NpcTargetSelection.NearestIndex(0f, 0f, candidates, 10f));
        }

        [Test]
        public void NearestIndex_Is_Inclusive_At_The_Range_Edge()
        {
            var candidates = Candidates(new NpcTargetCandidate(5f, 0f, true));
            Assert.AreEqual(0, NpcTargetSelection.NearestIndex(0f, 0f, candidates, 5f));
        }

        [Test]
        public void NearestIndex_Returns_Minus_One_When_No_Candidate_Qualifies()
        {
            Assert.AreEqual(-1, NpcTargetSelection.NearestIndex(0f, 0f, new List<NpcTargetCandidate>(), 5f));
            Assert.AreEqual(-1, NpcTargetSelection.NearestIndex(0f, 0f, null, 5f));

            var allInvalid = Candidates(new NpcTargetCandidate(1f, 0f, false));
            Assert.AreEqual(-1, NpcTargetSelection.NearestIndex(0f, 0f, allInvalid, 5f));
        }

        [Test]
        public void Default_Perception_Config_Keeps_The_Original_Scan_Radius()
        {
            Assert.AreEqual(8f, new NpcPerceptionConfig().ScanRadius);
        }
    }
}
