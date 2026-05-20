using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DialogueUIController : MonoBehaviour
{
    private static DialogueUIController instance;

    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private TextMeshProUGUI speakerNameText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private TextMeshProUGUI choicesText;

    public bool IsShowingDialogue => panelRoot != null && panelRoot.gameObject.activeSelf;

    public static DialogueUIController GetOrCreate()
    {
        if (instance != null)
        {
            instance.EnsureRuntimeReferences();
            return instance;
        }

        instance = FindFirstObjectByType<DialogueUIController>();
        if (instance != null)
        {
            instance.EnsureRuntimeReferences();
            return instance;
        }

        GameObject uiObject = new GameObject("Dialogue UI");
        instance = uiObject.AddComponent<DialogueUIController>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureRuntimeReferences();
        HideDialogue();
    }

    public void ShowDialogue(string speakerName, string line, List<string> choices, int selectedChoiceIndex)
    {
        EnsureRuntimeReferences();

        speakerNameText.text = speakerName;
        dialogueText.text = line;
        choicesText.text = FormatChoices(choices, selectedChoiceIndex);
        choicesText.gameObject.SetActive(!string.IsNullOrEmpty(choicesText.text));
        panelRoot.gameObject.SetActive(true);
    }

    public void HideDialogue()
    {
        EnsureRuntimeReferences();
        panelRoot.gameObject.SetActive(false);
    }

    private void EnsureRuntimeReferences()
    {
        if (rootCanvas != null && panelRoot != null && speakerNameText != null && dialogueText != null && choicesText != null)
        {
            return;
        }

        BuildRuntimeUi();
    }

    private void BuildRuntimeUi()
    {
        if (rootCanvas != null)
        {
            Destroy(rootCanvas.gameObject);
        }

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }

        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        rootCanvas = canvasObject.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 1000;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelObject = new GameObject("Dialogue Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);

        panelRoot = panelObject.GetComponent<RectTransform>();
        panelRoot.anchorMin = new Vector2(0.08f, 0.05f);
        panelRoot.anchorMax = new Vector2(0.92f, 0.28f);
        panelRoot.offsetMin = Vector2.zero;
        panelRoot.offsetMax = Vector2.zero;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.11f, 0.14f, 0.92f);

        speakerNameText = CreateAnchoredText(
            panelRoot,
            "Speaker Name",
            32f,
            FontStyles.Bold,
            new Vector2(0f, 0.72f),
            new Vector2(1f, 1f),
            new Vector2(24f, -8f),
            new Vector2(-24f, -8f),
            TextAlignmentOptions.Left);

        dialogueText = CreateAnchoredText(
            panelRoot,
            "Dialogue Text",
            28f,
            FontStyles.Normal,
            new Vector2(0f, 0.28f),
            new Vector2(1f, 0.74f),
            new Vector2(24f, 0f),
            new Vector2(-24f, -8f),
            TextAlignmentOptions.TopLeft);

        choicesText = CreateAnchoredText(
            panelRoot,
            "Choices Text",
            24f,
            FontStyles.Normal,
            new Vector2(0f, 0f),
            new Vector2(1f, 0.3f),
            new Vector2(24f, 8f),
            new Vector2(-24f, -8f),
            TextAlignmentOptions.TopLeft);
    }

    private static string FormatChoices(List<string> choices, int selectedChoiceIndex)
    {
        if (choices == null || choices.Count == 0)
        {
            return string.Empty;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < choices.Count; i++)
        {
            string prefix = i == selectedChoiceIndex ? "> " : "  ";
            builder.Append(prefix);
            builder.Append(choices[i]);

            if (i < choices.Count - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private static TextMeshProUGUI CreateAnchoredText(
        RectTransform parent,
        string objectName,
        float fontSize,
        FontStyles fontStyle,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
        {
            text.font = TMP_Settings.defaultFontAsset;
        }

        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = true;
        return text;
    }
}