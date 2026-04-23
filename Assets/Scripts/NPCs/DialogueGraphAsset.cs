using UnityEngine;

[CreateAssetMenu(fileName = "DialogueGraph", menuName = "Game/Dialogue Graph")]
public class DialogueGraphAsset : ScriptableObject
{
    [SerializeField] private DialogueGraphDefinition graph = new DialogueGraphDefinition();

    public DialogueGraphDefinition Graph => graph;
    public string DialogueId => graph != null ? graph.dialogueId : string.Empty;
}