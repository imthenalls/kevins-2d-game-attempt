using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Singleton scene loader with a smooth fade-in / fade-out transition overlay.
/// Automatically creates a fullscreen Canvas + CanvasGroup overlay at runtime;
/// no prefab or UI setup is needed in the scene.
///
/// Unity setup:
///   1. Add to a persistent bootstrap GameObject in your first scene (DontDestroyOnLoad).
///   2. Adjust Fade Duration and Fade Color in the Inspector if needed.
///   3. Call SceneLoader.Instance.LoadScene("SceneName") from any script to transition.
///   4. Subscribe to OnLoadStart / OnLoadComplete for any loading-screen logic.
///
/// Usage:
///   SceneLoader.Instance.LoadScene("MyScene");
///   SceneLoader.Instance.ReloadCurrentScene();
///
/// Hook events:
///   SceneLoader.Instance.OnLoadStart    += () => { ... };
///   SceneLoader.Instance.OnLoadComplete += () => { ... };
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [Header("Fade Transition")]
    [SerializeField] private float fadeDuration = 0.4f;
    [SerializeField] private Color fadeColor = Color.black;

    /// <summary>Fired once the fade-out finishes and the new scene begins loading.</summary>
    public event Action OnLoadStart;

    /// <summary>Fired once the new scene is active and the fade-in completes.</summary>
    public event Action OnLoadComplete;

    /// <summary>True while a scene load is in progress.</summary>
    public bool IsLoading { get; private set; }

    private CanvasGroup fadeGroup;

    // ── Unity Lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildFadeOverlay();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Load a scene by name with a fade transition.</summary>
    public void LoadScene(string sceneName)
    {
        if (!IsLoading)
            StartCoroutine(LoadRoutine(sceneName));
    }

    /// <summary>Reload the currently active scene with a fade transition.</summary>
    public void ReloadCurrentScene() =>
        LoadScene(SceneManager.GetActiveScene().name);

    // ── Internal ─────────────────────────────────────────────────────────────

    private IEnumerator LoadRoutine(string sceneName)
    {
        IsLoading = true;

        // Fade to black and block input
        yield return StartCoroutine(Fade(1f));
        fadeGroup.blocksRaycasts = true;

        OnLoadStart?.Invoke();

        // Load async — hold activation until fully loaded
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
            yield return null;

        op.allowSceneActivation = true;
        yield return op;

        // Fade back in and restore input
        yield return StartCoroutine(Fade(0f));
        fadeGroup.blocksRaycasts = false;

        IsLoading = false;
        OnLoadComplete?.Invoke();
    }

    private IEnumerator Fade(float target)
    {
        float start = fadeGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
            yield return null;
        }

        fadeGroup.alpha = target;
    }

    /// <summary>
    /// Builds a full-screen fade overlay at runtime.
    /// Canvas sortingOrder 999 keeps it on top of all game UI.
    /// </summary>
    private void BuildFadeOverlay()
    {
        var canvasGo = new GameObject("SceneLoader_FadeCanvas");
        canvasGo.transform.SetParent(transform);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasGo.AddComponent<CanvasScaler>();

        fadeGroup = canvasGo.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.blocksRaycasts = false;
        fadeGroup.interactable = false;

        var imgGo = new GameObject("FadeImage");
        imgGo.transform.SetParent(canvasGo.transform, false);

        var rt = imgGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = imgGo.AddComponent<Image>();
        img.color = fadeColor;
        img.raycastTarget = true;
    }
}
