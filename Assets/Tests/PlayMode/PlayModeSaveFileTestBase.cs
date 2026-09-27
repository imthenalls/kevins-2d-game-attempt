using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Shared Play Mode fixture for tests that exercise <see cref="SaveManager"/>. It points
    /// <see cref="SaveManager.SaveDirectoryOverride"/> at a unique, empty sandbox directory for the
    /// duration of each test and removes it afterward, so the player's real save files
    /// (<c>save.json</c>, <c>.bak</c>, <c>.tmp</c>) are never read, overwritten, or deleted.
    ///
    /// Play Mode test fixtures that touch SaveManager should inherit this instead of
    /// <see cref="PlayModeTestBase"/>.
    /// </summary>
    public abstract class PlayModeSaveFileTestBase : PlayModeTestBase
    {
        private const string SandboxRootName = "TestSaves";

        private string sandboxDirectory;

        /// <summary>The sandbox save file the test's SaveManager writes.</summary>
        protected string SavePath => Path.Combine(sandboxDirectory, "save.json");

        /// <summary>The sandbox backup SaveManager keeps.</summary>
        protected string BackupPath => SavePath + ".bak";

        /// <summary>The sandbox temp file SaveManager writes before replacing the save.</summary>
        protected string TempPath => SavePath + ".tmp";

        [UnitySetUp]
        public IEnumerator SandboxSaveFiles()
        {
            sandboxDirectory = Path.Combine(
                Application.persistentDataPath, SandboxRootName, System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sandboxDirectory);

            // Redirect every SaveManager file operation away from the player's real save location.
            SaveManager.SaveDirectoryOverride = sandboxDirectory;

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreSaveFiles()
        {
            SaveManager.SaveDirectoryOverride = null;

            if (!string.IsNullOrEmpty(sandboxDirectory) && Directory.Exists(sandboxDirectory))
            {
                try { Directory.Delete(sandboxDirectory, recursive: true); }
                catch (IOException e) { Debug.LogWarning($"[PlayModeSaveFileTestBase] Sandbox cleanup failed: {e.Message}"); }
            }
            sandboxDirectory = null;

            yield return null;
        }
    }
}
