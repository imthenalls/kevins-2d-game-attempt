using Game.Core;
using UnityEngine;

/// <summary>
/// General-purpose interactable world object (sign, chest, shrine, notice board, etc.).
/// When the player presses the interact key within range, it pages through Lines one by one
/// using the existing dialogue UI. An optional item reward is given when the last line is read.
/// One-time objects disable themselves afterwards; repeatable ones reset for next use.
///
/// Implements IInteractable — PlayerInteractionController discovers and drives it automatically
/// as long as this GameObject is on a layer included in the controller's Interactable Layers mask.
///
/// Unity setup:
///   1. Create a GameObject with a SpriteRenderer and any Collider2D.
///   2. Add this component.
///   3. Set the GameObject's physics layer to match the Interactable Layers mask on
///      PlayerInteractionController (create a dedicated "Interactable" layer for clarity).
///   4. Set Display Name — shown as the speaker header in the dialogue box.
///   5. Fill Lines — each element is one page of text the player reads through.
///   6. Optionally assign Reward Item + Reward Quantity to give an item on completion.
///   7. One Time Only — disables the GameObject after the first full read.
///      Leave off for signs/noticeboards that can be re-read.
///   8. Adjust Interaction Range to control how close the player must be.
///
/// Quest integration (automatic on completion):
///   QuestEventBus.Raise("ObjectInteracted", displayName)
///   QuestEventBus.Raise("ItemCollected", rewardItem.itemId, taken)  — if reward given
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WorldObject : MonoBehaviour, IInteractable
{
    [Header("Config (Game.Data)")]
    [SerializeField] private WorldObjectConfig config = new WorldObjectConfig();

    [Header("Identity")]
    [SerializeField] private string displayName = "Object";

    [Tooltip("Stable id used to remember one-time completion across scene reloads and saves. " +
             "Leave blank to derive a stable id from the scene, name, and position.")]
    [SerializeField] private string objectId;

    [Header("Text")]
    [Tooltip("Each element is one page of text shown in the dialogue box. Player presses Space to advance.")]
    [SerializeField, TextArea(2, 5)] private string[] lines = { "..." };

    [Header("Reward")]
    [Tooltip("Item given to the player when they finish reading. Leave blank for no reward.")]
    [SerializeField] private ItemData rewardItem;

    // ── State ─────────────────────────────────────────────────────────────────

    private int  _currentLine;
    private bool _used;
    private string _resolvedId;
    private WorldObjectInteractionModel _interaction;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        // A one-time object completed before this scene (re)loaded must not offer its reward again.
        _resolvedId ??= ResolveStableId();
        WorldObjectInteractionModel interaction = Interaction;
        if (config.OneTimeOnly && interaction != null && interaction.IsCompleted(_resolvedId))
        {
            _used = true;
            gameObject.SetActive(false);
        }
    }

    private WorldObjectInteractionModel Interaction =>
        _interaction ??= WorldStateManager.Instance != null
            ? new WorldObjectInteractionModel(WorldStateManager.Instance.Facts)
            : null;

    // Stable identity for the completed fact: the explicit id, or a deterministic scene/name/position key.
    private string ResolveStableId()
    {
        if (!string.IsNullOrWhiteSpace(objectId))
            return objectId.Trim();

        Vector3 p = transform.position;
        string sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : "Unknown";
        return $"{sceneName}:{displayName}:{p.x:0.##}_{p.y:0.##}_{p.z:0.##}";
    }

    // ── IInteractable ─────────────────────────────────────────────────────────

    public bool CanInteract(Vector3 worldPosition) =>
        enabled && !_used && lines != null && lines.Length > 0 &&
        Vector2.Distance(transform.position, worldPosition) <= config.InteractionRange;

    public string GetDisplayName() => displayName;

    public bool TryGetCurrentLine(out string line)
    {
        if (lines == null || _currentLine >= lines.Length)
        {
            line = string.Empty;
            return false;
        }
        line = lines[_currentLine];
        return true;
    }

    public void Advance() => _currentLine++;

    public void EndInteraction(GameObject interactor)
    {
        // Give reward on a completed read (all lines shown), not on cancel
        bool completed = _currentLine >= (lines != null ? lines.Length : 0);
        _currentLine = 0;

        if (!completed)
            return;

        _resolvedId ??= ResolveStableId();
        WorldObjectInteractionModel interaction = Interaction;

        // A one-time object that already completed (e.g. a reload raced the disable) must not pay out again.
        if (interaction != null && !interaction.CanComplete(_resolvedId, config.OneTimeOnly))
            return;

        // Deliver the reward first and retain any undelivered remainder as pending, so completion is
        // only committed once every unit of the reward is accounted for. Otherwise a full inventory
        // would permanently consume a one-time object and lose its reward.
        if (!TryDeliverReward(interactor))
            return;

        // The reward is delivered or safely pending: only now is it safe to record completion.
        // Committing after this point means a scene reload cannot hand out the one-time reward twice.
        interaction?.CommitCompletion(_resolvedId, config.OneTimeOnly);

        QuestEventBus.Raise("ObjectInteracted", displayName);

        if (config.OneTimeOnly)
        {
            _used = true;
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Gives the configured reward and returns true when the whole quantity was either delivered or
    /// retained as a pending reward. Returns false only when a remainder exists and no pending
    /// ledger is available, so the caller can leave the object unconsumed and let the player retry.
    /// </summary>
    private bool TryDeliverReward(GameObject interactor)
    {
        if (rewardItem == null || config.RewardQuantity <= 0)
            return true;

        int taken = InventoryHelper.GiveItem(rewardItem, config.RewardQuantity, interactor);
        int leftover = config.RewardQuantity - taken;
        if (leftover <= 0)
            return true;

        if (PendingRewardManager.Instance != null &&
            PendingRewardManager.Instance.RecordLeftover(_resolvedId, rewardItem.itemId, leftover))
        {
            return true;
        }

        if (GameSessionHost.Session != null)
        {
            GameSessionHost.Session.PendingRewards.Record(_resolvedId, rewardItem.itemId, leftover);
            return true;
        }

        Debug.LogWarning(
            $"[WorldObject] Reward '{rewardItem.itemId}' x{leftover} could not be delivered or " +
            "retained; completion deferred so the reward is not lost.");
        return false;
    }

    // ── Editor ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _used ? Color.gray : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, config.InteractionRange);
    }
}
