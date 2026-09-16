using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds a Keyring button to the inventory and displays all player-owned keys in a readable
/// modal panel. It refreshes automatically when PlayerKeyring changes.
///
/// Unity setup:
///   1. No manual Canvas setup is required; InventoryUI calls GetOrCreate(panelRoot).
///   2. The generated UI is parented to the same Canvas as InventoryUI.
///
/// Runtime API:
///   GetOrCreate(inventoryPanel), SetInventoryVisible(bool), Open(), Close(), and Instance.
/// </summary>
[DisallowMultipleComponent]
public sealed class KeyringUI : MonoBehaviour
{
    private static KeyringUI instance;
    private GameObject launcher;
    private GameObject panel;
    private TextMeshProUGUI listText;
    private PlayerKeyring keyring;

    public static KeyringUI Instance => instance;

    public static KeyringUI GetOrCreate(RectTransform inventoryPanel)
    {
        if (instance == null)
        {
            Canvas canvas = InventoryUI.Instance != null
                ? InventoryUI.Instance.GetComponentInParent<Canvas>()
                : FindAnyObjectByType<Canvas>();
            if (canvas == null)
                return null;

            var root = new GameObject("Keyring UI", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            instance = root.AddComponent<KeyringUI>();
            instance.Build(inventoryPanel);
        }
        return instance;
    }

    private void Awake() => instance = this;

    private void OnDestroy()
    {
        if (keyring != null)
            keyring.OnChanged -= Refresh;
        if (instance == this)
            instance = null;
    }

    public void SetInventoryVisible(bool visible)
    {
        if (launcher != null)
            launcher.SetActive(visible);
        if (!visible)
            Close();
    }

    public void Open()
    {
        Refresh();
        if (panel != null)
            panel.SetActive(true);
    }

    public void Close()
    {
        if (panel != null)
            panel.SetActive(false);
    }

    private void Build(RectTransform inventoryPanel)
    {
        keyring = PlayerKeyring.GetOrCreate();
        keyring.OnChanged += Refresh;

        Transform launcherParent = inventoryPanel != null ? inventoryPanel : transform;
        launcher = CreateButton(launcherParent, "Keyring Button", "Keyring", Open);
        RectTransform launcherRect = launcher.GetComponent<RectTransform>();
        launcherRect.anchorMin = launcherRect.anchorMax = new Vector2(1f, 0f);
        launcherRect.pivot = new Vector2(1f, 0f);
        launcherRect.sizeDelta = new Vector2(150f, 42f);
        launcherRect.anchoredPosition = new Vector2(-16f, 16f);

        panel = new GameObject("Keyring Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
        panel.transform.SetParent(transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(380f, 340f);
        panel.GetComponent<Image>().color = Color.white;
        panel.GetComponent<Outline>().effectColor = Color.black;

        CreateText(panelRect, "Keyring Title", "Keyring", 28f, FontStyles.Bold,
            new Vector2(20f, -16f), new Vector2(340f, 42f));
        GameObject viewportObject = new GameObject(
            "Key List View", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.transform.SetParent(panelRect, false);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        viewport.anchorMin = viewport.anchorMax = new Vector2(0f, 1f);
        viewport.pivot = new Vector2(0f, 1f);
        viewport.anchoredPosition = new Vector2(24f, -74f);
        viewport.sizeDelta = new Vector2(332f, 210f);
        viewportObject.GetComponent<Image>().color = new Color(0.94f, 0.94f, 0.94f, 1f);

        listText = CreateText(viewport, "Key List", "No keys", 19f, FontStyles.Normal,
            new Vector2(8f, -8f), new Vector2(-16f, 0f));
        listText.alignment = TextAlignmentOptions.TopLeft;
        RectTransform listRect = listText.rectTransform;
        listRect.anchorMin = new Vector2(0f, 1f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.pivot = new Vector2(0.5f, 1f);
        ContentSizeFitter fitter = listText.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.content = listRect;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 24f;

        GameObject close = CreateButton(panelRect, "Close Button", "Close", Close);
        RectTransform closeRect = close.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 16f);
        closeRect.sizeDelta = new Vector2(140f, 40f);

        panel.SetActive(false);
        launcher.SetActive(false);
    }

    private void Refresh()
    {
        if (listText == null || keyring == null)
            return;

        var builder = new StringBuilder();
        foreach (var entry in keyring.GetEntries())
        {
            ItemData item = ItemDatabase.Instance?.Get(entry.Key);
            string shownName = item != null ? item.itemName : entry.Key;
            builder.Append("• ").Append(shownName);
            if (entry.Value > 1)
                builder.Append(" ×").Append(entry.Value);
            builder.AppendLine();
        }
        listText.text = builder.Length > 0 ? builder.ToString() : "No keys";
    }

    private static GameObject CreateButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction action)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = new Color(0.94f, 0.94f, 0.94f, 1f);
        buttonObject.GetComponent<Button>().onClick.AddListener(action);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        TextMeshProUGUI text = CreateText(rect, "Text", label, 20f, FontStyles.Normal, Vector2.zero, Vector2.zero);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;
        return buttonObject;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent, string name, string value, float size, FontStyles style,
        Vector2 position, Vector2 dimensions)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.black;
        text.raycastTarget = false;
        return text;
    }
}
