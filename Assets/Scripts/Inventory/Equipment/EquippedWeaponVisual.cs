using System.Collections;
using UnityEngine;

/// <summary>
/// Displays the currently equipped Weapon item as a SpriteRenderer attached to an entity's
/// visual hierarchy. It listens to EquipmentManager, hides the renderer when no weapon is
/// equipped, and animates a local rotation arc around the weapon's grip when CombatAttacker
/// starts an attack. The swing includes a backswing, strike, and recovery with no pose snaps.
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
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class EquippedWeaponVisual : MonoBehaviour
{
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private CombatAttacker combatAttacker;
    [SerializeField] private SpriteRenderer weaponRenderer;
    [Tooltip("Body renderer whose Flip X controls weapon facing. Auto-detected from parents.")]
    [SerializeField] private SpriteRenderer facingRenderer;

    [Header("Swing")]
    [SerializeField] private Transform swingTransform;
    [SerializeField] private float startAngleOffset = -70f;
    [SerializeField] private float endAngleOffset = 70f;
    [Tooltip("Hand/grip position inside the weapon sprite rect, normalized from bottom-left.")]
    [SerializeField] private Vector2 gripPivotNormalized = new Vector2(0.16f, 0.18f);

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
    private Coroutine swingCoroutine;

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

        SetWeapon(null);
    }

    private void Start() => TryBind();

    private void Update()
    {
        if (boundModel == null || boundAttacker == null)
            TryBind();
    }

    private void LateUpdate()
    {
        if (facingRenderer == null)
            facingRenderer = FindFacingRenderer();
        if (facingRenderer != null)
            SetFacingLeft(facingRenderer.flipX);
    }

    private void OnDisable()
    {
        Unbind();
        ResetSwingPose();
    }

    /// <summary>Redraws the equipped Weapon item immediately.</summary>
    public void Refresh()
    {
        SetWeapon(boundModel?.GetEquipped(EquipSlotType.Weapon));
    }

    /// <summary>Plays the configured visual swing without starting another damage attack.</summary>
    public void PlaySwing()
    {
        if (weaponRenderer == null || !weaponRenderer.enabled || weaponRenderer.sprite == null)
            return;

        if (swingCoroutine != null)
            StopCoroutine(swingCoroutine);

        float duration = boundAttacker != null ? boundAttacker.AttackDuration : 0.3f;
        swingCoroutine = StartCoroutine(SwingRoutine(duration));
    }

    /// <summary>Mirror the resting weapon pose and its swing to face left or right.</summary>
    public void SetFacingLeft(bool value)
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

    private void Unbind()
    {
        if (boundModel != null)
            boundModel.OnSlotChanged -= HandleSlotChanged;
        if (boundAttacker != null)
            boundAttacker.OnAttackStarted -= HandleAttackStarted;
        boundModel = null;
        boundAttacker = null;
    }

    private void HandleSlotChanged(EquipSlotType slot, ItemData newItem, ItemData _)
    {
        if (slot == EquipSlotType.Weapon)
            SetWeapon(newItem);
    }

    private void HandleAttackStarted() => PlaySwing();

    private IEnumerator SwingRoutine(float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float angle = EvaluateSwingAngle(normalized);
            ApplySwingPose(angle);
            yield return null;
        }

        swingCoroutine = null;
        ResetSwingPose();
    }

    private float EvaluateSwingAngle(float normalized)
    {
        const float backswingEnd = 0.2f;
        const float strikeEnd = 0.72f;

        if (normalized < backswingEnd)
        {
            float phase = Smooth01(normalized / backswingEnd);
            return Mathf.Lerp(0f, startAngleOffset, phase);
        }

        if (normalized < strikeEnd)
        {
            float phase = Smooth01((normalized - backswingEnd) / (strikeEnd - backswingEnd));
            return Mathf.Lerp(startAngleOffset, endAngleOffset, phase);
        }

        float recovery = Smooth01((normalized - strikeEnd) / (1f - strikeEnd));
        return Mathf.Lerp(endAngleOffset, 0f, recovery);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - (2f * value));
    }

    private void ApplySwingPose(float angle)
    {
        if (facingLeft)
            angle = -angle;

        Quaternion rotation = restLocalRotation * Quaternion.Euler(0f, 0f, angle);
        swingTransform.localRotation = rotation;

        if (!compensateGripPosition)
            return;

        Vector3 scaledGrip = Vector3.Scale(gripPointLocal, swingTransform.localScale);
        swingTransform.localPosition = restGripPosition - (rotation * scaledGrip);
    }

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
            Mathf.Lerp(bounds.min.x, bounds.max.x, Mathf.Clamp01(gripPivotNormalized.x)),
            Mathf.Lerp(bounds.min.y, bounds.max.y, Mathf.Clamp01(gripPivotNormalized.y)),
            0f);
        if (weaponRenderer.flipX)
            gripPointLocal.x = -gripPointLocal.x;

        Vector3 scaledGrip = Vector3.Scale(gripPointLocal, swingTransform.localScale);
        restGripPosition = restLocalPosition + (restLocalRotation * scaledGrip);
    }

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

    private void SetWeapon(ItemData item)
    {
        if (weaponRenderer == null)
            return;

        weaponRenderer.sprite = item != null ? item.icon : null;
        weaponRenderer.enabled = weaponRenderer.sprite != null;
        RefreshGripPivot();
    }
}
