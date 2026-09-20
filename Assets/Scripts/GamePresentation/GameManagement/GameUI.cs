using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// Persistent UI canvas + EventSystem created by <see cref="GameBootstrap"/> so UI always has
/// somewhere to live.
///
/// Overridable per scene: on scene load these are enabled only when the loaded scene does not
/// already provide its own Canvas / EventSystem. A scene with its own UI therefore wins, and a
/// scene with none still gets working UI.
///
/// Unity setup: none - created by GameBootstrap; do not add to a scene.
/// Runtime API: GameUI.Instance.Canvas / .EventSystem (may be inactive when a scene overrides).
/// </summary>
[DisallowMultipleComponent]
public sealed class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    public Canvas Canvas { get; private set; }
    public EventSystem EventSystem { get; private set; }

    public static GameUI Create(Transform parent)
    {
        var go = new GameObject("Game UI");
        go.transform.SetParent(parent, false);
        GameUI ui = go.AddComponent<GameUI>();
        ui.Build();
        return ui;
    }

    private void Awake() => Instance = this;

    private void Build()
    {
        var canvasGo = new GameObject(
            "Canvas",
            typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        Canvas = canvasGo.GetComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Canvas.sortingOrder = 5000;

        var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

        var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));
        eventSystemGo.transform.SetParent(transform, false);
        EventSystem = eventSystemGo.GetComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemGo.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemGo.AddComponent<StandaloneInputModule>();
#endif

        SetActive(false); // enabled per scene by SyncForScene
    }

    /// <summary>
    /// Enables the persistent canvas/EventSystem only for the parts the loaded scene does not provide
    /// itself, so a per-scene UI overrides the persistent one.
    /// </summary>
    public void SyncForScene(Scene scene)
    {
        bool sceneHasCanvas = false;
        foreach (Canvas candidate in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (candidate.gameObject.scene == scene)
            {
                sceneHasCanvas = true;
                break;
            }
        }

        bool sceneHasEventSystem = false;
        foreach (EventSystem candidate in Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include))
        {
            if (candidate.gameObject.scene == scene)
            {
                sceneHasEventSystem = true;
                break;
            }
        }

        if (Canvas != null)
            Canvas.gameObject.SetActive(!sceneHasCanvas);
        if (EventSystem != null)
            EventSystem.gameObject.SetActive(!sceneHasEventSystem);
    }

    private void SetActive(bool value)
    {
        if (Canvas != null)
            Canvas.gameObject.SetActive(value);
        if (EventSystem != null)
            EventSystem.gameObject.SetActive(value);
    }
}
