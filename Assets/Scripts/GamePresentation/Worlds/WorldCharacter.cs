using UnityEngine;

/// <summary>
/// Marks a playable character as belonging to one of the two world layers so only the
/// character for the active world receives input, renders, and runs gameplay components.
///
/// Unity setup:
///   1. Add this component to the root GameObject of each world's playable character.
///   2. Assign the matching PlayerAvatarProfile. World is used as a fallback when no profile exists.
///   3. Keep the character root, its camera, input, visuals, and colliders in the same hierarchy.
///      The complete root is enabled or disabled when worlds change.
///
/// Runtime API:
///   World and AvatarId identify the avatar; ApplyProfile applies its movement tuning;
///   HasAbility and UnlockAbility access its progression; SetActiveForWorld changes activation.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldCharacter : MonoBehaviour
{
    [SerializeField] private WorldLayer world = WorldLayer.WorldA;
    [Tooltip("Defines this avatar's identity, movement tuning, and starting abilities.")]
    [SerializeField] private PlayerAvatarProfile profile;

    public WorldLayer World => profile != null ? profile.World : world;
    public string AvatarId => profile != null ? profile.AvatarId : World.ToString();
    public PlayerAvatarProfile Profile => profile;

    private void Awake()
    {
        ApplyProfile();
    }

    private void Start()
    {
        WorldTravelState.Instance?.RegisterCharacter(this);
    }

    public void ApplyProfile()
    {
        if (profile != null && TryGetComponent(out PlayerControllerBase controller))
            controller.ApplyAvatarProfile(profile);
    }

    public bool HasAbility(string abilityId)
    {
        return WorldTravelState.Instance != null &&
               WorldTravelState.Instance.HasAbility(World, abilityId);
    }

    public bool UnlockAbility(string abilityId)
    {
        return WorldTravelState.Instance != null &&
               WorldTravelState.Instance.UnlockAbility(World, abilityId);
    }

    public void SetActiveForWorld(WorldLayer activeWorld)
    {
        gameObject.SetActive(World == activeWorld);
    }
}
