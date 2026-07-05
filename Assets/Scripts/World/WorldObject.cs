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
    [Header("Identity")]
    [SerializeField] private string displayName = "Object";
    [SerializeField, Min(0.25f)] private float interactionRange = 1.5f;

    [Header("Text")]
    [Tooltip("Each element is one page of text shown in the dialogue box. Player presses E to advance.")]
    [SerializeField, TextArea(2, 5)] private string[] lines = { "..." };

    [Header("Reward")]
    [Tooltip("Item given to the player when they finish reading. Leave blank for no reward.")]
    [SerializeField] private ItemData rewardItem;
    [SerializeField, Min(1)] private int rewardQuantity = 1;

    [Header("Settings")]
    [Tooltip("Disable this object after the first full interaction. Uncheck for re-readable objects.")]
    [SerializeField] private bool oneTimeOnly = true;

    // ── State ─────────────────────────────────────────────────────────────────

    private int  _currentLine;
    private bool _used;

    // ── IInteractable ─────────────────────────────────────────────────────────

    public bool CanInteract(Vector3 worldPosition) =>
        enabled && !_used && lines != null && lines.Length > 0 &&
        Vector2.Distance(transform.position, worldPosition) <= interactionRange;

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

        if (completed && rewardItem != null)
        {
            var inv = InventoryUI.Model;
            if (inv != null)
            {
                int leftover = inv.AddItem(rewardItem, rewardQuantity);
                int taken    = rewardQuantity - leftover;

                if (taken > 0)
                {
                    if (interactor.TryGetComponent<CharacterStatistics>(out var stats))
                        stats.RecordItemGathered(taken);

                    QuestEventBus.Raise("ItemCollected", rewardItem.itemId, taken);
                }
            }
        }

        if (completed)
            QuestEventBus.Raise("ObjectInteracted", displayName);

        // Reset for next use, or disable if one-time
        _currentLine = 0;
        if (oneTimeOnly && completed)
        {
            _used = true;
            gameObject.SetActive(false);
        }
    }

    // ── Editor ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _used ? Color.gray : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
