using UnityEngine;

/// <summary>
/// ScriptableObject wrapper around a DialogueGraphDefinition.
/// Lets you author a complete dialogue graph as a Project asset in the Unity
/// Inspector instead of editing raw JSON.
/// Create via right-click → Game → Dialogue Graph in the Project window.
///
/// Unity setup:
///   1. Create a DialogueGraphAsset: right-click in Project → Game → Dialogue Graph.
///   2. Set dialogueId (must be unique across all dialogue sources).
///   3. Set startNodeId to the id of the first node.
///   4. Add nodes to the nodes list in the Inspector (or via a custom editor).
///   5. Drag this asset into the Dialogue Asset field on an NpcDialogue component.
///   NpcDialogue registers it with DialogueDatabase automatically on Awake.
/// </summary>
[CreateAssetMenu(fileName = "DialogueGraph", menuName = "Game/Dialogue Graph")]
public class DialogueGraphAsset : ScriptableObject
{
    [SerializeField] private DialogueGraphDefinition graph = new DialogueGraphDefinition();

    public DialogueGraphDefinition Graph => graph;
    public string DialogueId => graph != null ? graph.dialogueId : string.Empty;
}
