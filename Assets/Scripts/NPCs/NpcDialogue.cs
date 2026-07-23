using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bridges an NPC's identity (NpcController) with a dialogue graph so that
/// PlayerInteractionController can start, navigate, and end conversations.
///
/// Dialogue can come from two sources — assign whichever you use:
///   A) dialogueAsset  — a DialogueGraphAsset ScriptableObject assigned in the Inspector.
///   B) dialogueId     — a string id matching an entry in StreamingAssets/dialogues.json.
///   If both are set, the asset takes precedence.
///
/// Unity setup:
///   1. Add to an NPC GameObject that already has NpcController (required component).
///   2. Either drag a DialogueGraphAsset into the Dialogue Asset field, or
///      type a Dialogue Id that matches an entry in dialogues.json.
///   3. No further wiring needed — PlayerInteractionController discovers and drives this.
///   The NPC enters NpcBehaviorState.Talking while a conversation is active, pausing
///   any NpcBehaviorManager-driven movement.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NpcController))]
public class NpcDialogue : MonoBehaviour
{
    [SerializeField] private string dialogueId;
    [SerializeField] private DialogueGraphAsset dialogueAsset;

    private NpcController npcController;
    private readonly Dictionary<string, DialogueNodeDefinition> nodeLookup = new Dictionary<string, DialogueNodeDefinition>(StringComparer.OrdinalIgnoreCase);
    private DialogueGraphDefinition activeGraph;

    public NpcController Controller => npcController;
    public string SpeakerName => npcController != null ? npcController.DisplayName : gameObject.name;
    public string DialogueId => dialogueId;

    private void Awake()
    {
        npcController = GetComponent<NpcController>();
        ResolveDialogueData();
    }

    private void OnValidate()
    {
        if (dialogueAsset != null && string.IsNullOrWhiteSpace(dialogueId))
        {
            dialogueId = dialogueAsset.DialogueId;
        }
    }

    public bool CanStartDialogue(Vector3 interactorPosition)
    {
        EnsureDialogueResolved();
        return enabled && activeGraph != null && nodeLookup.Count > 0 && npcController != null && npcController.CanInteract(interactorPosition);
    }

    public bool TryGetStartNode(out DialogueNodeDefinition node)
    {
        EnsureDialogueResolved();
        node = null;

        if (activeGraph == null || nodeLookup.Count == 0)
        {
            return false;
        }

        string startNodeId = string.IsNullOrWhiteSpace(activeGraph.startNodeId) ? activeGraph.nodes[0].id : activeGraph.startNodeId;
        return TryGetNode(startNodeId, out node);
    }

    public bool TryGetNode(string nodeId, out DialogueNodeDefinition node)
    {
        EnsureDialogueResolved();
        node = null;

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        return nodeLookup.TryGetValue(nodeId, out node);
    }

    public string GetSpeakerNameForNode(DialogueNodeDefinition node)
    {
        if (node != null && !string.IsNullOrWhiteSpace(node.speakerName))
        {
            return node.speakerName;
        }

        return SpeakerName;
    }

    public void BeginConversation()
    {
        if (npcController != null)
        {
            npcController.SetBehaviorState(NpcBehaviorState.Talking);
        }
    }

    public void EndConversation()
    {
        if (npcController != null && npcController.BehaviorState == NpcBehaviorState.Talking)
        {
            npcController.SetBehaviorState(NpcBehaviorState.Idle);
        }
    }

    private void EnsureDialogueResolved()
    {
        if (activeGraph == null || nodeLookup.Count == 0)
        {
            ResolveDialogueData();
        }
    }

    private void ResolveDialogueData()
    {
        activeGraph = null;
        nodeLookup.Clear();

        if (dialogueAsset != null && dialogueAsset.Graph != null)
        {
            DialogueDatabase.RegisterAsset(dialogueAsset);

            if (string.IsNullOrWhiteSpace(dialogueId))
            {
                dialogueId = dialogueAsset.DialogueId;
            }
        }

        if (!string.IsNullOrWhiteSpace(dialogueId) && DialogueDatabase.TryGetDialogue(dialogueId, out DialogueGraphDefinition graphFromId))
        {
            activeGraph = graphFromId;
        }
        else if (dialogueAsset != null)
        {
            activeGraph = dialogueAsset.Graph;
        }

        if (activeGraph == null || activeGraph.nodes == null)
        {
            return;
        }

        for (int i = 0; i < activeGraph.nodes.Count; i++)
        {
            DialogueNodeDefinition node = activeGraph.nodes[i];
            if (node == null || string.IsNullOrWhiteSpace(node.id))
            {
                continue;
            }

            nodeLookup[node.id] = node;
        }
    }

    /// <summary>
    /// Replace the active dialogue graph at runtime.
    /// Called by WorldStateDialogueSelector to swap NPC dialogue based on world state flags.
    /// Passing null clears the active dialogue.
    /// </summary>
    public void SelectDialogue(DialogueGraphAsset asset)
    {
        dialogueAsset = asset;
        dialogueId    = asset != null ? asset.DialogueId : string.Empty;
        ResolveDialogueData();
    }
}
