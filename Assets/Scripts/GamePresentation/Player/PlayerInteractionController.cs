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
///   1. Add to the player GameObject alongside PlayerControllerBase.
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
    [SerializeField] private PlayerControllerBase playerController;
    private readonly List<Collider2D> overlap2D = new List<Collider2D>();
    private readonly List<Component> candidates = new List<Component>();
    private static readonly Collider[] Overlap3D = new Collider[32];
    private readonly List<DialogueChoiceDefinition> availableChoices = new List<DialogueChoiceDefinition>();
    private NpcDialogue activeDialogue;
    private DialogueNodeDefinition activeNode;
    private int selectedChoiceIndex;
    private IInteractable activeInteractable;

    private void Awake()
    {
        dialogueUI = dialogueUI != null ? dialogueUI : DialogueUIController.GetOrCreate();
        playerController = playerController != null ? playerController : GetComponent<PlayerControllerBase>();
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

    // 2D scenes use Physics2D (the player has a Rigidbody2D); 3D planar-isometric scenes use Physics.
    private bool Is3D => !TryGetComponent<Rigidbody2D>(out _);

    // Fills results with collider components near the player on the layer mask, using 2D or 3D
    // physics to match the scene so the same interaction logic works in both.
    private void CollectTargets(int layerMask, List<Component> results)
    {
        results.Clear();

        if (Is3D)
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, config.InteractionSearchRadius, Overlap3D,
                layerMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
                results.Add(Overlap3D[i]);
            return;
        }

        var filter = new ContactFilter2D { useLayerMask = true, layerMask = layerMask, useTriggers = true };
        int hits = Physics2D.OverlapCircle(transform.position, config.InteractionSearchRadius, filter, overlap2D);
        for (int i = 0; i < hits; i++)
            results.Add(overlap2D[i]);
    }

    private void TryStartNearestDialogue()
    {
        CollectTargets(npcLayers, candidates);
        NpcDialogue nearestDialogue = null;
        float nearestDistanceSqr = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            Component hit = candidates[i];
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

        List<DialogueChoiceDefinition> choices = availableChoices;
        if (choices.Count > 0)
        {
            selectedChoiceIndex = Mathf.Clamp(selectedChoiceIndex, 0, choices.Count - 1);
            DialogueChoiceDefinition selectedChoice = choices[selectedChoiceIndex];
            if (selectedChoice == null)
            {
                EndDialogue();
                return;
            }

            // A choice that references a quest branch should work even when the player skipped the
            // quest giver (for example asking the caretaker for the key directly), so start the
            // referenced quest first when it is not already running.
            if (!string.IsNullOrWhiteSpace(selectedChoice.questId) &&
                QuestManager.Instance != null &&
                !QuestManager.Instance.IsQuestActive(selectedChoice.questId))
            {
                QuestManager.Instance.StartQuest(selectedChoice.questId);
            }

            // A failed manual transition (the quest already moved past this branch, or conditions
            // are unmet) must not leave the dialogue stuck on an option that does nothing. It is
            // logged in TryApplyManualQuestTransition; continue to the choice's next node / end.
            TryApplyManualQuestTransition(selectedChoice);

            // A choice can begin a quest directly (e.g. accepting a quest from an NPC).
            if (!string.IsNullOrWhiteSpace(selectedChoice.startQuestId) && QuestManager.Instance != null)
                QuestManager.Instance.StartQuest(selectedChoice.startQuestId);

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

        // Only offer choices whose quest gating currently holds (e.g. the caretaker's key response
        // is hidden until the key quest has actually begun).
        availableChoices.Clear();
        if (activeNode.choices != null)
        {
            for (int i = 0; i < activeNode.choices.Count; i++)
            {
                DialogueChoiceDefinition choice = activeNode.choices[i];
                if (choice == null || !DialogueGate.IsAvailable(choice.requireQuestId, choice.requireQuestNodeId))
                    continue;

                availableChoices.Add(choice);
            }
        }

        List<string> choiceTexts = null;
        if (availableChoices.Count > 0)
        {
            selectedChoiceIndex = Mathf.Clamp(selectedChoiceIndex, 0, availableChoices.Count - 1);
            choiceTexts = new List<string>(availableChoices.Count);
            for (int i = 0; i < availableChoices.Count; i++)
                choiceTexts.Add(availableChoices[i].text);
        }
        else
        {
            selectedChoiceIndex = 0;
        }

        dialogueUI.ShowDialogue(speaker, activeNode.text, choiceTexts, selectedChoiceIndex);
    }

    private void MoveChoiceSelection(int direction)
    {
        if (!HasMultipleChoices())
        {
            return;
        }

        int count = availableChoices.Count;
        selectedChoiceIndex = (selectedChoiceIndex + direction + count) % count;
        ShowCurrentNode();
    }

    private bool HasMultipleChoices()
    {
        return availableChoices.Count > 1;
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
        CollectTargets(interactableLayers, candidates);

        IInteractable nearest         = null;
        float         nearestDistSqr  = float.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == null) continue;
            var candidate = candidates[i].GetComponentInParent<IInteractable>();
            if (candidate == null || !candidate.CanInteract(transform.position)) continue;

            float distSqr = (candidates[i].transform.position - transform.position).sqrMagnitude;
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
