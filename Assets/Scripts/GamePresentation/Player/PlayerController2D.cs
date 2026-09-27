using Game.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Top-down 2D player controller on the XY plane. Reads WASD / left-stick directional input each
/// frame and drives the Rigidbody2D via linearVelocity. Derives from PlayerControllerBase so shared
/// systems (inventory, save, world travel, scene rules) work without knowing the dimension.
///
/// Tuning values live in the pure-C# Game.Core.PlayerMovementConfig (Game.Data assembly); this
/// component only holds the Unity-only references (the visual transform) and reads the config.
///
/// Unity setup:
///   1. Add to the player root GameObject.
///   2. Add a Rigidbody2D:
///        • Gravity Scale = 0  (or enable Force No Gravity in Settings).
///        • Freeze Z Rotation  (or enable Lock Rotation in Settings).
///   3. EntityStats is required (enforced by RequireComponent). Its legacy Starting MP and
///      Max MP initialize the canonical mana account when the player has no Wallet yet.
///   4. Wallet is found or added automatically on Awake.
///   5. Optionally add CombatReceiver / CombatAttacker.
///   6. Assign Settings (PlayerMovementConfig) and Visual Transform in the Inspector.
///
/// Movement is locked at runtime by SetMovementEnabled(false) — called automatically
/// by dialogue, inventory, and cutscene systems.
///
/// Runtime API: MovementEnabled/SetMovementEnabled, MoveSpeed, Stats, ManaWallet,
/// ApplyAvatarProfile, CapturePositionModel, ApplyPositionModel, IsDashing.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EntityStats))]
public class PlayerController2D : PlayerControllerBase
{
    [Header("Movement Settings (Game.Data)")]
    [Tooltip("Authoritative movement/facing/dash tuning. Stored in the pure-C# Game.Data layer.")]
    [SerializeField] private PlayerMovementConfig settings = new PlayerMovementConfig();

    [Header("Unity References")]
    [Tooltip("Visual child to rotate without rotating the Rigidbody2D or collider.")]
    [SerializeField] private Transform visualTransform;

    [Tooltip("Grid for the logical player position. Defaults to a parent Grid, then the nearest in scene.")]
    [SerializeField] private Grid grid;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool movementEnabled = true;
    private Vector2 lastMovementDirection = Vector2.up;
    private Vector2 dashDirection;
    private float playerLength;
    private PlayerDashModel dash;
    private TrailRenderer dashTrail;

    private EntityStats stats;
    private Wallet manaWallet;
    private CombatReceiver combatReceiver;

    private PositionModel positionModel;
    private const float RepositionEpsilon = 0.001f;

    public override EntityStats Stats => stats;
    public override Wallet ManaWallet => manaWallet;
    public override CombatReceiver CombatReceiver => combatReceiver;
    public override bool MovementEnabled => movementEnabled;
    public bool IsDashing => dash != null && dash.IsDashing;
    public int CurrentDashCharges => dash != null ? dash.Charges : 0;
    public int MaxDashCharges => settings.MaxDashCharges;

    // Pure form of the trail color; converted to a UnityEngine.Color only where needed.
    private Color TrailColor =>
        new Color(settings.DashTrailR, settings.DashTrailG, settings.DashTrailB, settings.DashTrailA);

    /// <summary>Applies movement and dash tuning from the active world's avatar profile.</summary>
    public override void ApplyAvatarProfile(PlayerAvatarProfile profile)
    {
        if (profile == null) return;

        settings.MoveSpeed = Mathf.Max(settings.MinMoveSpeed, profile.MoveSpeed);
        settings.DashEnabled = profile.DashEnabled;
        settings.DashDistanceInPlayerLengths = Mathf.Max(settings.MinDashDistanceInPlayerLengths, profile.DashDistanceInPlayerLengths);
        settings.DashSpeedMultiplier = Mathf.Max(settings.MinDashSpeedMultiplier, profile.DashSpeedMultiplier);
        settings.DashCooldown = Mathf.Max(settings.MinDashCooldown, profile.DashCooldown);
        settings.MaxDashCharges = Mathf.Max(settings.MinDashCharges, profile.MaxDashCharges);
        settings.DashRechargeSeconds = Mathf.Max(settings.MinDashRechargeSeconds, profile.DashRechargeSeconds);
        dash?.ResetCharges();
    }

    /// <summary>Movement speed in units/s. Reads/writes Settings.MoveSpeed.</summary>
    public override float MoveSpeed
    {
        get => settings.MoveSpeed;
        set => settings.MoveSpeed = value;
    }

    private void Awake()
    {
        rb       = GetComponent<Rigidbody2D>();
        stats    = GetComponent<EntityStats>();
        combatReceiver = GetComponent<CombatReceiver>(); // may be null if CombatReceiver is not added

        bool hadWallet = TryGetComponent(out Wallet wallet);
        manaWallet = hadWallet ? wallet : gameObject.AddComponent<Wallet>();
        stats.BindManaWallet(manaWallet, initializeFromStats: !hadWallet);

        // Player HP lives in the GameSession model so it is shared across avatars/scenes and out of
        // the MonoBehaviour (Engine-Free Core). The first player binds and seeds it; later avatars
        // adopt the existing model via EntityStats.BindHealthModel.
        GameSessionHost.EnsureExists();
        GameSession session = GameSessionHost.Session;
        if (session != null)
            stats.BindHealthModel(session.GetOrCreatePlayerHealth(stats.MaxHp, stats.Hp));

        // Bind the logical position model (cell + local offset). See LateUpdate/HandlePositionChanged.
        grid ??= GetComponentInParent<Grid>();
        if (grid == null)
            grid = FindAnyObjectByType<Grid>();

        if (session != null && grid != null)
        {
            Vector3Int cell = grid.WorldToCell(transform.position);
            Vector3 center = grid.GetCellCenterWorld(cell);
            positionModel = session.GetOrCreatePlayerPosition(
                cell.x, cell.y, transform.position.x - center.x, transform.position.y - center.y);
            positionModel.Changed += HandlePositionChanged;
            // Adopt any stored position (returning avatar / loaded save) before physics runs.
            HandlePositionChanged(positionModel);
        }

        if (settings.ForceNoGravity)
        {
            rb.gravityScale = 0f;
        }

        if (settings.LockRotation)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        Collider2D playerCollider = GetComponent<Collider2D>();
        playerLength = settings.DefaultPlayerLength;
        if (playerCollider != null)
        {
            Vector2 colliderSize = playerCollider.bounds.size;
            playerLength = Mathf.Max(settings.MinPlayerLength, colliderSize.x, colliderSize.y);
        }

        dash = new PlayerDashModel(settings);
        EnsureDashTrail();
    }

    // Stops and clears the runtime trail if the player controller becomes disabled.
    private void OnDisable()
    {
        StopAndClearDashTrail();
    }

    // Mirrors the physics transform into the session-owned logical position (Engine-Free Core).
    // The model is authoritative for save/load; this keeps it in sync as physics moves the body.
    private void LateUpdate()
    {
        CapturePositionModel();
    }

    /// <summary>Mirrors the physics transform into the authoritative session position model.</summary>
    public override void CapturePositionModel()
    {
        if (positionModel == null || grid == null)
            return;

        Vector3Int cell = grid.WorldToCell(transform.position);
        Vector3 center = grid.GetCellCenterWorld(cell);
        positionModel.Set(
            cell.x,
            cell.y,
            transform.position.x - center.x,
            transform.position.y - center.y);
    }

    /// <summary>Applies the authoritative session position model back onto the physics body.</summary>
    public override void ApplyPositionModel()
    {
        HandlePositionChanged(positionModel);
    }

    // Applies an externally changed model position (load, teleport, avatar switch) to the body.
    private void HandlePositionChanged(PositionModel model)
    {
        if (model == null || grid == null)
            return;

        Vector3 target = grid.GetCellCenterWorld(new Vector3Int(model.CellX, model.CellY, 0))
                         + new Vector3(model.OffsetX, model.OffsetY, 0f);
        target.z = transform.position.z;

        if ((target - transform.position).sqrMagnitude <= RepositionEpsilon * RepositionEpsilon)
            return;

        if (rb != null)
            rb.position = target;
        transform.position = target;
    }

    private void OnDestroy()
    {
        if (positionModel != null)
            positionModel.Changed -= HandlePositionChanged;
    }

    private void Update()
    {
        dash.TickTimers(Time.deltaTime);

        if (!movementEnabled)
        {
            moveInput = Vector2.zero;
            return;
        }

#if ENABLE_INPUT_SYSTEM
        Vector2 input = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            {
                input.y += 1f;
            }
        }

        if (Gamepad.current != null)
        {
            input += Gamepad.current.leftStick.ReadValue();
        }

        moveInput = Vector2.ClampMagnitude(input, 1f);
#else
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(horizontal, vertical).normalized;
#endif

        UpdateMovementFacing();

        if (moveInput.sqrMagnitude > settings.FacingInputDeadZone * settings.FacingInputDeadZone)
            lastMovementDirection = moveInput.normalized;

        if (dash.CanStart && WasDashPressedThisFrame())
            BeginDash();
    }

    private void FixedUpdate()
    {
        if (movementEnabled && dash.IsDashing)
        {
            float dashSpeed = Mathf.Max(settings.MinDashSpeed, settings.MoveSpeed * settings.DashSpeedMultiplier);
            float stepFraction = dash.DashStepFraction(Time.fixedDeltaTime);
            rb.linearVelocity = dashDirection * dashSpeed * stepFraction;
            if (dash.TickDash(Time.fixedDeltaTime) == PlayerDashCommand.StopDash)
                EndDashTrailEmission();
            return;
        }

        rb.linearVelocity = movementEnabled ? moveInput * settings.MoveSpeed : Vector2.zero;
    }

    // Starts a fixed-distance dash in the direction the player visual currently faces.
    private void BeginDash()
    {
        dashDirection = GetFacingDirection();
        float dashSpeed = Mathf.Max(settings.MinDashSpeed, settings.MoveSpeed * settings.DashSpeedMultiplier);
        float dashDistance = playerLength * settings.DashDistanceInPlayerLengths;
        if (dash.RequestDash(dashDistance / dashSpeed) == PlayerDashCommand.StartDash)
            BeginDashTrail();
    }

    // Creates the tapered runtime TrailRenderer used only by the dash.
    private void EnsureDashTrail()
    {
        if (dashTrail != null)
            return;

        var trailObject = new GameObject("Player Dash Trail");
        trailObject.transform.SetParent(transform, false);
        dashTrail = trailObject.AddComponent<TrailRenderer>();
        dashTrail.time = settings.DashTrailFadeTime;
        dashTrail.minVertexDistance = settings.DashTrailMinVertexDistance;
        dashTrail.emitting = false;
        dashTrail.autodestruct = false;
        dashTrail.alignment = LineAlignment.View;
        dashTrail.textureMode = LineTextureMode.Stretch;
        dashTrail.numCornerVertices = settings.DashTrailCornerVertices;
        dashTrail.numCapVertices = settings.DashTrailCapVertices;
        dashTrail.widthMultiplier = playerLength * settings.DashTrailWidthInPlayerLengths;
        dashTrail.widthCurve = new AnimationCurve(
            new Keyframe(settings.DashTrailWidthStartTime, settings.DashTrailWidthStart),
            new Keyframe(settings.DashTrailWidthMidTime, settings.DashTrailWidthMid),
            new Keyframe(settings.DashTrailWidthEndTime, settings.DashTrailWidthEnd));

        Color trailColor = TrailColor;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(trailColor, 0f),
                new GradientColorKey(trailColor * settings.DashTrailEndColorMultiplier, 1f)
            },
            new[]
            {
                new GradientAlphaKey(trailColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        dashTrail.colorGradient = gradient;

        Shader trailShader = Shader.Find("Sprites/Default");
        if (trailShader != null)
            dashTrail.material = new Material(trailShader) { name = "Runtime Player Dash Trail" };

        SpriteRenderer playerRenderer = visualTransform != null
            ? visualTransform.GetComponent<SpriteRenderer>()
            : GetComponentInChildren<SpriteRenderer>();
        if (playerRenderer != null)
        {
            dashTrail.sortingLayerID = playerRenderer.sortingLayerID;
            dashTrail.sortingOrder = playerRenderer.sortingOrder - 1;
        }
    }

    // Clears the previous trail and starts emitting from the player's current position.
    private void BeginDashTrail()
    {
        EnsureDashTrail();
        if (dashTrail == null)
            return;

        dashTrail.time = settings.DashTrailFadeTime;
        dashTrail.widthMultiplier = playerLength * settings.DashTrailWidthInPlayerLengths;
        dashTrail.Clear();
        dashTrail.emitting = true;
    }

    // Stops adding points while allowing the completed dash trail to fade naturally.
    private void EndDashTrailEmission()
    {
        if (dashTrail != null)
            dashTrail.emitting = false;
    }

    // Removes all trail points immediately when a dash is cancelled or disabled.
    private void StopAndClearDashTrail()
    {
        if (dashTrail == null)
            return;

        dashTrail.emitting = false;
        dashTrail.Clear();
    }

    // Converts the visual's configured forward axis into a world-space dash direction.
    private Vector2 GetFacingDirection()
    {
        if (visualTransform != null)
        {
            float radians = settings.SpriteForwardAngle * Mathf.Deg2Rad;
            Vector3 localForward = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
            Vector2 worldForward = visualTransform.TransformDirection(localForward);
            if (worldForward.sqrMagnitude > 0.0001f)
                return worldForward.normalized;
        }

        return lastMovementDirection.sqrMagnitude > 0.0001f
            ? lastMovementDirection.normalized
            : Vector2.up;
    }

    // Reads a single Left Shift press for the dash; holding the key does not retrigger it.
    private bool WasDashPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown((KeyCode)settings.LegacyDashKeyCode);
#endif
    }

    private void UpdateMovementFacing()
    {
        if (!settings.FaceMovementDirection ||
            visualTransform == null ||
            moveInput.sqrMagnitude <= settings.FacingInputDeadZone * settings.FacingInputDeadZone)
        {
            return;
        }

        float movementAngle = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg;
        float targetAngle = movementAngle - settings.SpriteForwardAngle;
        float currentAngle = visualTransform.localEulerAngles.z;
        float nextAngle = settings.FacingTurnSpeed <= 0f
            ? targetAngle
            : Mathf.MoveTowardsAngle(
                currentAngle,
                targetAngle,
                settings.FacingTurnSpeed * Time.deltaTime);

        visualTransform.localRotation = Quaternion.Euler(0f, 0f, nextAngle);
    }

    public override void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!movementEnabled)
        {
            moveInput = Vector2.zero;
            dash.CancelDash();
            StopAndClearDashTrail();
            rb.linearVelocity = Vector2.zero;
        }
    }
}
