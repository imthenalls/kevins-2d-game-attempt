using System;
using System.Collections.Generic;

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