using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>Engine-free tests for the gated dialogue gift rule.</summary>
    public class DialogueGiftPolicyTests
    {
        [Test]
        public void No_Required_Flag_Always_Gives()
        {
            Assert.IsTrue(DialogueGiftPolicy.ShouldGive(requiresFlag: false, flagSet: false));
            Assert.IsTrue(DialogueGiftPolicy.ShouldGive(requiresFlag: false, flagSet: true));
        }

        [Test]
        public void Required_Flag_Only_Gives_When_Set()
        {
            Assert.IsFalse(DialogueGiftPolicy.ShouldGive(requiresFlag: true, flagSet: false));
            Assert.IsTrue(DialogueGiftPolicy.ShouldGive(requiresFlag: true, flagSet: true));
        }
    }
}
