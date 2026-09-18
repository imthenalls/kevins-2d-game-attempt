using System.Collections;
using Game.Core;
using UnityEngine;

/// <summary>
/// Displays the currently equipped Weapon item as a SpriteRenderer attached to an entity's
/// visual hierarchy. It listens to EquipmentManager, hides the renderer when no weapon is
/// equipped, and animates a local rotation arc around the weapon's grip when CombatAttacker
/// starts an attack. The weapon travels through one forward arc from its visible neutral pose,
/// then returns instantly to neutral only after the animation has ended.
///
/// Unity setup:
///   1. Create a WeaponVisual child under the entity's rotating visual GameObject.
///   2. Add a SpriteRenderer and this component to WeaponVisual.
///   3. Assign Equipment Manager and Combat Attacker from the entity root.
///   4. Assign Weapon Renderer and Swing Transform from WeaponVisual.
///   5. Configure Start/End Angle Offset; attack duration comes from CombatAttacker.
///   6. Grip Pivot Normalized identifies the hand position inside the sprite rect (0–1).
///
/// Runtime API:
///   Refresh() immediately redraws the currently equipped Weapon slot.
///   PlaySwing() plays only the visual arc; normal attacks trigger it automatically.
///   SetFacingLeft(bool) mirrors the held pose and swing around the owning character.
///   A runtime PolygonCollider2D traces the blade and reports overlaps to CombatAttacker only
///   while its attack window is open; existing CombatReceiver colliders act as hurtboxes.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class EquippedWeaponVisual : MonoBehaviour
{
    [Header("Config (Game.Data)")]
    [SerializeField] private WeaponVisualConfig config = new WeaponVisualConfig();

    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private CombatAttacker combatAttacker;
    [SerializeField] private SpriteRenderer weaponRenderer;
    [Tooltip("Body renderer whose Flip X controls weapon facing. Auto-detected from parents.")]
    [SerializeField] private SpriteRenderer facingRenderer;

    [Header("Swing")]
    [SerializeField] private Transform swingTransform;

    private EquipmentModel boundModel;
    private CombatAttacker boundAttacker;
    private Quaternion authoredRestLocalRotation;
    private Vector3 authoredRestLocalPosition;
    private Quaternion restLocalRotation;
    private Vector3 restLocalPosition;
    private Vector3 restGripPosition;
    private Vector3 gripPointLocal;
    private bool compensateGripPosition;
    private bool facingLeft;
    private bool hasPendingFacing;
    private bool pendingFacingLeft;
    private Coroutine swingCoroutine;
    private TrailRenderer arcTrail;
    private Transform arcTrailTransform;
    private PolygonCollider2D weaponHitbox;
    private readonly Collider2D[] hitboxResults = new Collider2D[16];
    private ContactFilter2D hitboxContactFilter;

    private Color TrailColor => new Color(config.TrailR, config.TrailG, config.TrailB, config.TrailA);

    // Finds required components and records the weapon's authored neutral holding pose.
    private void Awake()
    {
        if (weaponRenderer == null)
            weaponRenderer = GetComponent<SpriteRenderer>();
        if (equipmentManager == null)
            equipmentManager = GetComponentInParent<EquipmentManager>();
        if (combatAttacker == null)
            combatAttacker = GetComponentInParent<CombatAttacker>();
        if (swingTransform == null)
            swingTransform = transform;
        if (facingRenderer == null)
            facingRenderer = FindFacingRenderer();

        authoredRestLocalRotation = swingTransform.localRotation;
        authoredRestLocalPosition = swingTransform.localPosition;
        restLocalRotation = authoredRestLocalRotation;
        restLocalPosition = authoredRestLocalPosition;

        EnsureArcTrail();
        EnsureWeaponHitbox();
        SetWeapon(null);
    }

    // Connects this visual to the equipment and combat systems after initialization.
    private void Start() => TryBind();

    // Retries system binding if either dependency was not ready during Start.
    private void Update()
    {
        if (boundModel == null || boundAttacker == null)
            TryBind();
    }

    // Synchronizes the weapon's left/right facing with the character renderer.
    private void LateUpdate()
    {
        if (facingRenderer == null)
            facingRenderer = FindFacingRenderer();
        if (facingRenderer != null)
            SetFacingLeft(facingRenderer.flipX);

        EvaluateWeaponHitboxContacts();
    }

    // Removes event subscriptions and restores the weapon to its neutral pose.
    private void OnDisable()
    {
        Unbind();
        if (weaponHitbox != null)
            weaponHitbox.enabled = false;
        StopAndClearArcTrail();
        ResetSwingPose();
    }

    /// <summary>Displays the item currently stored in the equipment model's Weapon slot.</summary>
    public void Refresh()
    {
        SetWeapon(boundModel?.GetEquipped(EquipSlotType.Weapon));
    }

    /// <summary>Starts the visual swing using CombatAttacker's configured duration.</summary>
    public void PlaySwing()
    {
        if (weaponRenderer == null || !weaponRenderer.enabled || weaponRenderer.sprite == null)
            return;

        if (swingCoroutine != null)
            StopCoroutine(swingCoroutine);

        float duration = boundAttacker != null ? boundAttacker.AttackDuration : 0.3f;
        swingCoroutine = StartCoroutine(SwingRoutine(duration));
    }

    /// <summary>
    /// Requests mirrored facing, postponing the change until after an active swing finishes.
    /// </summary>
    public void SetFacingLeft(bool value)
    {
        // Do not let movement or AI facing updates cancel an attack halfway through its arc.
        // Remember the latest requested direction and apply it after the swing completes.
        if (swingCoroutine != null)
        {
            hasPendingFacing = true;
            pendingFacingLeft = value;
            return;
        }

        ApplyFacingImmediately(value);
    }

    // Mirrors the neutral pose, sprite, and grip when no attack animation is running.
    private void ApplyFacingImmediately(bool value)
    {
        if (facingLeft == value)
            return;

        ResetSwingPose();
        facingLeft = value;

        restLocalPosition = authoredRestLocalPosition;
        restLocalPosition.x = value
            ? -authoredRestLocalPosition.x
            : authoredRestLocalPosition.x;

        float authoredZ = Mathf.DeltaAngle(0f, authoredRestLocalRotation.eulerAngles.z);
        restLocalRotation = Quaternion.Euler(0f, 0f, value ? -authoredZ : authoredZ);

        if (weaponRenderer != null)
            weaponRenderer.flipX = value;

        RefreshGripPivot();
        ResetSwingPose();
    }

    // Finds the equipment/combat models and subscribes to their change and attack events.
    private void TryBind()
    {
        if (equipmentManager == null)
            equipmentManager = GetComponentInParent<EquipmentManager>();

        EquipmentModel model = equipmentManager != null ? equipmentManager.Model : null;
        if (model != null && model != boundModel)
        {
            if (boundModel != null)
                boundModel.OnSlotChanged -= HandleSlotChanged;
            boundModel = model;
            boundModel.OnSlotChanged += HandleSlotChanged;
            Refresh();
        }

        if (combatAttacker == null)
            combatAttacker = GetComponentInParent<CombatAttacker>();
        if (combatAttacker != null && combatAttacker != boundAttacker)
        {
            if (boundAttacker != null)
                boundAttacker.OnAttackStarted -= HandleAttackStarted;
            boundAttacker = combatAttacker;
            boundAttacker.OnAttackStarted += HandleAttackStarted;
        }
    }

    // Removes equipment and combat event subscriptions held by this visual.
    private void Unbind()
    {
        if (boundModel != null)
            boundModel.OnSlotChanged -= HandleSlotChanged;
        if (boundAttacker != null)
            boundAttacker.OnAttackStarted -= HandleAttackStarted;
        boundModel = null;
        boundAttacker = null;
    }

    // Refreshes the displayed sprite when the equipped Weapon slot changes.
    private void HandleSlotChanged(EquipSlotType slot, ItemData newItem, ItemData _)
    {
        if (slot == EquipSlotType.Weapon)
            SetWeapon(newItem);
    }

    // Begins the visual swing when CombatAttacker announces a new attack.
    private void HandleAttackStarted() => PlaySwing();

    // Runs the timed lower-right-to-lower-left arc, then restores the neutral holding pose.
    private IEnumerator SwingRoutine(float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        BeginArcTrail();

        // The held pose and attack-start pose are intentionally separate. Jump to the
        // lower-right attack position immediately, then animate only the forward sweep.
        ApplySwingPose(EvaluateSwingAngle(0f));

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float angle = EvaluateSwingAngle(normalized);
            ApplySwingPose(angle);
            yield return null;
        }

        EndArcTrailEmission();
        swingCoroutine = null;
        ResetSwingPose();

        if (hasPendingFacing)
        {
            bool requestedFacing = pendingFacingLeft;
            hasPendingFacing = false;
            ApplyFacingImmediately(requestedFacing);
        }
    }

    // Converts normalized animation progress into the current configured attack angle.
    private float EvaluateSwingAngle(float normalized)
    {
        return Mathf.Lerp(config.StartAngleOffset, config.EndAngleOffset, Smooth01(normalized));
    }

    // Applies smooth acceleration and deceleration to a zero-to-one progress value.
    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - (2f * value));
    }

    // Positions and rotates the sword on its widened orbit for one animation frame.
    private void ApplySwingPose(float attackAngle)
    {
        // Mirror the whole attack path when the character faces left. For the normal
        // right-facing pose this travels lower-right -> overhead -> lower-left.
        if (facingLeft)
            attackAngle = 180f - attackAngle;

        Vector3 restingOrbitPoint = compensateGripPosition
            ? restGripPosition
            : restLocalPosition;
        float restingAngle = Mathf.Atan2(restingOrbitPoint.y, restingOrbitPoint.x) * Mathf.Rad2Deg;
        float rotationFromHoldingPose = attackAngle - restingAngle;
        float attackRadius = restingOrbitPoint.magnitude * Mathf.Max(1f, config.AttackRadiusMultiplier);

        Quaternion arcRotation = Quaternion.Euler(0f, 0f, rotationFromHoldingPose);
        Quaternion rotation = restLocalRotation * arcRotation;
        swingTransform.localRotation = rotation;

        float attackRadians = attackAngle * Mathf.Deg2Rad;
        Vector3 orbitingGripPosition = new Vector3(
            Mathf.Cos(attackRadians) * attackRadius,
            Mathf.Sin(attackRadians) * attackRadius,
            restingOrbitPoint.z);

        if (!compensateGripPosition)
        {
            swingTransform.localPosition = orbitingGripPosition;
            return;
        }

        Vector3 scaledGrip = Vector3.Scale(gripPointLocal, swingTransform.localScale);
        swingTransform.localPosition = orbitingGripPosition - (rotation * scaledGrip);
    }

    // Calculates the sword handle position from the sprite bounds and normalized grip setting.
    private void RefreshGripPivot()
    {
        compensateGripPosition = weaponRenderer != null &&
                                 weaponRenderer.sprite != null &&
                                 weaponRenderer.transform == swingTransform;
        if (!compensateGripPosition)
        {
            restGripPosition = restLocalPosition;
            gripPointLocal = Vector3.zero;
            return;
        }

        Bounds bounds = weaponRenderer.sprite.bounds;
        gripPointLocal = new Vector3(
            Mathf.Lerp(bounds.min.x, bounds.max.x, Mathf.Clamp01(config.GripPivotX)),
            Mathf.Lerp(bounds.min.y, bounds.max.y, Mathf.Clamp01(config.GripPivotY)),
            0f);
        if (weaponRenderer.flipX)
            gripPointLocal.x = -gripPointLocal.x;

        Vector3 scaledGrip = Vector3.Scale(gripPointLocal, swingTransform.localScale);
        restGripPosition = restLocalPosition + (restLocalRotation * scaledGrip);
        RefreshArcTrailTip();
        RefreshWeaponHitboxShape();
    }

    // Creates a trigger collider on the rendered weapon; it is enabled only during attacks.
    private void EnsureWeaponHitbox()
    {
        if (weaponHitbox == null)
            weaponHitbox = GetComponent<PolygonCollider2D>();
        if (weaponHitbox == null)
            weaponHitbox = gameObject.AddComponent<PolygonCollider2D>();

        weaponHitbox.isTrigger = true;
        weaponHitbox.enabled = false;
        hitboxContactFilter = ContactFilter2D.noFilter;
        hitboxContactFilter.useTriggers = true;
    }

    // Fits a narrow four-point polygon from just above the grip to the visible blade tip.
    private void RefreshWeaponHitboxShape()
    {
        EnsureWeaponHitbox();
        if (weaponHitbox == null || weaponRenderer == null || weaponRenderer.sprite == null)
        {
            if (weaponHitbox != null)
                weaponHitbox.enabled = false;
            return;
        }

        Bounds bounds = weaponRenderer.sprite.bounds;
        Vector2 grip = gripPointLocal;
        Vector2 tip = new Vector2(
            weaponRenderer.flipX ? -bounds.max.x : bounds.max.x,
            bounds.max.y);
        Vector2 blade = tip - grip;
        float bladeLength = blade.magnitude;
        if (bladeLength <= 0.001f)
        {
            weaponHitbox.enabled = false;
            return;
        }

        Vector2 direction = blade / bladeLength;
        Vector2 bladeBase = grip + direction * (bladeLength * 0.18f);
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float halfWidth = Mathf.Max(0.025f, bladeLength * 0.055f);
        float tipHalfWidth = halfWidth * 0.35f;

        weaponHitbox.pathCount = 1;
        weaponHitbox.SetPath(0, new[]
        {
            bladeBase + perpendicular * halfWidth,
            tip + perpendicular * tipHalfWidth,
            tip - perpendicular * tipHalfWidth,
            bladeBase - perpendicular * halfWidth
        });
        weaponHitbox.enabled = false;
    }

    // Checks the actual blade polygon after the sword has moved for this rendered frame.
    private void EvaluateWeaponHitboxContacts()
    {
        bool canHit = boundAttacker != null && boundAttacker.IsWeaponHitWindowOpen &&
                      weaponRenderer != null && weaponRenderer.enabled &&
                      weaponRenderer.sprite != null;

        if (weaponHitbox == null)
        {
            if (!canHit)
                return;
            EnsureWeaponHitbox();
            RefreshWeaponHitboxShape();
        }

        weaponHitbox.enabled = canHit;
        if (!canHit)
            return;

        Physics2D.SyncTransforms();
        int hitCount = weaponHitbox.Overlap(hitboxContactFilter, hitboxResults);
        for (int i = 0; i < hitCount; i++)
        {
            CombatReceiver receiver = hitboxResults[i].GetComponentInParent<CombatReceiver>();
            if (receiver != null)
                boundAttacker.TryApplyWeaponHit(receiver);
            hitboxResults[i] = null;
        }
    }

    // Creates the red tapered TrailRenderer as a runtime child of the sword.
    private void EnsureArcTrail()
    {
        if (arcTrail != null || swingTransform == null)
            return;

        var trailObject = new GameObject("Sword Arc Trail");
        arcTrailTransform = trailObject.transform;
        arcTrailTransform.SetParent(swingTransform, false);

        arcTrail = trailObject.AddComponent<TrailRenderer>();
        arcTrail.time = config.TrailFadeTime;
        arcTrail.minVertexDistance = 0.025f;
        arcTrail.emitting = false;
        arcTrail.autodestruct = false;
        arcTrail.alignment = LineAlignment.View;
        arcTrail.textureMode = LineTextureMode.Stretch;
        arcTrail.numCornerVertices = 3;
        arcTrail.numCapVertices = 2;
        arcTrail.widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.7f, 0.35f),
            new Keyframe(1f, 0.02f));

        Color arcTrailColor = TrailColor;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(arcTrailColor, 0f),
                new GradientColorKey(new Color(0.55f, 0f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(arcTrailColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        arcTrail.colorGradient = gradient;

        Shader trailShader = Shader.Find("Sprites/Default");
        if (trailShader != null)
            arcTrail.material = new Material(trailShader) { name = "Runtime Sword Arc Trail" };

        if (weaponRenderer != null)
        {
            arcTrail.sortingLayerID = weaponRenderer.sortingLayerID;
            arcTrail.sortingOrder = weaponRenderer.sortingOrder - 1;
        }
    }

    // Places the trail emitter at the blade tip and scales its width to the weapon sprite.
    private void RefreshArcTrailTip()
    {
        EnsureArcTrail();
        if (arcTrail == null || arcTrailTransform == null || weaponRenderer == null ||
            weaponRenderer.sprite == null)
            return;

        Bounds bounds = weaponRenderer.sprite.bounds;
        arcTrailTransform.localPosition = new Vector3(
            weaponRenderer.flipX ? -bounds.max.x : bounds.max.x,
            bounds.max.y,
            0f);

        Vector3 scaledSize = Vector3.Scale(bounds.size, swingTransform.lossyScale);
        float weaponLength = Mathf.Max(Mathf.Abs(scaledSize.x), Mathf.Abs(scaledSize.y));
        arcTrail.widthMultiplier = Mathf.Clamp(weaponLength * 0.14f, 0.08f, 0.38f);
        arcTrail.time = config.TrailFadeTime;
    }

    // Clears an older trail and begins recording the current forward sweep.
    private void BeginArcTrail()
    {
        RefreshArcTrailTip();
        if (arcTrail == null)
            return;

        arcTrail.Clear();
        arcTrail.emitting = true;
    }

    // Stops adding trail points while allowing the completed red arc to fade away.
    private void EndArcTrailEmission()
    {
        if (arcTrail != null)
            arcTrail.emitting = false;
    }

    // Immediately removes the trail when the weapon visual itself is disabled.
    private void StopAndClearArcTrail()
    {
        if (arcTrail == null)
            return;

        arcTrail.emitting = false;
        arcTrail.Clear();
    }

    // Searches parent objects for the character SpriteRenderer that controls facing.
    private SpriteRenderer FindFacingRenderer()
    {
        Transform current = transform.parent;
        while (current != null)
        {
            SpriteRenderer candidate = current.GetComponent<SpriteRenderer>();
            if (candidate != null && candidate != weaponRenderer)
                return candidate;
            current = current.parent;
        }

        return null;
    }

    // Stops any active swing and returns the sword to its saved neutral transform.
    private void ResetSwingPose()
    {
        if (swingCoroutine != null)
        {
            StopCoroutine(swingCoroutine);
            swingCoroutine = null;
        }
        if (swingTransform != null)
        {
            swingTransform.localRotation = restLocalRotation;
            swingTransform.localPosition = restLocalPosition;
        }
    }

    // Assigns the equipped item's sprite, or hides the renderer when no weapon is equipped.
    private void SetWeapon(ItemData item)
    {
        if (weaponRenderer == null)
            return;

        weaponRenderer.sprite = item != null ? item.icon : null;
        weaponRenderer.enabled = weaponRenderer.sprite != null;
        RefreshGripPivot();
        RefreshWeaponHitboxShape();
        if (!weaponRenderer.enabled)
            StopAndClearArcTrail();
    }
}
