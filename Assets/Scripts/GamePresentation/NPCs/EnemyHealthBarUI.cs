using System;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Enemy health bar rendered with UGUI. Replaces the previous immediate-mode OnGUI drawing so the
/// bar no longer forces several native-to-managed GUI event callbacks per enemy per frame.
///
/// Unity setup: none. NpcController.Start creates one bar per Enemy NPC (when Show Enemy Health Bar
/// is enabled) on a shared screen-space overlay canvas created on demand and kept across scenes.
///
/// Runtime API: none. The bar is self-managing: it repositions each frame from its owner, refreshes
/// the fill and label from the owner's EntityStats, hides itself under the same conditions the old
/// OnGUI used, and destroys itself when its owner is gone.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyHealthBarUI : MonoBehaviour
{
    private const float Inset = 2f;
    private const float CanvasSortingOrder = 900f; // below the dialogue UI (1000)

    private static readonly Color BackingColor = new(0.25f, 0.04f, 0.04f, 1f);
    private static readonly Color FullHealthColor = new(0.9f, 0.12f, 0.08f, 1f);
    private static readonly Color LowHealthColor = new(0.2f, 0.85f, 0.2f, 1f);

    private static Canvas sharedCanvas;

    private NpcController owner;
    private RectTransform visual;
    private Image fill;
    private TextMeshProUGUI label;
    private float lastFillRatio = -1f;
    private string labelText = string.Empty;

    /// <summary>Creates a screen-anchored health bar that tracks the owner while it lives.</summary>
    public static EnemyHealthBarUI Create(NpcController owner)
    {
        if (owner == null)
            return null;

        EnsureCanvas();

        var barObject = new GameObject("Enemy Health Bar", typeof(RectTransform), typeof(EnemyHealthBarUI));
        barObject.transform.SetParent(sharedCanvas.transform, false);
        EnemyHealthBarUI bar = barObject.GetComponent<EnemyHealthBarUI>();
        bar.owner = owner;
        bar.Build();
        return bar;
    }

    private void Build()
    {
        RectTransform root = (RectTransform)transform;
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(owner.HealthBarScreenWidth, owner.HealthBarScreenHeight);
        root.anchoredPosition = Vector2.zero;

        visual = new GameObject("Visual", typeof(RectTransform)).GetComponent<RectTransform>();
        visual.SetParent(root, false);
        visual.anchorMin = Vector2.zero;
        visual.anchorMax = Vector2.one;
        visual.offsetMin = Vector2.zero;
        visual.offsetMax = Vector2.zero;

        Image shadow = CreateImage(visual, "Shadow", Color.black);
        Stretch(shadow.rectTransform);

        Image backing = CreateImage(visual, "Backing", BackingColor);
        backing.rectTransform.anchorMin = Vector2.zero;
        backing.rectTransform.anchorMax = Vector2.one;
        backing.rectTransform.offsetMin = new Vector2(Inset, Inset);
        backing.rectTransform.offsetMax = new Vector2(-Inset, -Inset);

        fill = CreateImage(backing.rectTransform, "Fill", FullHealthColor).GetComponent<Image>();
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.up;
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label = labelObject.GetComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(root, false);
        Stretch(label.rectTransform);
        label.fontSize = 9f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null && label.font == null)
            label.font = TMP_Settings.defaultFontAsset;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Image CreateImage(RectTransform parent, string name, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform target = imageObject.GetComponent<RectTransform>();
        target.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private void Update()
    {
        // Self-cleanup so a destroyed or recycled owner never leaves an orphaned bar behind.
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        EntityStats stats = owner.Stats;
        Camera worldCamera = Camera.main;
        bool visible = owner.ShowEnemyHealthBar &&
                       owner.NpcType == NpcType.Enemy &&
                       stats != null &&
                       stats.IsAlive &&
                       worldCamera != null &&
                       visual != null;

        if (!visible)
        {
            visual.gameObject.SetActive(false);
            return;
        }

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(
            owner.transform.position + Vector3.up * owner.HealthBarWorldOffset);
        if (screenPoint.z <= 0f)
        {
            visual.gameObject.SetActive(false);
            return;
        }

        if (!visual.gameObject.activeSelf)
            visual.gameObject.SetActive(true);

        RectTransform root = (RectTransform)transform;
        root.sizeDelta = new Vector2(owner.HealthBarScreenWidth, owner.HealthBarScreenHeight);
        root.anchoredPosition = new Vector2(
            screenPoint.x - Screen.width * 0.5f,
            screenPoint.y - Screen.height * 0.5f);

        float innerWidth = Math.Max(1f, owner.HealthBarScreenWidth - Inset * 2f);
        float ratio = stats.MaxHp > 0 ? Mathf.Clamp01((float)stats.Hp / stats.MaxHp) : 0f;
        if (!Mathf.Approximately(ratio, lastFillRatio))
        {
            lastFillRatio = ratio;
            fill.color = Color.Lerp(FullHealthColor, LowHealthColor, ratio);
            fill.rectTransform.sizeDelta = new Vector2(innerWidth * ratio, 0f);
        }

        string nextLabel = $"{stats.Hp} / {stats.MaxHp}";
        if (!string.Equals(nextLabel, labelText, StringComparison.Ordinal))
        {
            labelText = nextLabel;
            label.text = nextLabel;
        }
    }

    private static void EnsureCanvas()
    {
        if (sharedCanvas != null)
            return;

        var canvasObject = new GameObject(
            "Enemy Health Bars", typeof(Canvas), typeof(GraphicRaycaster));
        sharedCanvas = canvasObject.GetComponent<Canvas>();
        sharedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        sharedCanvas.sortingOrder = Mathf.RoundToInt(CanvasSortingOrder);
    }
}
