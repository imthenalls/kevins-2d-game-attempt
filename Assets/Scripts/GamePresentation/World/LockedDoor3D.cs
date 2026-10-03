using System;
using System.Collections;
using Game.Core;
using UnityEngine;

/// <summary>
/// The 3D planar-isometric counterpart of <see cref="SlidingDoor"/>: an E-interactable door that
/// physically blocks a doorway with a solid collider on the Walls layer and slides two visible
/// panels apart to open. It requires a key resolved from the interacting entity's <see cref="IKeyHolder"/>
/// (falling back to <see cref="PlayerKeyring"/> for the player).
///
/// The lock decision and door state live in Game.Data (<see cref="DoorLockPolicy"/> and
/// <see cref="LockedDoorModel"/>); this component is a thin facade that builds the panels/blocker,
/// animates them, and forwards input. It uses [ExecuteAlways] so the panels and blocker are visible
/// and positionable in the Scene view without entering Play Mode.
///
/// Unity setup:
///   1. Place the root on the centre of the doorway, with its local +X along the opening.
///   2. Add this component; it generates a trigger BoxCollider (interaction) on the root, an
///      invisible "DoorBlocker" child on the Walls layer, and "PanelLeft"/"PanelRight" visuals.
///   3. Put the root on a layer included in PlayerInteractionController.Interactable Layers
///      (in the Town scene that is the Npc layer).
///   4. Configure Settings (Door3DConfig): Display Name, Required Key Id, Interaction Range,
///      Slide Duration, and lock colours.
///
/// Runtime API:
///   TryOpen(interactor) / TryUse(interactor) perform the key check.
///   IsOpen, IsLocked, IsMoving expose state. Open()/Close() drive the animation directly.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class LockedDoor3D : MonoBehaviour, IInteractable
{
    [Header("Config (Game.Data)")]
    [SerializeField] private Door3DConfig config = new Door3DConfig();

    [Header("Geometry (Unity)")]
    [Tooltip("World-space width of the doorway the two panels cover.")]
    [SerializeField] private float openingWidth = 2f;
    [Tooltip("Height of each door panel (kept under the school cover height).")]
    [SerializeField] private float panelHeight = 0.92f;
    [Tooltip("Depth (Z thickness) of each panel and the blocker.")]
    [SerializeField] private float panelThickness = 0.28f;
    [Tooltip("Sprite drawn on each sliding panel.")]
    [SerializeField] private Sprite panelSprite;
    [Tooltip("Layer the solid blocker collider is placed on so pathfinding and reachability see it.")]
    [SerializeField] private string blockerLayerName = "Walls";

    private Transform panelLeft;
    private Transform panelRight;
    private Transform blocker;
    private BoxCollider blockerCollider;
    private BoxCollider interactionCollider;
    private SpriteRenderer leftRenderer;
    private SpriteRenderer rightRenderer;
    private LockedDoorModel model;
    private Coroutine slideCoroutine;
    private bool built;
    private bool lockedMessagePending;
    private float builtWidth;
    private float builtHeight;
    private float builtThickness;
    private Sprite builtSprite;

    public event Action<GameObject, GateUseResult> OnUseResolved;
    public event Action OnUnlockFailed;
    public event Action OnOpened;
    public event Action OnClosed;

    public bool IsOpen => model != null && model.IsOpen;
    public bool IsLocked => model != null && model.IsLocked;
    public bool IsMoving => slideCoroutine != null;
    public string RequiredKeyId => config.RequiredKeyId;
    public Vector3 InteractionPosition => transform.position;

    private Color LockedColor => new Color(config.LockedR, config.LockedG, config.LockedB, config.LockedA);
    private Color UnlockedColor => new Color(config.UnlockedR, config.UnlockedG, config.UnlockedB, config.UnlockedA);

    private void Awake()
    {
        EnsureModel();
        EnsureBuilt();
    }

    private void OnEnable()
    {
        EnsureModel();
        EnsureBuilt();
    }

    private void OnDisable()
    {
        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
            slideCoroutine = null;
        }
    }

    private void OnValidate()
    {
        openingWidth = Mathf.Max(0.5f, openingWidth);
        panelHeight = Mathf.Max(0.2f, panelHeight);
        panelThickness = Mathf.Max(0.05f, panelThickness);
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;
        built = false;
        UnityEditor.EditorApplication.delayCall -= EditorRebuild;
        UnityEditor.EditorApplication.delayCall += EditorRebuild;
#endif
    }

#if UNITY_EDITOR
    private void EditorRebuild()
    {
        UnityEditor.EditorApplication.delayCall -= EditorRebuild;
        if (this == null || Application.isPlaying)
            return;
        EnsureModel();
        EnsureBuilt();
    }
#endif

    private void EnsureModel()
    {
        if (model == null)
            model = new LockedDoorModel(config);
    }

    // Builds the interaction trigger, invisible blocker, and two panels when missing or resized.
    private void EnsureBuilt()
    {
        if (this == null)
            return;

        Sprite sprite = panelSprite != null ? panelSprite : LoadDefaultSprite();
        if (built && Mathf.Approximately(builtWidth, openingWidth) &&
            Mathf.Approximately(builtHeight, panelHeight) &&
            Mathf.Approximately(builtThickness, panelThickness) && builtSprite == sprite)
        {
            return;
        }

        interactionCollider = GetComponent<BoxCollider>();
        if (interactionCollider == null)
            interactionCollider = gameObject.AddComponent<BoxCollider>();
        interactionCollider.isTrigger = true;
        interactionCollider.size = new Vector3(openingWidth + 0.4f, 1.7f, 1.4f);
        interactionCollider.center = new Vector3(0f, 0.85f, 0f);

        blocker = EnsureChild("DoorBlocker");
        blockerCollider = blocker.GetComponent<BoxCollider>();
        if (blockerCollider == null)
            blockerCollider = blocker.gameObject.AddComponent<BoxCollider>();
        blockerCollider.isTrigger = false;
        blockerCollider.size = new Vector3(openingWidth, panelHeight, panelThickness);
        blockerCollider.center = new Vector3(0f, panelHeight * 0.5f, 0f);
        int wallsLayer = LayerMask.NameToLayer(blockerLayerName);
        blocker.gameObject.layer = wallsLayer >= 0 ? wallsLayer : gameObject.layer;

        panelLeft = EnsurePanel("PanelLeft", sprite, out leftRenderer);
        panelRight = EnsurePanel("PanelRight", sprite, out rightRenderer);

        ApplyPoseImmediate(model != null && model.IsOpen);
        ApplyLockVisuals();

        builtWidth = openingWidth;
        builtHeight = panelHeight;
        builtThickness = panelThickness;
        builtSprite = sprite;
        built = true;
    }

    private Transform EnsurePanel(string name, Sprite sprite, out SpriteRenderer renderer)
    {
        Transform panel = EnsureChild(name);
        renderer = panel.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = panel.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 58;
        panel.localRotation = Quaternion.identity;
        return panel;
    }

    private Transform EnsureChild(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            child = new GameObject(childName).transform;
            child.SetParent(transform, false);
        }

        return child;
    }

    private static Sprite LoadDefaultSprite()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
            "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/Square.png");
#else
        return null;
#endif
    }

    // ── Pose / animation ─────────────────────────────────────────────────────

    private void ApplyPoseImmediate(bool open)
    {
        float half = openingWidth * 0.5f;
        float retract = open ? half : 0f;
        if (panelLeft != null)
            panelLeft.localPosition = new Vector3(-openingWidth * 0.25f - retract, panelHeight * 0.5f, 0f);
        if (panelRight != null)
            panelRight.localPosition = new Vector3(openingWidth * 0.25f + retract, panelHeight * 0.5f, 0f);
        ApplyPanelScale();
        SetBlockerEnabled(!open);
    }

    private void ApplyPanelScale()
    {
        var scale = new Vector3(openingWidth * 0.5f, panelHeight, panelThickness);
        if (panelLeft != null)
            panelLeft.localScale = scale;
        if (panelRight != null)
            panelRight.localScale = scale;
    }

    private void SetBlockerEnabled(bool value)
    {
        if (blockerCollider != null)
            blockerCollider.enabled = value;
        if (blocker != null)
            blocker.gameObject.SetActive(value);
    }

    /// <summary>Retracts both panels to the open position if the door is closed.</summary>
    public void Open()
    {
        if (model == null || model.IsOpen || IsMoving)
            return;

        StartSlide(open: true);
    }

    /// <summary>Closes both panels when closing is enabled.</summary>
    public void Close()
    {
        if (model == null || !model.IsOpen || !config.CanClose || IsMoving)
            return;

        StartSlide(open: false);
    }

    private void StartSlide(bool open)
    {
        if (model != null)
            model.ApplyOpen(open);
        SetBlockerEnabled(!open);
        if (slideCoroutine != null)
            StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideRoutine(open));
    }

    private IEnumerator SlideRoutine(bool open)
    {
        float half = openingWidth * 0.5f;
        float retract = open ? half : 0f;
        Vector3 leftTarget = new Vector3(-openingWidth * 0.25f - retract, panelHeight * 0.5f, 0f);
        Vector3 rightTarget = new Vector3(openingWidth * 0.25f + retract, panelHeight * 0.5f, 0f);
        Vector3 leftStart = panelLeft != null ? panelLeft.localPosition : leftTarget;
        Vector3 rightStart = panelRight != null ? panelRight.localPosition : rightTarget;

        float duration = Mathf.Max(0.05f, config.SlideDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            if (panelLeft != null)
                panelLeft.localPosition = Vector3.LerpUnclamped(leftStart, leftTarget, eased);
            if (panelRight != null)
                panelRight.localPosition = Vector3.LerpUnclamped(rightStart, rightTarget, eased);
            yield return null;
        }

        if (panelLeft != null)
            panelLeft.localPosition = leftTarget;
        if (panelRight != null)
            panelRight.localPosition = rightTarget;

        slideCoroutine = null;
        ApplyLockVisuals();

        if (open)
            OnOpened?.Invoke();
        else
            OnClosed?.Invoke();
    }

    // ── Interaction ──────────────────────────────────────────────────────────

    public bool CanInteract(Vector3 worldPosition)
    {
        return enabled && !IsMoving &&
               Vector3.Distance(InteractionPosition, worldPosition) <= config.InteractionRange;
    }

    public string GetDisplayName() => config.DisplayName;

    public bool TryGetCurrentLine(out string line)
    {
        line = string.Empty;

        if (lockedMessagePending)
        {
            lockedMessagePending = false;
            return false;
        }

        if (!IsLocked || PlayerHasRequiredKey())
            return false;

        line = BuildLockedMessage();
        lockedMessagePending = true;
        return true;
    }

    public void Advance() { }

    public void EndInteraction(GameObject interactor)
    {
        lockedMessagePending = false;
        if (IsOpen)
            Close();
        else
            TryOpen(interactor);
    }

    public bool TryOpen(GameObject interactor) => TryUse(interactor) == GateUseResult.Opened;

    /// <summary>Attempts to use the door and reports the outcome; the key check is Core-owned.</summary>
    public GateUseResult TryUse(GameObject interactor)
    {
        EnsureModel();
        EnsureBuilt();

        if (model.IsOpen)
        {
            ResolveUse(interactor, GateUseResult.Opened);
            return GateUseResult.Opened;
        }

        if (IsMoving)
        {
            ResolveUse(interactor, GateUseResult.Busy);
            return GateUseResult.Busy;
        }

        IKeyHolder holder = ResolveKeyHolder(interactor);
        bool holderHasKey = holder != null && !string.IsNullOrWhiteSpace(config.RequiredKeyId) &&
                            holder.HasKey(config.RequiredKeyId);

        GateUseResult result = model.ResolveUse(holderHasKey);
        if (result == GateUseResult.Locked)
        {
            Debug.Log($"[LockedDoor3D] {config.DisplayName} is locked. Required key: '{config.RequiredKeyId}'.", this);
            OnUnlockFailed?.Invoke();
            ResolveUse(interactor, GateUseResult.Locked);
            return GateUseResult.Locked;
        }

        if (config.ConsumeKeyOnUnlock &&
            (holder == null || !holder.RemoveKey(config.RequiredKeyId, 1)))
        {
            OnUnlockFailed?.Invoke();
            ResolveUse(interactor, GateUseResult.Locked);
            return GateUseResult.Locked;
        }

        if (config.ConsumeKeyOnUnlock)
            QuestEventBus.Raise("ItemUsed", config.RequiredKeyId, 1);

        StartSlide(open: true);
        ResolveUse(interactor, GateUseResult.Opened);
        return GateUseResult.Opened;
    }

    private void ResolveUse(GameObject interactor, GateUseResult result) =>
        OnUseResolved?.Invoke(interactor, result);

    private bool PlayerHasRequiredKey()
    {
        if (string.IsNullOrWhiteSpace(config.RequiredKeyId))
            return true;

        PlayerKeyring keyring = PlayerKeyring.GetOrCreate();
        return keyring != null && keyring.HasKey(config.RequiredKeyId);
    }

    private string BuildLockedMessage()
    {
        string keyName = config.RequiredKeyId;
        ItemDatabase database = ItemDatabase.Instance;
        if (database != null && database.TryGet(config.RequiredKeyId, out ItemData key) &&
            key != null && !string.IsNullOrWhiteSpace(key.itemName))
        {
            keyName = key.itemName;
        }

        return string.IsNullOrWhiteSpace(config.LockedMessage)
            ? $"It's locked. You need the {keyName}."
            : string.Format(config.LockedMessage, keyName);
    }

    // Resolves the interacting entity's key holder, falling back to the player keyring for the player.
    private static IKeyHolder ResolveKeyHolder(GameObject interactor)
    {
        if (interactor == null)
            return PlayerKeyring.GetOrCreate();

        IKeyHolder holder = interactor.GetComponentInParent<IKeyHolder>();
        if (holder != null)
            return holder;

        if (interactor.CompareTag("Player") ||
            interactor.GetComponentInParent<PlayerControllerBase>() != null)
        {
            return PlayerKeyring.GetOrCreate();
        }

        return null;
    }

    private void ApplyLockVisuals()
    {
        Color color = IsLocked ? LockedColor : UnlockedColor;
        if (leftRenderer != null)
            leftRenderer.color = color;
        if (rightRenderer != null)
            rightRenderer.color = color;
    }

    [ContextMenu("Rebuild Door")]
    private void RebuildDoorContext()
    {
        built = false;
        EnsureModel();
        EnsureBuilt();
    }

    /// <summary>Rebuilds the generated panels/blocker. Called by editor scene builders.</summary>
    public void RebuildPreview()
    {
        built = false;
        EnsureModel();
        EnsureBuilt();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            transform.position + new Vector3(0f, 0.85f, 0f),
            new Vector3(openingWidth, 1.7f, 1.4f));
    }
}
