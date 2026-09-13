using UnityEngine;

/// <summary>
/// Defines the movement rules and built-in ability IDs for one playable world avatar.
///
/// Unity setup:
///   1. Create one asset per avatar with Create > Game > Player Avatar Profile.
///   2. Set its World, Avatar Id, movement speed, dash tuning, and Starting Ability Ids.
///   3. Assign the asset to the matching WorldCharacter component on a player prefab.
///
/// Runtime API:
///   WorldCharacter reads this asset and applies it to PlayerController2D automatically.
/// </summary>
[CreateAssetMenu(fileName = "PlayerAvatarProfile", menuName = "Game/Player Avatar Profile")]
public sealed class PlayerAvatarProfile : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string avatarId = "world_a_player";
    [SerializeField] private WorldLayer world = WorldLayer.WorldA;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 6f;

    [Header("Dash")]
    [SerializeField] private bool dashEnabled = true;
    [SerializeField, Min(0.1f)] private float dashDistanceInPlayerLengths = 5f;
    [SerializeField, Min(1f)] private float dashSpeedMultiplier = 6f;
    [SerializeField, Min(0f)] private float dashCooldown = 0.4f;
    [SerializeField, Min(1)] private int maxDashCharges = 3;
    [SerializeField, Min(0.1f)] private float dashRechargeSeconds = 15f;

    [Header("Abilities")]
    [Tooltip("Generic IDs other systems can query through WorldTravelState.HasAbility.")]
    [SerializeField] private string[] startingAbilityIds = { "interact", "dash" };

    public string AvatarId => string.IsNullOrWhiteSpace(avatarId) ? world.ToString() : avatarId.Trim();
    public WorldLayer World => world;
    public float MoveSpeed => moveSpeed;
    public bool DashEnabled => dashEnabled;
    public float DashDistanceInPlayerLengths => dashDistanceInPlayerLengths;
    public float DashSpeedMultiplier => dashSpeedMultiplier;
    public float DashCooldown => dashCooldown;
    public int MaxDashCharges => maxDashCharges;
    public float DashRechargeSeconds => dashRechargeSeconds;
    public string[] StartingAbilityIds => startingAbilityIds;
}
