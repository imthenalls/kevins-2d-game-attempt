#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Produces read-only JSON and Markdown maps from PortalTrigger2D components.
/// The generated files are documentation only and are never read at runtime.
/// </summary>
public static class PortalMapExporter
{
    private const string OutputDirectory = "Documents/Generated";
    private const string JsonOutputPath = OutputDirectory + "/portal-map.json";
    private const string MarkdownOutputPath = OutputDirectory + "/portal-map.md";

    [Serializable]
    private class PortalMapDocument
    {
        public int version = 1;
        public List<PortalMapEntry> portals = new List<PortalMapEntry>();
        public List<string> validationErrors = new List<string>();
    }

    [Serializable]
    private class PortalMapEntry
    {
        public string id;
        public string objectName;
        public string scene;
        public string destinationScene;
        public string destinationPortalId;
        public List<string> incomingPortals = new List<string>();
        public List<string> additionalIncomingSources = new List<string>();
        public bool hasExitPoint;
        public SerializablePortalPosition exitPosition;
    }

    [Serializable]
    private class SerializablePortalPosition
    {
        public float x;
        public float y;
        public float z;

        public SerializablePortalPosition(Vector3 value)
        {
            x = value.x;
            y = value.y;
            z = value.z;
        }
    }

    [MenuItem("Tools/Portals/Export Portal Map")]
    public static void ExportPortalMap()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            PortalMapDocument document = CollectPortalMap();
            Validate(document);
            WriteOutputs(document);

            string message =
                $"Exported {document.portals.Count} portals to:\n" +
                $"{JsonOutputPath}\n{MarkdownOutputPath}";

            if (document.validationErrors.Count > 0)
            {
                message += $"\n\nFound {document.validationErrors.Count} validation issue(s).";
                Debug.LogWarning(message);
            }
            else
            {
                Debug.Log(message);
            }
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
    }

    private static PortalMapDocument CollectPortalMap()
    {
        PortalMapDocument document = new PortalMapDocument();
        string[] scenePaths = GetProjectScenePaths();

        for (int i = 0; i < scenePaths.Length; i++)
        {
            string scenePath = scenePaths[i];
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            PortalTrigger2D[] portals =
                UnityEngine.Object.FindObjectsByType<PortalTrigger2D>(FindObjectsInactive.Include);

            for (int j = 0; j < portals.Length; j++)
            {
                PortalTrigger2D portal = portals[j];
                string destinationScene = string.IsNullOrWhiteSpace(portal.DestinationScene)
                    ? scene.name
                    : portal.DestinationScene;

                document.portals.Add(new PortalMapEntry
                {
                    id = portal.PortalId,
                    objectName = portal.gameObject.name,
                    scene = scene.name,
                    destinationScene = destinationScene,
                    destinationPortalId = portal.DestinationPortalId,
                    additionalIncomingSources = portal.AdditionalIncomingSources
                        .Where(source => !string.IsNullOrWhiteSpace(source))
                        .Select(source => source.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    hasExitPoint = portal.ExitPoint != null,
                    exitPosition = new SerializablePortalPosition(portal.ArrivalPosition)
                });
            }
        }

        document.portals = document.portals
            .OrderBy(entry => entry.scene, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        PopulateIncomingPortals(document);
        return document;
    }

    private static void PopulateIncomingPortals(PortalMapDocument document)
    {
        for (int i = 0; i < document.portals.Count; i++)
        {
            PortalMapEntry source = document.portals[i];
            PortalMapEntry destination = document.portals.FirstOrDefault(candidate =>
                string.Equals(candidate.scene, source.destinationScene, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.id, source.destinationPortalId, StringComparison.OrdinalIgnoreCase));

            if (destination == null)
            {
                continue;
            }

            destination.incomingPortals.Add($"{source.scene}/{source.id}");
        }

        for (int i = 0; i < document.portals.Count; i++)
        {
            document.portals[i].incomingPortals = document.portals[i].incomingPortals
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    private static string[] GetProjectScenePaths()
    {
        return AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void Validate(PortalMapDocument document)
    {
        Dictionary<string, PortalMapEntry> portalsById =
            new Dictionary<string, PortalMapEntry>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < document.portals.Count; i++)
        {
            PortalMapEntry portal = document.portals[i];

            if (string.IsNullOrWhiteSpace(portal.id))
            {
                document.validationErrors.Add(
                    $"Scene '{portal.scene}', object '{portal.objectName}' has no portal ID.");
                continue;
            }

            if (portalsById.TryGetValue(portal.id, out PortalMapEntry duplicate))
            {
                document.validationErrors.Add(
                    $"Duplicate portal ID '{portal.id}' on '{duplicate.scene}/{duplicate.objectName}' " +
                    $"and '{portal.scene}/{portal.objectName}'.");
            }
            else
            {
                portalsById.Add(portal.id, portal);
            }

            if (!portal.hasExitPoint)
            {
                document.validationErrors.Add(
                    $"Portal '{portal.id}' in scene '{portal.scene}' has no Exit Point.");
            }

            if (string.IsNullOrWhiteSpace(portal.destinationPortalId))
            {
                document.validationErrors.Add(
                    $"Portal '{portal.id}' in scene '{portal.scene}' has no destination portal ID.");
            }
        }

        for (int i = 0; i < document.portals.Count; i++)
        {
            PortalMapEntry source = document.portals[i];
            if (string.IsNullOrWhiteSpace(source.destinationPortalId))
            {
                continue;
            }

            bool destinationExists = document.portals.Any(candidate =>
                string.Equals(candidate.scene, source.destinationScene, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.id, source.destinationPortalId, StringComparison.OrdinalIgnoreCase));

            if (!destinationExists)
            {
                document.validationErrors.Add(
                    $"Portal '{source.id}' targets missing portal " +
                    $"'{source.destinationScene}/{source.destinationPortalId}'.");
            }
        }
    }

    private static void WriteOutputs(PortalMapDocument document)
    {
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(JsonOutputPath, JsonUtility.ToJson(document, true) + Environment.NewLine);
        File.WriteAllText(MarkdownOutputPath, BuildMarkdown(document));
    }

    private static string BuildMarkdown(PortalMapDocument document)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("# Portal Map");
        builder.AppendLine();
        builder.AppendLine("> Generated from `PortalTrigger2D` components. Do not edit this file as configuration.");
        builder.AppendLine();
        builder.AppendLine("| Portal | Scene | Destination | Incoming portals | Additional incoming sources | Exit point |");
        builder.AppendLine("|---|---|---|---|---|---|");

        for (int i = 0; i < document.portals.Count; i++)
        {
            PortalMapEntry portal = document.portals[i];
            builder.Append("| `")
                .Append(portal.id)
                .Append("` | ")
                .Append(portal.scene)
                .Append(" | `")
                .Append(portal.destinationScene)
                .Append("/")
                .Append(portal.destinationPortalId)
                .Append("` | ")
                .Append(FormatList(portal.incomingPortals))
                .Append(" | ")
                .Append(FormatList(portal.additionalIncomingSources))
                .Append(" | ")
                .Append(portal.hasExitPoint ? "Yes" : "Missing")
                .AppendLine(" |");
        }

        builder.AppendLine();
        builder.AppendLine("## Route List");
        builder.AppendLine();
        for (int i = 0; i < document.portals.Count; i++)
        {
            PortalMapEntry portal = document.portals[i];
            builder.Append("- `")
                .Append(portal.scene)
                .Append("/")
                .Append(portal.id)
                .Append("` → `")
                .Append(portal.destinationScene)
                .Append("/")
                .Append(portal.destinationPortalId)
                .AppendLine("`");
        }

        builder.AppendLine();
        builder.AppendLine("## Validation");
        builder.AppendLine();

        if (document.validationErrors.Count == 0)
        {
            builder.AppendLine("- No portal configuration errors found.");
        }
        else
        {
            for (int i = 0; i < document.validationErrors.Count; i++)
            {
                builder.Append("- ")
                    .AppendLine(document.validationErrors[i]);
            }
        }

        return builder.ToString();
    }

    private static string FormatList(List<string> values)
    {
        return values == null || values.Count == 0
            ? "—"
            : string.Join("<br>", values.Select(value => $"`{value}`"));
    }
}
#endif
