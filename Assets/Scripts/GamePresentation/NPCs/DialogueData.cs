using System;
using System.Collections.Generic;

/// <summary>
/// Plain serializable data classes that map 1-to-1 with the JSON dialogue format.
/// Used for deserialization by DialogueDatabase (from dialogues.json) and
/// DialogueGraphAsset (from a ScriptableObject). Not a MonoBehaviour.
///
/// Dialogue graph structure:
///   DialogueDatabaseJson      — root container (list of graphs, used in dialogues.json).
///     DialogueGraphDefinition — one complete conversation identified by dialogueId.
///       startNodeId           — id of the first node to show (defaults to first in list).
///       fallbackStartNodeId   — start node used when startNodeId's condition is not met (optional).
///       DialogueNodeDefinition— one line of dialogue.
///         text                — what the speaker says.
///         speakerName         — overrides NPC display name for this line (optional).
///         nextNodeId          — advances to this node after the player confirms (linear).
///         endConversation     — set true to close the dialogue on this node.
///         choices             — list of player response options (branching dialogue).
///           DialogueChoiceDefinition — one selectable response with its own nextNodeId.
///             Optional questId / questSourceNodeId / questTargetNodeId select a manual
///             quest transition when the response is confirmed.
///       Optional gating: a node or choice carrying requireQuestId (quest must be active) and/or
///       requireQuestNodeId (quest must be at that node) is only used while that holds true. This
///       lets a greeting change once a quest has begun, and hides responses that do not apply yet.
///
/// Unity setup: none — these are pure data containers, not components.
/// </summary>
[Serializable]
public class DialogueDatabaseJson
{
    public int version = 1;
    public List<DialogueGraphDefinition> dialogues = new List<DialogueGraphDefinition>();
}

[Serializable]
public class DialogueGraphDefinition
{
    public string dialogueId;
    public string startNodeId = "start";
    /// <summary>Start node used when startNodeId's condition is not met (optional).</summary>
    public string fallbackStartNodeId;
    public List<DialogueNodeDefinition> nodes = new List<DialogueNodeDefinition>();
}

[Serializable]
public class DialogueNodeDefinition
{
    public string id;
    public string speakerName;
    public string text;
    public string nextNodeId;
    public bool endConversation;
    public List<DialogueChoiceDefinition> choices = new List<DialogueChoiceDefinition>();
    /// <summary>Quest that must be active for this node to be used (optional).</summary>
    public string requireQuestId;
    /// <summary>Quest node that must be active for this node to be used (optional).</summary>
    public string requireQuestNodeId;
}

[Serializable]
public class DialogueChoiceDefinition
{
    public string text;
    public string nextNodeId;
    public bool endConversation;
    public string questId;
    public string questSourceNodeId;
    public string questTargetNodeId;
    public string startQuestId;
    public string teleportPortalId;
    public string teleportScene;
    /// <summary>Quest that must be active for this choice to be shown (optional).</summary>
    public string requireQuestId;
    /// <summary>Quest node that must be active for this choice to be shown (optional).</summary>
    public string requireQuestNodeId;
}
