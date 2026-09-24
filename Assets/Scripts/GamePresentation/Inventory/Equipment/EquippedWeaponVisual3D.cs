using Game.Core;
using UnityEngine;

/// <summary>
/// 3D counterpart of <see cref="EquippedWeaponVisual"/> for planar-isometric scenes. Shows the
/// equipped Weapon item as a billboarded sprite that orbits the character, sweeps in a horizontal
/// arc while <see cref="CombatAttacker"/>'s hit window is open, and samples the blade with a 3D
/// sphere overlap to deliver hits through <c>CombatAttacker.TryApplyWeaponHit</c>. Because the
/// swing is horizontal on the XZ plane, a weapon hitbox collider is unnecessary.
///
/// Unity setup:
///   1. Create a pivot child of the entity (e.g. "WeaponPivot") and a sprite child under it
///      ("WeaponVisual") with a SpriteRenderer.
///   2. Add this component plus a BillboardSprite to the sprite child.
///   3. Assign Equipment Manager, Combat Attacker, Weapon Renderer and Swing Pivot from the root.
///   4. Tune the arc and reach on the nested Weapon Swing 3D config.
///
/// Runtime API: Refresh() redraws the equipped Weapon slot immediately.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class EquippedWeaponVisual3D : MonoBehaviour
{
    [Header("Config (Game.Data)")]
    [SerializeField] private WeaponSwing3DConfig config = new WeaponSwing3DConfig();

    [Header("Unity References")]
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private CombatAttacker combatAttacker;
    [SerializeField] private SpriteRenderer weaponRenderer;
    [Tooltip("Pivot that yaws around the character to sweep the blade. Defaults to this object's parent.")]
    [SerializeField] private Transform swingPivot;

    private EquipmentModel boundModel;
    private CombatAttacker boundAttacker;
    private PlayerControllerBase player;
    private float swingStartedAt;
    private float currentBladeYaw;
    private bool wasWindowOpen;

    private static readonly Collider[] HitBuffer = new Collider[16];

    private void Awake()
    {
        if (weaponRenderer == null)
            weaponRenderer = GetComponent<SpriteRenderer>();
        if (swingPivot == null)
            swingPivot = transform.parent != null ? transform.parent : transform;
        if (equipmentManager == null)
            equipmentManager = GetComponentInParent<EquipmentManager>();
        if (combatAttacker == null)
            combatAttacker = GetComponentInParent<CombatAttacker>();

        transform.localPosition = new Vector3(config.OrbitRadius, 0f, 0f);
        ResetPose();
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
        if (boundAttacker == null)
            return;

        float baseYaw = ResolveBaseYaw();
        bool windowOpen = boundAttacker.IsWeaponHitWindowOpen;

        if (windowOpen)
        {
            if (!wasWindowOpen)
                swingStartedAt = Time.time;

            float duration = Mathf.Max(0.01f, boundAttacker.AttackDuration);
            float normalized = Mathf.Clamp01((Time.time - swingStartedAt) / duration);
            float sweep = Mathf.Lerp(config.StartYaw, config.EndYaw, Smooth(normalized));
            currentBladeYaw = baseYaw + config.RestYaw + sweep;
            swingPivot.localRotation = Quaternion.Euler(0f, currentBladeYaw, 0f);
            EvaluateHits();
        }
        else
        {
            swingPivot.localRotation = Quaternion.Euler(0f, baseYaw + config.RestYaw, 0f);
        }

        wasWindowOpen = windowOpen;
    }

    // Player: aims where the player last moved. NPC: aims at the player. Otherwise no base yaw.
    private float ResolveBaseYaw()
    {
        PlayerController3D owner = GetComponentInParent<PlayerController3D>();
        if (owner != null)
            return owner.FacingYaw;

        if (player == null)
            player = FindAnyObjectByType<PlayerControllerBase>();
        if (player != null)
        {
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.0001f)
                return Mathf.Atan2(-toPlayer.z, toPlayer.x) * Mathf.Rad2Deg;
        }

        return 0f;
    }

    private void OnDisable()
    {
        if (boundModel != null)
            boundModel.OnSlotChanged -= HandleSlotChanged;
        ResetPose();
    }

    /// <summary>Displays the item currently stored in the equipment model's Weapon slot.</summary>
    public void Refresh()
    {
        SetWeapon(boundModel?.GetEquipped(EquipSlotType.Weapon) as ItemData);
    }

    // Samples everything within reach of the blade and offers any receiver inside the current
    // frontal cone to the attacker. Testing the whole reach each frame (not just the blade point)
    // prevents a fast swing tunnelling past a small target between frames.
    private void EvaluateHits()
    {
        Vector3 origin = swingPivot.position;
        float reach = config.OrbitRadius + config.BladeLength + config.HitRadius;

        int count = Physics.OverlapSphereNonAlloc(
            origin, reach, HitBuffer, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = HitBuffer[i];
            if (hit == null)
                continue;

            CombatReceiver receiver = hit.GetComponentInParent<CombatReceiver>();
            if (receiver == null)
                continue;

            Vector3 toTarget = receiver.transform.position - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > reach * reach)
                continue;

            float targetYaw = Mathf.Atan2(-toTarget.z, toTarget.x) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(currentBladeYaw, targetYaw)) <= config.HitConeDegrees)
                boundAttacker.TryApplyWeaponHit(receiver);
        }
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
        if (combatAttacker != null)
            boundAttacker = combatAttacker;
    }

    private void HandleSlotChanged(EquipSlotType slot, IItem newItem, IItem _)
    {
        if (slot == EquipSlotType.Weapon)
            SetWeapon(newItem as ItemData);
    }

    private void SetWeapon(ItemData item)
    {
        if (weaponRenderer == null)
            return;

        weaponRenderer.sprite = item != null ? item.icon : null;
        weaponRenderer.enabled = weaponRenderer.sprite != null;
    }

    private void ResetPose()
    {
        if (swingPivot != null)
            swingPivot.localRotation = Quaternion.Euler(0f, config.RestYaw, 0f);
    }

    private static float Smooth(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - (2f * value));
    }
}
