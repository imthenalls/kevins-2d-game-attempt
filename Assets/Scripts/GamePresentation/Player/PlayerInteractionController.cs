using System.Collections.Generic;
using Game.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Handles player–NPC interaction and walks through a dialogue graph node by node.
/// Each frame it listens for the interact key (E / gamepad South), then does an
/// OverlapCircle search for the nearest NpcDialogue within interactionSearchRadius
/// and begins the conversation. While in dialogue, Space advances text and confirms
/// choices until the graph ends or the player cancels.
///
/// Unity setup:
///   1. Add to the player GameObject alongside PlayerController2D.
///   2. Set NPC Layers to the Physics layer(s) your NPC GameObjects are on.
///   3. Optionally assign Dialogue UI (DialogueUIController) and Player Controller;
///      both are found automatically in the scene if left blank.
///   4. Adjust Interaction Search Radius to match your intended interaction range.
///
/// Controls:
///   Start conversation: E / gamepad South.
///   Advance dialogue / confirm choice: Space / gamepad South.
///   Up / Down (W–S / D-pad)       — navigate multi-choice option lists.
///   NPC dialogue is not dismissed by Escape; finish it with Space.
/// </summary>
[DisallowMultipleComponent]
public class PlayerInteractionController : MonoBehaviour
{
    [Header("Config (Game.Data)")]
    [SerializeField] private PlayerInteractionConfig config = new PlayerInteractionConfig();

    [Header("Unity References")]
    [SerializeField] private LayerMask npcLayers = Physics2D.DefaultRaycastLayers;
    [Tooltip("Layer(s) that WorldObject and other IInteractable objects are on.")]
    [SerializeField] private LayerMask interactableLayers = Physics2D.DefaultRaycastLayers;
    [SerializeField] private DialogueUIController dialogueUI;
    [SerializeField] private PlayerController2D playerController;
    private readonly Collider2D[] overlapResults = new Collider2D[12];
    private NpcDialogue activeDialogue;
    private DialogueNodeDefinition activeNode;
    private int selectedChoiceIndex;
    private IInteractable activeInteractable;

    private void Awake()
    {
        dialogueUI = dialogueUI != null ? dialogueUI : DialogueUIController.GetOrCreate();
        playerController = playerController != null ? playerController : GetComponent<PlayerController2D>();
    }
    private void OnDisable()
    {
        activeInteractable = null;
        SetPlayerMovementLocked(false);
    }
    private void Update()
    {
        if (activeDialogue != null)
        {
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

            if (WasDialogueAdvancePressedThisFrame())
            {
                AdvanceDialogue();
            }

            return;
        }

        if (activeInteractable != null)
        {
            if (WasCancelPressedThisFrame())
            {
                EndInteractable();
                return;
            }
            if (WasDialogueAdvancePressedThisFrame())
                AdvanceInteractable();
            return;
        }

        // Inventory owns the interact key while open (E equips its selected item).
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen)
            return;

        if (WasInteractPressedThisFrame())
        {
            TryStartNearestDialogue();
            if (activeDialogue == null)
                TryStartNearestInteractable();
        }
    }

    private void TryStartNearestDialogue()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, config.InteractionSearchRadius, overlapResults, npcLayers);
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

            if (!TryApplyManualQuestTransition(selectedChoice))
                return;

            if (!string.IsNullOrWhiteSpace(selectedChoice.teleportPortalId))
            {
                string portalId = selectedChoice.teleportPortalId;
                string destinationScene = selectedChoice.teleportScene;

                EndDialogue(completed: true);

                PortalManager portalManager = PortalManager.Instance;
                if (portalManager == null ||
                    !portalManager.TryTeleportToPortal(
                        portalId,
                        transform,
                        destinationScene))
                {
                    Debug.LogWarning(
                        $"[PlayerInteractionController] Could not teleport player to portal '{portalId}'.");
                }

                return;
            }

            if (selectedChoice.endConversation || string.IsNullOrWhiteSpace(selectedChoice.nextNodeId))
            {
                EndDialogue(completed: true);
                return;
            }

            MoveToNode(selectedChoice.nextNodeId);
            return;
        }

        if (activeNode.endConversation || string.IsNullOrWhiteSpace(activeNode.nextNodeId))
        {
            EndDialogue(completed: true);
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

    private static bool TryApplyManualQuestTransition(DialogueChoiceDefinition choice)
    {
        if (choice == null || string.IsNullOrWhiteSpace(choice.questTargetNodeId))
            return true;

        if (QuestManager.Instance == null || string.IsNullOrWhiteSpace(choice.questId))
        {
            Debug.LogWarning(
                "[PlayerInteractionController] Dialogue choice has a quest target but no " +
                "available QuestManager/questId.");
            return false;
        }

        bool advanced = string.IsNullOrWhiteSpace(choice.questSourceNodeId)
            ? QuestManager.Instance.TryChooseTransition(
                choice.questId,
                choice.questTargetNodeId)
            : QuestManager.Instance.TryChooseTransition(
                choice.questId,
                choice.questSourceNodeId,
                choice.questTargetNodeId);

        if (!advanced)
        {
            Debug.LogWarning(
                $"[PlayerInteractionController] Manual quest transition failed: " +
                $"{choice.questId} {choice.questSourceNodeId} -> {choice.questTargetNodeId}");
        }

        return advanced;
    }

    private void EndDialogue(bool completed = false)
    {
        NpcDialogue completedDialogue = activeDialogue;
        if (completedDialogue != null)
        {
            completedDialogue.EndConversation();
            activeDialogue = null;
        }

        activeNode = null;
        selectedChoiceIndex = 0;

        if (dialogueUI != null)
        {
            dialogueUI.HideDialogue();
        }

        SetPlayerMovementLocked(false);

        if (completed)
        {
            completedDialogue?.GiveInventoryGift(gameObject);
        }
    }

    // ── World interactable flow ───────────────────────────────────────────────

    private void TryStartNearestInteractable()
    {
        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position, config.InteractionSearchRadius, overlapResults, interactableLayers);

        IInteractable nearest         = null;
        float         nearestDistSqr  = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            if (overlapResults[i] == null) continue;
            var candidate = overlapResults[i].GetComponentInParent<IInteractable>();
            if (candidate == null || !candidate.CanInteract(transform.position)) continue;

            float distSqr = (overlapResults[i].transform.position - transform.position).sqrMagnitude;
            if (distSqr < nearestDistSqr)
            {
                nearestDistSqr = distSqr;
                nearest        = candidate;
            }
        }

        if (nearest == null) return;

        activeInteractable = nearest;
        SetPlayerMovementLocked(true);
        ShowCurrentInteractableLine();
    }

    private void ShowCurrentInteractableLine()
    {
        if (activeInteractable == null || dialogueUI == null) return;

        if (activeInteractable.TryGetCurrentLine(out string line))
            dialogueUI.ShowDialogue(activeInteractable.GetDisplayName(), line, null, 0);
        else
            EndInteractable();
    }

    private void AdvanceInteractable()
    {
        if (activeInteractable == null) return;
        activeInteractable.Advance();
        ShowCurrentInteractableLine();
    }

    private void EndInteractable()
    {
        activeInteractable?.EndInteraction(gameObject);
        activeInteractable = null;
        dialogueUI?.HideDialogue();
        // Don't unlock movement if EndInteraction opened the loot panel — it manages its own lock.
        if (!LootContainerUI.IsOpen)
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
        return Input.GetKeyDown((KeyCode)config.LegacyInteractKeyCode);
#endif
    }

    private bool WasDialogueAdvancePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return true;
        }

        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            return true;
        }

        return false;
#else
        if (Input.GetKeyDown((KeyCode)config.LegacyAdvanceKeyCode))
        {
            return true;
        }

        return !string.IsNullOrEmpty(config.LegacyInteractButton) && Input.GetButtonDown(config.LegacyInteractButton);
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
        Gizmos.DrawWireSphere(transform.position, config.InteractionSearchRadius);
    }
}
