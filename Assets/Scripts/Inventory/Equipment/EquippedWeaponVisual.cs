using System.Collections;
using UnityEngine;

/// <summary>
/// Displays the currently equipped Weapon item as a SpriteRenderer attached to an entity's
/// visual hierarchy. It listens to EquipmentManager, hides the renderer when no weapon is
/// equipped, and animates a local rotation arc when CombatAttacker starts an attack.
///
/// Unity setup:
///   1. Create a WeaponVisual child under the entity's rotating visual GameObject.
///   2. Add a SpriteRenderer and this component to WeaponVisual.
///   3. Assign Equipment Manager and Combat Attacker from the entity root.
///   4. Assign Weapon Renderer and Swing Transform from WeaponVisual.
///   5. Configure Start/End Angle Offset; attack duration comes from CombatAttacker.
///
/// Runtime API:
///   Refresh() immediately redraws the currently equipped Weapon slot.
///   PlaySwing() plays only the visual arc; normal attacks trigger it automatically.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class EquippedWeaponVisual : MonoBehaviour
{
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private CombatAttacker combatAttacker;
    [SerializeField] private SpriteRenderer weaponRenderer;

    [Header("Swing")]
    [SerializeField] private Transform swingTransform;
    [SerializeField] private float startAngleOffset = -70f;
    [SerializeField] private float endAngleOffset = 70f;

    private EquipmentModel boundModel;
    private CombatAttacker boundAttacker;
    private Quaternion restLocalRotation;
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

        restLocalRotation = swingTransform.localRotation;

        SetWeapon(null);
    }

    private void Start() => TryBind();

    private void Update()
    {
        if (boundModel == null || boundAttacker == null)
            TryBind();
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
            float eased = normalized * normalized * (3f - (2f * normalized));
            float angle = Mathf.Lerp(startAngleOffset, endAngleOffset, eased);
            swingTransform.localRotation = restLocalRotation * Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        swingCoroutine = null;
        ResetSwingPose();
    }

    private void ResetSwingPose()
    {
        if (swingCoroutine != null)
        {
            StopCoroutine(swingCoroutine);
            swingCoroutine = null;
        }
        if (swingTransform != null)
            swingTransform.localRotation = restLocalRotation;
    }

    private void SetWeapon(ItemData item)
    {
        if (weaponRenderer == null)
            return;

        weaponRenderer.sprite = item != null ? item.icon : null;
        weaponRenderer.enabled = weaponRenderer.sprite != null;
    }
}
