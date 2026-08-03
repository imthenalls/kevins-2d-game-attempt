using UnityEngine;

/// <summary>
/// Displays the currently equipped Weapon item as a SpriteRenderer attached to an entity's
/// visual hierarchy. It listens to EquipmentManager and hides the renderer when no weapon
/// is equipped.
///
/// Unity setup:
///   1. Create a WeaponVisual child under the entity's rotating visual GameObject.
///   2. Add a SpriteRenderer and this component to WeaponVisual.
///   3. Assign Equipment Manager from the entity root and Weapon Renderer from WeaponVisual.
///   4. Position, rotate, scale, and set the renderer sorting order in the Inspector.
///
/// Runtime API:
///   Refresh() immediately redraws the currently equipped Weapon slot.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class EquippedWeaponVisual : MonoBehaviour
{
    [SerializeField] private EquipmentManager equipmentManager;
    [SerializeField] private SpriteRenderer weaponRenderer;

    private EquipmentModel boundModel;

    private void Awake()
    {
        if (weaponRenderer == null)
            weaponRenderer = GetComponent<SpriteRenderer>();
        if (equipmentManager == null)
            equipmentManager = GetComponentInParent<EquipmentManager>();

        SetWeapon(null);
    }

    private void Start() => TryBind();

    private void Update()
    {
        if (boundModel == null)
            TryBind();
    }

    private void OnDisable() => Unbind();

    /// <summary>Redraws the equipped Weapon item immediately.</summary>
    public void Refresh()
    {
        SetWeapon(boundModel?.GetEquipped(EquipSlotType.Weapon));
    }

    private void TryBind()
    {
        if (equipmentManager == null)
            equipmentManager = GetComponentInParent<EquipmentManager>();

        EquipmentModel model = equipmentManager != null ? equipmentManager.Model : null;
        if (model == null || model == boundModel)
            return;

        Unbind();
        boundModel = model;
        boundModel.OnSlotChanged += HandleSlotChanged;
        Refresh();
    }

    private void Unbind()
    {
        if (boundModel != null)
            boundModel.OnSlotChanged -= HandleSlotChanged;
        boundModel = null;
    }

    private void HandleSlotChanged(EquipSlotType slot, ItemData newItem, ItemData _)
    {
        if (slot == EquipSlotType.Weapon)
            SetWeapon(newItem);
    }

    private void SetWeapon(ItemData item)
    {
        if (weaponRenderer == null)
            return;

        weaponRenderer.sprite = item != null ? item.icon : null;
        weaponRenderer.enabled = weaponRenderer.sprite != null;
    }
}
