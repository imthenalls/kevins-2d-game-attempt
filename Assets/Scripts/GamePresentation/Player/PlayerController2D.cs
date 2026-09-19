using Game.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Top-down 2D player controller. Reads WASD / left-stick directional input each frame
/// and drives the Rigidbody2D via linearVelocity. Implements IEntityController so any
/// system can accept either a player or NPC reference through the shared interface.
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
/// Runtime API:
///   SetMovementEnabled controls movement.
///   MoveSpeed reads/writes Settings.MoveSpeed.
///   Stats and ManaWallet expose player state.
///   ITradeParticipant uses the stable id "player", ManaWallet, and InventoryUI.Model.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EntityStats))]
public class PlayerController2D : MonoBehaviour, IEntityController, ITradeParticipant
{
    [Header("Movement Settings (Game.Data)")]
    [Tooltip("Authoritative movement/facing/dash tuning. Stored in the pure-C# Game.Data layer.")]
    [SerializeField] private PlayerMovementConfig settings = new PlayerMovementConfig();

    [Header("Unity References")]
    [Tooltip("Visual child to rotate without rotating the Rigidbody2D or collider.")]
    [SerializeField] private Transform visualTransform;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool movementEnabled = true;
    private Vector2 lastMovementDirection = Vector2.up;
    private Vector2 dashDirection;
    private float playerLength;
    private float dashTimeRemaining;
    private float dashCooldownRemaining;
    private float dashRechargeRemaining;
    private int currentDashCharges;
    private bool isDashing;
    private TrailRenderer dashTrail;

    public string     DisplayName     => gameObject.name;
    public EntityStats Stats          { get; private set; }
    public Wallet      ManaWallet     { get; private set; }
    public CombatReceiver CombatReceiver { get; private set; }
    public bool        MovementEnabled => movementEnabled;
    public bool        IsDashing => isDashing;
    public int         CurrentDashCharges => currentDashCharges;
    public int         MaxDashCharges => settings.MaxDashCharges;
    public string TradeParticipantId => "player";
    public Wallet TradeWallet => ManaWallet;
    public InventoryModel TradeInventory => InventoryUI.Model;

    // Pure form of the trail color; converted to a UnityEngine.Color only where needed.
    private Color TrailColor =>
        new Color(settings.DashTrailR, settings.DashTrailG, settings.DashTrailB, settings.DashTrailA);

    /// <summary>Applies movement and dash tuning from the active world's avatar profile.</summary>
    public void ApplyAvatarProfile(PlayerAvatarProfile profile)
    {
        if (profile == null) return;

        settings.MoveSpeed = Mathf.Max(settings.MinMoveSpeed, profile.MoveSpeed);
        settings.DashEnabled = profile.DashEnabled;
        settings.DashDistanceInPlayerLengths = Mathf.Max(settings.MinDashDistanceInPlayerLengths, profile.DashDistanceInPlayerLengths);
        settings.DashSpeedMultiplier = Mathf.Max(settings.MinDashSpeedMultiplier, profile.DashSpeedMultiplier);
        settings.DashCooldown = Mathf.Max(settings.MinDashCooldown, profile.DashCooldown);
        settings.MaxDashCharges = Mathf.Max(settings.MinDashCharges, profile.MaxDashCharges);
        settings.DashRechargeSeconds = Mathf.Max(settings.MinDashRechargeSeconds, profile.DashRechargeSeconds);
        currentDashCharges = settings.MaxDashCharges;
        dashRechargeRemaining = 0f;
    }

    /// <summary>Movement speed in units/s. Reads/writes Settings.MoveSpeed.</summary>
    public float MoveSpeed
    {
        get => settings.MoveSpeed;
        set => settings.MoveSpeed = value;
    }

    private void Awake()
    {
        rb       = GetComponent<Rigidbody2D>();
        Stats    = GetComponent<EntityStats>();
        CombatReceiver = GetComponent<CombatReceiver>(); // may be null if CombatReceiver is not added

        bool hadWallet = TryGetComponent(out Wallet wallet);
        ManaWallet = hadWallet ? wallet : gameObject.AddComponent<Wallet>();
        Stats.BindManaWallet(ManaWallet, initializeFromStats: !hadWallet);

        // Player HP lives in the GameSession model so it is shared across avatars/scenes and out of
        // the MonoBehaviour (Engine-Free Core). The first player binds and seeds it; later avatars
        // adopt the existing model via EntityStats.BindHealthModel.
        GameSessionHost.EnsureExists();
        GameSession session = GameSessionHost.Session;
        if (session != null)
            Stats.BindHealthModel(session.GetOrCreatePlayerHealth(Stats.MaxHp, Stats.Hp));

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

        currentDashCharges = Mathf.Max(settings.MinDashCharges, settings.MaxDashCharges);
        EnsureDashTrail();
    }

    // Stops and clears the runtime trail if the player controller becomes disabled.
    private void OnDisable()
    {
        StopAndClearDashTrail();
    }

    private void Update()
    {
        if (dashCooldownRemaining > 0f)
            dashCooldownRemaining = Mathf.Max(0f, dashCooldownRemaining - Time.deltaTime);

        RechargeDashCharges();

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

        if (settings.DashEnabled && !isDashing && currentDashCharges > 0 &&
            dashCooldownRemaining <= 0f && WasDashPressedThisFrame())
            BeginDash();
    }

    private void FixedUpdate()
    {
        if (movementEnabled && isDashing)
        {
            float dashSpeed = Mathf.Max(settings.MinDashSpeed, settings.MoveSpeed * settings.DashSpeedMultiplier);
            float stepFraction = Mathf.Clamp01(dashTimeRemaining / Time.fixedDeltaTime);
            rb.linearVelocity = dashDirection * dashSpeed * stepFraction;
            dashTimeRemaining -= Time.fixedDeltaTime;
            if (dashTimeRemaining <= 0f)
            {
                dashTimeRemaining = 0f;
                isDashing = false;
                EndDashTrailEmission();
            }
            return;
        }

        rb.linearVelocity = movementEnabled ? moveInput * settings.MoveSpeed : Vector2.zero;
    }

    // Starts a fixed-distance dash in the direction the player visual currently faces.
    private void BeginDash()
    {
        if (currentDashCharges <= 0)
            return;

        bool wasFullyCharged = currentDashCharges == settings.MaxDashCharges;
        currentDashCharges--;
        if (wasFullyCharged)
            dashRechargeRemaining = settings.DashRechargeSeconds;

        dashDirection = GetFacingDirection();
        float dashSpeed = Mathf.Max(settings.MinDashSpeed, settings.MoveSpeed * settings.DashSpeedMultiplier);
        float dashDistance = playerLength * settings.DashDistanceInPlayerLengths;
        dashTimeRemaining = dashDistance / dashSpeed;
        dashCooldownRemaining = settings.DashCooldown;
        isDashing = true;
        BeginDashTrail();
    }

    // Restores one missing dash every configured recharge interval until all charges are full.
    private void RechargeDashCharges()
    {
        if (currentDashCharges >= settings.MaxDashCharges)
        {
            currentDashCharges = settings.MaxDashCharges;
            dashRechargeRemaining = 0f;
            return;
        }

        dashRechargeRemaining -= Time.deltaTime;
        while (dashRechargeRemaining <= 0f && currentDashCharges < settings.MaxDashCharges)
        {
            currentDashCharges++;
            if (currentDashCharges < settings.MaxDashCharges)
                dashRechargeRemaining += Mathf.Max(settings.MinDashRechargeInterval, settings.DashRechargeSeconds);
            else
                dashRechargeRemaining = 0f;
        }
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

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!movementEnabled)
        {
            moveInput = Vector2.zero;
            isDashing = false;
            dashTimeRemaining = 0f;
            StopAndClearDashTrail();
            rb.linearVelocity = Vector2.zero;
        }
    }
}
