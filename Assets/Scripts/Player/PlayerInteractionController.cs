using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public class PlayerInteractionController : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField, Min(0.5f)] private float interactionSearchRadius = 2f;
    [SerializeField] private LayerMask npcLayers = Physics2D.DefaultRaycastLayers;
    [SerializeField] private DialogueUIController dialogueUI;
    [SerializeField] private PlayerController2D playerController;

    [Header("Legacy Input Fallback")]
    [SerializeField] private KeyCode legacyInteractKey = KeyCode.E;
    [SerializeField] private string legacyInteractButton = "Submit";
    private readonly Collider2D[] overlapResults = new Collider2D[12];
    private NpcDialogue activeDialogue;
    private DialogueNodeDefinition activeNode;
    private int selectedChoiceIndex;

    private void Awake()
    {
        dialogueUI = dialogueUI != null ? dialogueUI : DialogueUIController.GetOrCreate();
        playerController = playerController != null ? playerController : GetComponent<PlayerController2D>();
    }
    private void OnDisable()
    {
        SetPlayerMovementLocked(false);
    }
    private void Update()
    {
        if (activeDialogue != null)
        {
            if (WasCancelPressedThisFrame())
            {
                EndDialogue();
                return;
            }

            if (activeDialogue.Controller != null && !activeDialogue.Controller.CanInteract(transform.position))
            {
                EndDialogue();
                return;
            }

            if (HasMultipleChoices() && WasChoiceUpPressedThisFrame())
            {
                MoveChoiceSelection(-1);
                return;
            }

            if (HasMultipleChoices() && WasChoiceDownPressedThisFrame())
            {
                MoveChoiceSelection(1);
                return;
            }

            if (WasInteractPressedThisFrame())
            {
                AdvanceDialogue();
            }

            return;
        }

        if (WasInteractPressedThisFrame())
        {
            TryStartNearestDialogue();
        }
    }

    private void TryStartNearestDialogue()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, interactionSearchRadius, overlapResults, npcLayers);
    NpcDialogue nearestDialogue = null;
    float nearestDistanceSqr = float.MaxValue;

    for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = overlapResults[i];
            if (hit == null)
            {
                continue;
            }

            NpcDialogue candidate = hit.GetComponentInParent<NpcDialogue>();
            if (candidate == null || !candidate.CanStartDialogue(transform.position))
            {
                continue;
            }
            Vector3 speakerPosition = candidate.Controller != null ? candidate.Controller.InteractionPosition : candidate.transform.position;
            float distanceSqr = (speakerPosition - transform.position).sqrMagnitude;
            if (distanceSqr >= nearestDistanceSqr)
            {
                continue;
            }

            nearestDialogue = candidate;
            nearestDistanceSqr = distanceSqr;
        }

        if (nearestDialogue == null)
        {
            return;
        }

        activeDialogue = nearestDialogue;
    selectedChoiceIndex = 0;
    activeDialogue.BeginConversation();
    SetPlayerMovementLocked(true);
        if (!activeDialogue.TryGetStartNode(out activeNode))
        {
            EndDialogue();
            return;
        }
        ShowCurrentNode();
    }

    private void AdvanceDialogue()
    {
        if (activeDialogue == null || activeNode == null)
        {
            return;
        }

        List<DialogueChoiceDefinition> choices = activeNode.choices;
        if (choices != null && choices.Count > 0)
        {
            selectedChoiceIndex = Mathf.Clamp(selectedChoiceIndex, 0, choices.Count - 1);
            DialogueChoiceDefinition selectedChoice = choices[selectedChoiceIndex];
            if (selectedChoice == null)
            {
                EndDialogue();
                return;
            }

            if (selectedChoice.endConversation || string.IsNullOrWhiteSpace(selectedChoice.nextNodeId))
            {
                EndDialogue();
                return;
            }

            MoveToNode(selectedChoice.nextNodeId);
            return;
        }

        if (activeNode.endConversation || string.IsNullOrWhiteSpace(activeNode.nextNodeId))
        {
            EndDialogue();
            return;
        }

        MoveToNode(activeNode.nextNodeId);
    }

    private void MoveToNode(string nodeId)
    {
        if (activeDialogue == null)
        {
            return;
        }

        if (!activeDialogue.TryGetNode(nodeId, out activeNode))
        {
            EndDialogue();
            return;
        }

        selectedChoiceIndex = 0;
        ShowCurrentNode();
    }

    private void ShowCurrentNode()
    {
        if (activeDialogue == null || activeNode == null || dialogueUI == null)
        {
            return;
        }

        string speaker = activeDialogue.GetSpeakerNameForNode(activeNode);
        List<string> choiceTexts = null;

        if (activeNode.choices != null && activeNode.choices.Count > 0)
        {
            choiceTexts = new List<string>(activeNode.choices.Count);
            for (int i = 0; i < activeNode.choices.Count; i++)
            {
                DialogueChoiceDefinition choice = activeNode.choices[i];
                choiceTexts.Add(choice != null ? choice.text : string.Empty);
            }
        }

        dialogueUI.ShowDialogue(speaker, activeNode.text, choiceTexts, selectedChoiceIndex);
    }

    private void MoveChoiceSelection(int direction)
    {
        if (!HasMultipleChoices())
        {
            return;
        }

        int count = activeNode.choices.Count;
        selectedChoiceIndex = (selectedChoiceIndex + direction + count) % count;
        ShowCurrentNode();
    }

    private bool HasMultipleChoices()
    {
        return activeNode != null && activeNode.choices != null && activeNode.choices.Count > 1;
    }

    private void EndDialogue()
    {
        if (activeDialogue != null)
        {
            activeDialogue.EndConversation();
            activeDialogue = null;
        }

        activeNode = null;
        selectedChoiceIndex = 0;

        if (dialogueUI != null)
        {
            dialogueUI.HideDialogue();
        }

        SetPlayerMovementLocked(false);
    }

    private void SetPlayerMovementLocked(bool locked)
    {
        if (playerController == null)
        {
            return;
        }

        playerController.SetMovementEnabled(!locked);
    }

    private bool WasInteractPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            return true;
        }

        return false;
#else
        if (Input.GetKeyDown(legacyInteractKey))
        {
            return true;
        }

        return !string.IsNullOrEmpty(legacyInteractButton) && Input.GetButtonDown(legacyInteractButton);
#endif
    }

    private bool WasCancelPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            return true;
        }

        return false;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private bool WasChoiceUpPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame))
        {
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.dpad.up.wasPressedThisFrame)
        {
            return true;
        }

        return false;
#else
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W);
#endif
    }

    private bool WasChoiceDownPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame))
        {
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.dpad.down.wasPressedThisFrame)
        {
            return true;
        }

        return false;
#else
        return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S);
#endif
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionSearchRadius);
    }
}
