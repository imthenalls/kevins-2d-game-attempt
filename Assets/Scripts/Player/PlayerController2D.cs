using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Top-down 2D player controller. Reads WASD / left-stick directional input each frame
/// and drives the Rigidbody2D via linearVelocity. Implements IEntityController so any
/// system can accept either a player or NPC reference through the shared interface.
///
/// Unity setup:
///   1. Add to the player root GameObject.
///   2. Add a Rigidbody2D:
///        • Gravity Scale = 0  (or enable Force No Gravity to apply it via script).
///        • Freeze Z Rotation  (or enable Lock Rotation to apply it via script).
///   3. EntityStats is required (enforced by RequireComponent). Its legacy Starting MP and
///      Max MP initialize the canonical mana account when the player has no Wallet yet.
///   4. Wallet is found or added automatically on Awake. If manually added, configure
///      Starting Mana and Mana Capacity in the Wallet Inspector.
///   5. Optionally add CombatReceiver to the same GameObject so the player can take damage.
///   6. Optionally add CombatAttacker if the player should be able to attack.
///   7. Set Move Speed in the Inspector (default 6 units/s).
///   8. Configure Dash Distance In Player Lengths, Dash Speed Multiplier, and Dash Cooldown.
///
/// Movement is locked at runtime by SetMovementEnabled(false) — called automatically
/// by dialogue, inventory, and cutscene systems.
///
/// Runtime API:
///   SetMovementEnabled controls movement.
///   Stats and ManaWallet expose player state.
///   ITradeParticipant uses the stable id "player", ManaWallet, and InventoryUI.Model.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EntityStats))]
public class PlayerController2D : MonoBehaviour, IEntityController, ITradeParticipant
{
    [Header("Top-Down Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private bool lockRotation = true;
    [SerializeField] private bool forceNoGravity = true;

    [Header("Movement Facing")]
    [Tooltip("Visual child to rotate without rotating the Rigidbody2D or collider.")]
    [SerializeField] private Transform visualTransform;
    [SerializeField] private bool faceMovementDirection = true;
    [Tooltip("Direction the sprite tip points at zero rotation. Use 90 for up, 0 for right.")]
    [SerializeField] private float spriteForwardAngle = 90f;
    [Tooltip("Degrees per second. Set to 0 for immediate facing.")]
    [SerializeField, Min(0f)] private float facingTurnSpeed;
    [SerializeField, Min(0f)] private float facingInputDeadZone = 0.01f;

    [Header("Dash")]
    [SerializeField, Min(0.1f)] private float dashDistanceInPlayerLengths = 5f;
    [SerializeField, Min(1f)] private float dashSpeedMultiplier = 6f;
    [SerializeField, Min(0f)] private float dashCooldown = 0.4f;
    [SerializeField, Min(1)] private int maxDashCharges = 3;
    [SerializeField, Min(0.1f)] private float dashRechargeSeconds = 15f;
    [SerializeField] private KeyCode legacyDashKey = KeyCode.LeftShift;

    [Header("Dash Trail")]
    [SerializeField] private Color dashTrailColor = new Color(0.2f, 0.75f, 1f, 0.75f);
    [SerializeField, Min(0.05f)] private float dashTrailFadeTime = 0.3f;
    [SerializeField, Min(0.05f)] private float dashTrailWidthInPlayerLengths = 0.8f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private bool movementEnabled = true;
    private Vector2 lastMovementDirection = Vector2.up;
    private Vector2 dashDirection;
    private float playerLength = 1f;
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
    public int         MaxDashCharges => maxDashCharges;
    public string TradeParticipantId => "player";
    public Wallet TradeWallet => ManaWallet;
    public InventoryModel TradeInventory => InventoryUI.Model;

    /// <summary>Movement speed in units/s. Can be read or overridden at runtime (e.g. by SceneRulesManager).</summary>
    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = value;
    }

    private void Awake()
    {
        rb       = GetComponent<Rigidbody2D>();
        Stats    = GetComponent<EntityStats>();
        CombatReceiver = GetComponent<CombatReceiver>(); // may be null if CombatReceiver is not added

        bool hadWallet = TryGetComponent(out Wallet wallet);
        ManaWallet = hadWallet ? wallet : gameObject.AddComponent<Wallet>();
        Stats.BindManaWallet(ManaWallet, initializeFromStats: !hadWallet);

        if (forceNoGravity)
        {
            rb.gravityScale = 0f;
        }

        if (lockRotation)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        Collider2D playerCollider = GetComponent<Collider2D>();
        if (playerCollider != null)
        {
            Vector2 colliderSize = playerCollider.bounds.size;
            playerLength = Mathf.Max(0.1f, colliderSize.x, colliderSize.y);
        }

        currentDashCharges = Mathf.Max(1, maxDashCharges);
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

        if (moveInput.sqrMagnitude > facingInputDeadZone * facingInputDeadZone)
            lastMovementDirection = moveInput.normalized;

        if (!isDashing && currentDashCharges > 0 &&
            dashCooldownRemaining <= 0f && WasDashPressedThisFrame())
            BeginDash();
    }

    private void FixedUpdate()
    {
        if (movementEnabled && isDashing)
        {
            float dashSpeed = Mathf.Max(0.01f, moveSpeed * dashSpeedMultiplier);
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

        rb.linearVelocity = movementEnabled ? moveInput * moveSpeed : Vector2.zero;
    }

    // Starts a fixed-distance dash in the direction the player visual currently faces.
    private void BeginDash()
    {
        if (currentDashCharges <= 0)
            return;

        bool wasFullyCharged = currentDashCharges == maxDashCharges;
        currentDashCharges--;
        if (wasFullyCharged)
            dashRechargeRemaining = dashRechargeSeconds;

        dashDirection = GetFacingDirection();
        float dashSpeed = Mathf.Max(0.01f, moveSpeed * dashSpeedMultiplier);
        float dashDistance = playerLength * dashDistanceInPlayerLengths;
        dashTimeRemaining = dashDistance / dashSpeed;
        dashCooldownRemaining = dashCooldown;
        isDashing = true;
        BeginDashTrail();
    }

    // Restores one missing dash every configured recharge interval until all charges are full.
    private void RechargeDashCharges()
    {
        if (currentDashCharges >= maxDashCharges)
        {
            currentDashCharges = maxDashCharges;
            dashRechargeRemaining = 0f;
            return;
        }

        dashRechargeRemaining -= Time.deltaTime;
        while (dashRechargeRemaining <= 0f && currentDashCharges < maxDashCharges)
        {
            currentDashCharges++;
            if (currentDashCharges < maxDashCharges)
                dashRechargeRemaining += Mathf.Max(0.1f, dashRechargeSeconds);
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
        dashTrail.time = dashTrailFadeTime;
        dashTrail.minVertexDistance = 0.04f;
        dashTrail.emitting = false;
        dashTrail.autodestruct = false;
        dashTrail.alignment = LineAlignment.View;
        dashTrail.textureMode = LineTextureMode.Stretch;
        dashTrail.numCornerVertices = 3;
        dashTrail.numCapVertices = 2;
        dashTrail.widthMultiplier = playerLength * dashTrailWidthInPlayerLengths;
        dashTrail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.65f, 0.45f),
            new Keyframe(1f, 0.02f));

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(dashTrailColor, 0f),
                new GradientColorKey(dashTrailColor * 0.55f, 1f)
            },
            new[]
            {
                new GradientAlphaKey(dashTrailColor.a, 0f),
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

        dashTrail.time = dashTrailFadeTime;
        dashTrail.widthMultiplier = playerLength * dashTrailWidthInPlayerLengths;
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
            float radians = spriteForwardAngle * Mathf.Deg2Rad;
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
        return Input.GetKeyDown(legacyDashKey);
#endif
    }

    private void UpdateMovementFacing()
    {
        if (!faceMovementDirection ||
            visualTransform == null ||
            moveInput.sqrMagnitude <= facingInputDeadZone * facingInputDeadZone)
        {
            return;
        }

        float movementAngle = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg;
        float targetAngle = movementAngle - spriteForwardAngle;
        float currentAngle = visualTransform.localEulerAngles.z;
        float nextAngle = facingTurnSpeed <= 0f
            ? targetAngle
            : Mathf.MoveTowardsAngle(
                currentAngle,
                targetAngle,
                facingTurnSpeed * Time.deltaTime);

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
