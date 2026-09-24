using Game.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Planar-isometric 3D player controller. The world is real 3D (XZ ground plane, Y up), but gameplay
/// is verticality-free: the body never leaves the plane and movement is top-down on XZ. WASD / stick
/// input is made camera-relative, so W always moves "up" on screen regardless of the rig's yaw.
///
/// Derives from <see cref="PlayerControllerBase"/> so shared systems (inventory, save, world travel,
/// scene rules, bootstrap) work unchanged. Tuning lives in the pure-C# Game.Core.PlayerMovementConfig.
///
/// Unity setup:
///   1. Add to the player root GameObject.
///   2. Add a Rigidbody (Use Gravity off, Freeze Position Y + Freeze Rotation).
///   3. Add a CapsuleCollider (a thin upright capsule) plus EntityStats (required).
///   4. Optionally add CombatReceiver / CombatAttacker3D.
///   5. Put the sprite on a child with a BillboardSprite; assign it to Visual Transform.
///
/// Runtime API: MovementEnabled/SetMovementEnabled, MoveSpeed, Stats, ManaWallet,
/// ApplyAvatarProfile, CapturePositionModel, ApplyPositionModel, IsDashing.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EntityStats))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerController3D : PlayerControllerBase
{
    [Header("Movement Settings (Game.Data)")]
    [Tooltip("Authoritative movement/facing/dash tuning. Stored in the pure-C# Game.Data layer.")]
    [SerializeField] private PlayerMovementConfig settings = new PlayerMovementConfig();

    [Header("Unity References")]
    [Tooltip("Visual child that carries the billboarded sprite.")]
    [SerializeField] private Transform visualTransform;

    [Tooltip("World size of one logical grid cell on the XZ plane.")]
    [SerializeField, Min(0.01f)] private float gridCellSize = 1f;

    private Rigidbody rb;
    private Vector2 moveInput;
    private bool movementEnabled = true;
    private Vector2 lastMovementDirection = Vector2.up;
    private Vector3 dashDirection;
    private float playerLength;
    private float dashTimeRemaining;
    private float dashCooldownRemaining;
    private float dashRechargeRemaining;
    private int currentDashCharges;
    private bool isDashing;
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
    public bool IsDashing => isDashing;
    public int CurrentDashCharges => currentDashCharges;
    public int MaxDashCharges => settings.MaxDashCharges;

    /// <summary>Yaw (degrees) the player is aiming/facing, derived from the last movement input.</summary>
    public float FacingYaw
    {
        get
        {
            Vector3 direction = WorldDirection(lastMovementDirection);
            if (direction.sqrMagnitude < 0.0001f)
                direction = WorldDirection(Vector2.up);

            // Unity +Y euler: positive yaw turns local +X toward -Z, so negate the z component.
            return Mathf.Atan2(-direction.z, direction.x) * Mathf.Rad2Deg;
        }
    }

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
        currentDashCharges = settings.MaxDashCharges;
        dashRechargeRemaining = 0f;
    }

    /// <summary>Movement speed in units/s. Reads/writes Settings.MoveSpeed.</summary>
    public override float MoveSpeed
    {
        get => settings.MoveSpeed;
        set => settings.MoveSpeed = value;
    }

    private void Awake()
    {
        rb       = GetComponent<Rigidbody>();
        stats    = GetComponent<EntityStats>();
        combatReceiver = GetComponent<CombatReceiver>();
        stats.EnsureInitialized();

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        bool hadWallet = TryGetComponent(out Wallet wallet);
        manaWallet = hadWallet ? wallet : gameObject.AddComponent<Wallet>();
        stats.BindManaWallet(manaWallet, initializeFromStats: !hadWallet);

        GameSessionHost.EnsureExists();
        GameSession session = GameSessionHost.Session;
        if (session != null)
            stats.BindHealthModel(session.GetOrCreatePlayerHealth(stats.MaxHp, stats.Hp));

        if (session != null)
        {
            Vector3Int cell = WorldToCell(transform.position);
            Vector3 center = CellCenter(cell);
            positionModel = session.GetOrCreatePlayerPosition(
                cell.x, cell.y, transform.position.x - center.x, transform.position.z - center.z);
            positionModel.Changed += HandlePositionChanged;
            HandlePositionChanged(positionModel);
        }

        Collider playerCollider = GetComponent<Collider>();
        playerLength = settings.DefaultPlayerLength;
        if (playerCollider != null)
        {
            Vector3 size = playerCollider.bounds.size;
            playerLength = Mathf.Max(settings.MinPlayerLength, size.x, size.z);
        }

        currentDashCharges = Mathf.Max(settings.MinDashCharges, settings.MaxDashCharges);
        EnsureDashTrail();
    }

    private void OnDisable()
    {
        StopAndClearDashTrail();
    }

    private void LateUpdate()
    {
        CapturePositionModel();
    }

    /// <summary>Mirrors the physics transform into the authoritative session position model.</summary>
    public override void CapturePositionModel()
    {
        if (positionModel == null)
            return;

        Vector3Int cell = WorldToCell(transform.position);
        Vector3 center = CellCenter(cell);
        positionModel.Set(
            cell.x,
            cell.y,
            transform.position.x - center.x,
            transform.position.z - center.z);
    }

    /// <summary>Applies the authoritative session position model back onto the physics body.</summary>
    public override void ApplyPositionModel()
    {
        HandlePositionChanged(positionModel);
    }

    private void HandlePositionChanged(PositionModel model)
    {
        if (model == null)
            return;

        Vector3 target = CellCenter(new Vector3Int(model.CellX, model.CellY, 0))
                         + new Vector3(model.OffsetX, 0f, model.OffsetY);
        target.y = transform.position.y;

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
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
        }

        if (Gamepad.current != null)
            input += Gamepad.current.leftStick.ReadValue();

        moveInput = Vector2.ClampMagnitude(input, 1f);
#else
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(horizontal, vertical).normalized;
#endif

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

        rb.linearVelocity = movementEnabled ? WorldDirection(moveInput) * settings.MoveSpeed : Vector3.zero;
    }

    // Maps a screen-relative input (x = right, y = up) onto the XZ ground plane using the camera's
    // yaw, so W always moves the player "up" on screen in any isometric orientation.
    private Vector3 WorldDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.0001f)
            return Vector3.zero;

        Camera camera = Camera.main;
        Vector3 forward;
        Vector3 right;

        if (camera != null)
        {
            forward = camera.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            right = camera.transform.right;
            right.y = 0f;
            right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
        }
        else
        {
            forward = Vector3.forward;
            right = Vector3.right;
        }

        return (right * input.x + forward * input.y).normalized;
    }

    private void BeginDash()
    {
        if (currentDashCharges <= 0)
            return;

        bool wasFullyCharged = currentDashCharges == settings.MaxDashCharges;
        currentDashCharges--;
        if (wasFullyCharged)
            dashRechargeRemaining = settings.DashRechargeSeconds;

        dashDirection = WorldDirection(lastMovementDirection);
        if (dashDirection.sqrMagnitude <= 0.0001f)
            dashDirection = WorldDirection(Vector2.up);

        float dashSpeed = Mathf.Max(settings.MinDashSpeed, settings.MoveSpeed * settings.DashSpeedMultiplier);
        float dashDistance = playerLength * settings.DashDistanceInPlayerLengths;
        dashTimeRemaining = dashDistance / dashSpeed;
        dashCooldownRemaining = settings.DashCooldown;
        isDashing = true;
        BeginDashTrail();
    }

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

        Shader trailShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (trailShader == null)
            trailShader = Shader.Find("Sprites/Default");
        if (trailShader != null)
            dashTrail.material = new Material(trailShader) { name = "Runtime Player Dash Trail" };
    }

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

    private void EndDashTrailEmission()
    {
        if (dashTrail != null)
            dashTrail.emitting = false;
    }

    private void StopAndClearDashTrail()
    {
        if (dashTrail == null)
            return;

        dashTrail.emitting = false;
        dashTrail.Clear();
    }

    private bool WasDashPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftShiftKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown((KeyCode)settings.LegacyDashKeyCode);
#endif
    }

    public override void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (!movementEnabled)
        {
            moveInput = Vector2.zero;
            isDashing = false;
            dashTimeRemaining = 0f;
            StopAndClearDashTrail();
            rb.linearVelocity = Vector3.zero;
        }
    }

    private Vector3Int WorldToCell(Vector3 position)
    {
        return new Vector3Int(
            Mathf.FloorToInt(position.x / gridCellSize),
            Mathf.FloorToInt(position.z / gridCellSize),
            0);
    }

    private Vector3 CellCenter(Vector3Int cell)
    {
        return new Vector3(
            (cell.x + 0.5f) * gridCellSize,
            0f,
            (cell.y + 0.5f) * gridCellSize);
    }
}
