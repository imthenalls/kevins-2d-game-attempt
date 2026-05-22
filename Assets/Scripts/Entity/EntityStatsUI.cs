using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives HP and MP UI bars by listening to EntityStats events.
/// Works for any entity — player, NPC, or enemy.
///
/// Setup:
///   1. Create a Canvas (Screen Space – Overlay).
///   2. For each bar, add a background Image and a child "Fill" Image
///      with Image Type = Filled, Fill Method = Horizontal.
///   3. Assign the Fill Images (not the backgrounds) to hpFill / mpFill.
///   4. Assign the EntityStats component to entityStats, or leave it null
///      and this script will find it on Start via FindFirstObjectByType.
/// </summary>
public class EntityStatsUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EntityStats entityStats;

    [Header("HP Bar")]
    [SerializeField] private Image hpFill;

    [Header("MP Bar")]
    [SerializeField] private Image mpFill;

    private void Start()
    {
        if (entityStats == null)
            entityStats = FindFirstObjectByType<EntityStats>();

        if (entityStats == null)
        {
            Debug.LogWarning("[EntityStatsUI] No EntityStats found in scene.");
            return;
        }

        entityStats.OnHpChanged += HandleHpChanged;
        entityStats.OnMpChanged += HandleMpChanged;

        // Initialise bars to current values
        RefreshBars();
    }

    private void OnDestroy()
    {
        if (entityStats != null)
        {
            entityStats.OnHpChanged -= HandleHpChanged;
            entityStats.OnMpChanged -= HandleMpChanged;
        }
    }

    private void HandleHpChanged(int current, int max)
    {
        if (hpFill != null)
            hpFill.fillAmount = max > 0 ? (float)current / max : 0f;
    }

    private void HandleMpChanged(int current, int max)
    {
        if (mpFill != null)
            mpFill.fillAmount = max > 0 ? (float)current / max : 0f;
    }

    private void RefreshBars()
    {
        HandleHpChanged(entityStats.Hp, entityStats.MaxHp);
        HandleMpChanged(entityStats.Mp, entityStats.MaxMp);
    }
}
