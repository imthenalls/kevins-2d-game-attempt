using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Tests for the engine-free id-integrity helpers that back the Editor data validator.
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class IdIntegrityTests
    {
        [Test]
        public void Unique_Ids_Produce_No_Issues()
        {
            var issues = IdIntegrity.FindDuplicateOrBlankIds(new[] { "apple", "pear", "sword" }, "item");
            Assert.IsEmpty(issues);
        }

        [Test]
        public void Duplicate_Ids_Are_Reported_Once_Case_Insensitively()
        {
            var issues = IdIntegrity.FindDuplicateOrBlankIds(new[] { "apple", "Apple", "pear", "apple" }, "item");

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("item.duplicate", issues[0].Code);
            Assert.AreEqual(ValidationSeverity.Error, issues[0].Severity);
        }

        [Test]
        public void Blank_Ids_Are_Reported()
        {
            var issues = IdIntegrity.FindDuplicateOrBlankIds(new[] { "apple", "", "  ", null }, "item");

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("item.blank", issues[0].Code);
        }

        [Test]
        public void Null_List_Is_Safe()
        {
            Assert.IsEmpty(IdIntegrity.FindDuplicateOrBlankIds(null, "item"));
        }

        [Test]
        public void Dangling_References_Are_Reported_And_Deduplicated()
        {
            var references = new List<IdIntegrity.IdReference>
            {
                new IdIntegrity.IdReference("quest bandit", "ghost_item"),
                new IdIntegrity.IdReference("quest bandit", "ghost_item"),
                new IdIntegrity.IdReference("loot sword_guard", "gold_coin"),
            };

            var issues = IdIntegrity.FindDanglingReferences(references, new[] { "gold_coin" }, "item");

            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("item.dangling", issues[0].Code);
            StringAssert.Contains("ghost_item", issues[0].Message);
        }

        [Test]
        public void Known_Ids_Match_Case_Insensitively_And_Blanks_Are_Ignored()
        {
            var references = new List<IdIntegrity.IdReference>
            {
                new IdIntegrity.IdReference("owner", "GOLD_COIN"),
                new IdIntegrity.IdReference("owner", ""),
                new IdIntegrity.IdReference("owner", null),
            };

            var issues = IdIntegrity.FindDanglingReferences(references, new[] { "gold_coin" }, "item");

            Assert.IsEmpty(issues);
        }
    }
}
