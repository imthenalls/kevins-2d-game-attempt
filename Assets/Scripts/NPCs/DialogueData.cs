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
///       DialogueNodeDefinition— one line of dialogue.
///         text                — what the speaker says.
///         speakerName         — overrides NPC display name for this line (optional).
///         nextNodeId          — advances to this node after the player confirms (linear).
///         endConversation     — set true to close the dialogue on this node.
///         choices             — list of player response options (branching dialogue).
///           DialogueChoiceDefinition — one selectable response with its own nextNodeId.
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
}

[Serializable]
public class DialogueChoiceDefinition
{
    public string text;
    public string nextNodeId;
    public bool endConversation;
}
