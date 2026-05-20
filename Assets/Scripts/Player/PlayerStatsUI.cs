using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives HP and MP UI bars by listening to PlayerStats events.
/// 
/// Setup:
///   1. Create a Canvas (Screen Space – Overlay).
///   2. For each bar, add a background Image and a child "Fill" Image
///      with Image Type = Filled, Fill Method = Horizontal.
///   3. Assign the Fill Images (not the backgrounds) to hpFill / mpFill.
///   4. Assign the PlayerStats component to playerStats, or leave it null
///      and this script will find it on Start via FindFirstObjectByType.
/// </summary>
public class PlayerStatsUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EntityStats playerStats;

    [Header("HP Bar")]
    [SerializeField] private Image hpFill;

    [Header("MP Bar")]
    [SerializeField] private Image mpFill;

    private void Start()
    {
        if (playerStats == null)
            playerStats = FindFirstObjectByType<EntityStats>();

        if (playerStats == null)
        {
            Debug.LogWarning("[PlayerStatsUI] No EntityStats found in scene.");
            return;
        }

        playerStats.OnHpChanged += HandleHpChanged;
        playerStats.OnMpChanged += HandleMpChanged;

        // Initialise bars to current values
        RefreshBars();
    }

    private void OnDestroy()
    {
        if (playerStats != null)
        {
            playerStats.OnHpChanged -= HandleHpChanged;
            playerStats.OnMpChanged -= HandleMpChanged;
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
        HandleHpChanged(playerStats.Hp, playerStats.MaxHp);
        HandleMpChanged(playerStats.Mp, playerStats.MaxMp);
    }
}
