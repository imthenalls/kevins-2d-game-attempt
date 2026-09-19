using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Shared setup for Play Mode tests. Play Mode tests run in a single play session, so
    /// `DontDestroyOnLoad` singletons persist between tests — in particular
    /// `WorldTravelState.CurrentWorld`, which (via `WorldCharacter.SetActiveForWorld`) will
    /// deactivate the wrong-world player when an earlier test left it on World B.
    ///
    /// This restores the world to World A before each test so scene loads behave the same in any
    /// execution order. The singletons themselves are intentionally NOT destroyed: they are
    /// process-scoped by design and tearing them down mid-session breaks references and cached
    /// state. Reset only the mutable slice a test depends on.
    ///
    /// Unity setup: none. Play Mode test fixtures should inherit from this class.
    /// </summary>
    public abstract class PlayModeTestBase
    {
        [UnitySetUp]
        public IEnumerator ResetSharedState()
        {
            yield return null;
            if (WorldTravelState.Instance != null)
                WorldTravelState.Instance.SetCurrentWorld(WorldLayer.WorldA);
            yield return null;
        }
    }
}
