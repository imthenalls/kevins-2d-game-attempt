using System;

namespace Game.Core
{
    /// <summary>
    /// Engine-free completion and one-time-use rules for interactable world objects (signs, chests,
    /// shrines, notice boards). Completion is recorded in <see cref="WorldFacts"/> under a stable
    /// per-object key, so a one-time reward cannot be claimed again after the scene reloads or the
    /// game is saved and restarted.
    ///
    /// The Unity <c>WorldObject</c> adapter owns the dialogue paging, item delivery, and GameObject
    /// activation. It checks <see cref="CanComplete"/> first, delivers (or retains) the reward, then
    /// calls <see cref="CommitCompletion"/> so a one-time object is never consumed when the reward
    /// could not be accounted for.
    ///
    /// Unity setup: none.
    /// </summary>
    public sealed class WorldObjectInteractionModel
    {
        /// <summary>Fact-key prefix used to record completed world objects.</summary>
        public const string CompletionKeyPrefix = "worldObject.completed.";

        private readonly WorldFacts facts;

        public WorldObjectInteractionModel(WorldFacts facts)
            => this.facts = facts ?? throw new ArgumentNullException(nameof(facts));

        /// <summary>The stable fact key recording that object <paramref name="objectId"/> completed.</summary>
        public static string CompletionKey(string objectId) => CompletionKeyPrefix + objectId;

        /// <summary>True when a one-time object has already been completed.</summary>
        public bool IsCompleted(string objectId)
            => !string.IsNullOrWhiteSpace(objectId) && facts.HasFlag(CompletionKey(objectId));

        /// <summary>
        /// True when <paramref name="objectId"/> is still eligible to complete. A repeatable object
        /// is always eligible; a one-time object is eligible only before its completion fact is
        /// written. This changes no state, so a caller can attempt reward delivery before committing.
        /// </summary>
        public bool CanComplete(string objectId, bool oneTimeOnly)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return false;

            if (!oneTimeOnly)
                return true;

            return !facts.HasFlag(CompletionKey(objectId));
        }

        /// <summary>
        /// Commits completion, recording the completion fact for a one-time object. Returns false
        /// when the object is not eligible (blank id or already completed); check
        /// <see cref="CanComplete"/> first when the caller must deliver a reward before committing.
        /// </summary>
        public bool CommitCompletion(string objectId, bool oneTimeOnly)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return false;

            if (!oneTimeOnly)
                return true;

            string key = CompletionKey(objectId);
            if (facts.HasFlag(key))
                return false;

            facts.SetFlag(key);
            return true;
        }

        /// <summary>
        /// Convenience for callers that need no ordering between reward delivery and completion:
        /// checks eligibility and commits in one call. A one-time object completes only once; a
        /// repeatable object always returns true and records nothing.
        /// </summary>
        public bool TryComplete(string objectId, bool oneTimeOnly)
            => CanComplete(objectId, oneTimeOnly) && CommitCompletion(objectId, oneTimeOnly);
    }
}
