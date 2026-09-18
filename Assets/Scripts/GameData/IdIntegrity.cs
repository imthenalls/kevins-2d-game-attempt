using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Engine-free helpers for checking string-id integrity in authored data (items, NPCs,
    /// dialogue, quests, portals). The project leans on string ids, which otherwise fail only at
    /// runtime, so these checks back the Editor validation tool and are unit tested directly.
    ///
    /// All comparisons are case-insensitive, matching how the runtime resolves ids.
    ///
    /// Unity setup: none.
    /// </summary>
    public static class IdIntegrity
    {
        /// <summary>A reference from some owner to a string id (e.g. a quest action's itemId).</summary>
        public readonly struct IdReference
        {
            public readonly string Owner;
            public readonly string Id;

            public IdReference(string owner, string id)
            {
                Owner = owner ?? string.Empty;
                Id = id;
            }
        }

        /// <summary>
        /// Reports blank ids and ids that appear more than once.
        /// </summary>
        /// <param name="ids">The raw ids in authoring order.</param>
        /// <param name="label">What the ids name, e.g. "item" or "quest".</param>
        public static List<ValidationIssue> FindDuplicateOrBlankIds(
            IReadOnlyList<string> ids,
            string label)
        {
            var issues = new List<ValidationIssue>();
            if (ids == null)
                return issues;

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int blanks = 0;

            foreach (string id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    blanks++;
                    continue;
                }

                counts.TryGetValue(id, out int count);
                counts[id] = count + 1;
            }

            if (blanks > 0)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    label + ".blank",
                    blanks + " " + label + " entr" + (blanks == 1 ? "y has" : "ies have") + " a blank id."));
            }

            foreach (KeyValuePair<string, int> entry in counts)
            {
                if (entry.Value > 1)
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        label + ".duplicate",
                        "Duplicate " + label + " id '" + entry.Key + "' appears " + entry.Value + " times."));
                }
            }

            return issues;
        }

        /// <summary>
        /// Reports references that do not match any known id. Blank references are ignored, so a
        /// caller that treats blank as "unset" can pass everything through.
        /// </summary>
        public static List<ValidationIssue> FindDanglingReferences(
            IReadOnlyList<IdReference> references,
            IReadOnlyCollection<string> knownIds,
            string label)
        {
            var issues = new List<ValidationIssue>();
            if (references == null || references.Count == 0)
                return issues;

            var known = new HashSet<string>(knownIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (IdReference reference in references)
            {
                if (string.IsNullOrWhiteSpace(reference.Id) || known.Contains(reference.Id))
                    continue;

                if (!reported.Add(reference.Owner + "\u0000" + reference.Id))
                    continue;

                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    label + ".dangling",
                    "'" + reference.Owner + "' references unknown " + label + " id '" + reference.Id + "'."));
            }

            return issues;
        }
    }
}
