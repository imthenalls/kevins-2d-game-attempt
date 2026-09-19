using Game.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum WorldLayer
{
    WorldA,
    WorldB,
}

/// <summary>
/// Persistent authority for the active world, each world's last scene position and abilities,
/// shared player HP/mana, and playable character activation. It creates itself before scene load.
///
/// Unity setup:
///   1. No manager object is required; the singleton creates itself automatically.
///   2. Add WorldCharacter to both playable character roots when both can exist in one scene.
///   3. Configure world-changing PortalTrigger2D components with their destination world.
///
/// Runtime API:
///   CurrentWorld reports the active layer. RememberPosition stores a return position.
///   SetCurrentWorld switches characters and inventory context. HasAbility and UnlockAbility
///   expose world-specific progression. CaptureSharedPlayerState and ApplySharedPlayerState move
///   shared stats between avatars. Load/Write methods provide save-system integration.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldTravelState : MonoBehaviour
{
    private sealed class RememberedPosition
    {
        public string scene;
        public Vector3 position;
    }

    public static WorldTravelState Instance { get; private set; }

    public event Action<WorldLayer> OnWorldChanged;
    public event Action<WorldLayer, string> OnAbilityUnlocked;

    public WorldLayer CurrentWorld { get; private set; } = WorldLayer.WorldA;

    private readonly Dictionary<WorldLayer, RememberedPosition> positions = new();
    private readonly Dictionary<WorldLayer, HashSet<string>> unlockedAbilities = new();
    private bool sharedPlayerStateInitialized;
    private WalletSaveData sharedWallet;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance == null)
            new GameObject("World Travel State").AddComponent<WorldTravelState>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    public void RememberPosition(WorldLayer world, string scene, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(scene)) return;
        positions[world] = new RememberedPosition
        {
            scene = scene.Trim(),
            position = position,
        };
    }

    public void RememberTravelerPosition(Transform traveler)
    {
        if (traveler == null) return;
        RememberPosition(CurrentWorld, SceneManager.GetActiveScene().name, traveler.position);
        CaptureSharedPlayerState(traveler);
    }

    public bool TryGetRememberedPosition(
        WorldLayer world,
        out string scene,
        out Vector3 position)
    {
        if (positions.TryGetValue(world, out RememberedPosition remembered))
        {
            scene = remembered.scene;
            position = remembered.position;
            return true;
        }

        scene = string.Empty;
        position = Vector3.zero;
        return false;
    }

    public void SetCurrentWorld(WorldLayer world)
    {
        bool changed = CurrentWorld != world;
        if (changed)
        {
            WorldCharacter currentCharacter = FindCharacter(CurrentWorld);
            if (currentCharacter != null && currentCharacter.gameObject.activeInHierarchy)
                CaptureSharedPlayerState(currentCharacter.transform);
        }

        CurrentWorld = world;
        ApplyActiveCharacters();
        if (changed)
            OnWorldChanged?.Invoke(world);
    }

    public Transform ResolveActiveTraveler(Transform fallback)
    {
        WorldCharacter[] characters = FindObjectsByType<WorldCharacter>(
            FindObjectsInactive.Include);

        for (int i = 0; i < characters.Length; i++)
        {
            WorldCharacter character = characters[i];
            if (character != null &&
                character.gameObject.scene == SceneManager.GetActiveScene() &&
                character.World == CurrentWorld)
            {
                if (!character.gameObject.activeSelf)
                    character.gameObject.SetActive(true);
                character.ApplyProfile();
                ApplySharedPlayerState(character.transform);
                return character.transform;
            }
        }

        return fallback;
    }

    public void WritePositions(List<WorldPositionSaveEntry> destination)
    {
        if (destination == null) return;
        destination.Clear();
        foreach (var pair in positions)
        {
            destination.Add(new WorldPositionSaveEntry
            {
                world = pair.Key.ToString(),
                scene = pair.Value.scene,
                x = pair.Value.position.x,
                y = pair.Value.position.y,
                z = pair.Value.position.z,
            });
        }
    }

    public void RegisterCharacter(WorldCharacter character)
    {
        if (character == null) return;

        PlayerAvatarProfile profile = character.Profile;
        if (profile != null && profile.StartingAbilityIds != null)
        {
            for (int i = 0; i < profile.StartingAbilityIds.Length; i++)
                AddAbility(profile.World, profile.StartingAbilityIds[i], notify: false);
        }

        if (character.World != CurrentWorld)
        {
            character.SetActiveForWorld(CurrentWorld);
            return;
        }

        character.ApplyProfile();
        if (sharedPlayerStateInitialized)
            ApplySharedPlayerState(character.transform);
        else
            CaptureSharedPlayerState(character.transform);
    }

    public bool HasAbility(WorldLayer world, string abilityId)
    {
        string normalizedId = NormalizeAbilityId(abilityId);
        return normalizedId.Length > 0 &&
               unlockedAbilities.TryGetValue(world, out HashSet<string> abilities) &&
               abilities.Contains(normalizedId);
    }

    public bool HasAbility(string abilityId) => HasAbility(CurrentWorld, abilityId);

    public bool UnlockAbility(WorldLayer world, string abilityId)
    {
        return AddAbility(world, abilityId, notify: true);
    }

    public void WriteAbilities(List<WorldAbilitySaveEntry> destination)
    {
        if (destination == null) return;
        destination.Clear();

        foreach (var pair in unlockedAbilities)
        {
            foreach (string abilityId in pair.Value)
            {
                destination.Add(new WorldAbilitySaveEntry
                {
                    world = pair.Key.ToString(),
                    abilityId = abilityId,
                });
            }
        }
    }

    public void LoadAbilities(List<WorldAbilitySaveEntry> savedAbilities)
    {
        unlockedAbilities.Clear();
        if (savedAbilities == null) return;

        for (int i = 0; i < savedAbilities.Count; i++)
        {
            WorldAbilitySaveEntry entry = savedAbilities[i];
            if (entry != null && Enum.TryParse(entry.world, out WorldLayer world))
                AddAbility(world, entry.abilityId, notify: false);
        }
    }

    public void CaptureSharedPlayerState(Transform traveler)
    {
        if (traveler == null || !traveler.TryGetComponent(out PlayerController2D controller))
            return;

        EntityStats stats = controller.Stats != null
            ? controller.Stats
            : traveler.GetComponent<EntityStats>();
        Wallet wallet = controller.ManaWallet != null
            ? controller.ManaWallet
            : traveler.GetComponent<Wallet>();
        if (stats == null) return;

        sharedWallet = wallet != null ? wallet.GetSaveData() : null;
        sharedPlayerStateInitialized = true;
    }

    public void ApplySharedPlayerState(Transform traveler)
    {
        if (!sharedPlayerStateInitialized || traveler == null ||
            !traveler.TryGetComponent(out PlayerController2D controller))
            return;

        EntityStats stats = controller.Stats != null
            ? controller.Stats
            : traveler.GetComponent<EntityStats>();
        Wallet wallet = controller.ManaWallet != null
            ? controller.ManaWallet
            : traveler.GetComponent<Wallet>();
        if (stats == null) return;

        if (wallet != null && sharedWallet != null)
            wallet.LoadSaveData(sharedWallet);
    }

    public void LoadSharedPlayerState(WalletSaveData wallet)
    {
        sharedWallet = wallet;
        sharedPlayerStateInitialized = true;
    }

    public void LoadState(string activeWorld, List<WorldPositionSaveEntry> savedPositions)
    {
        positions.Clear();
        if (savedPositions != null)
        {
            for (int i = 0; i < savedPositions.Count; i++)
            {
                WorldPositionSaveEntry entry = savedPositions[i];
                if (entry != null && Enum.TryParse(entry.world, out WorldLayer world))
                    RememberPosition(world, entry.scene, new Vector3(entry.x, entry.y, entry.z));
            }
        }

        if (!Enum.TryParse(activeWorld, out WorldLayer parsedWorld))
            parsedWorld = WorldLayer.WorldA;
        SetCurrentWorld(parsedWorld);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyActiveCharacters();
    }

    private void ApplyActiveCharacters()
    {
        WorldCharacter[] characters = FindObjectsByType<WorldCharacter>(
            FindObjectsInactive.Include);

        for (int i = 0; i < characters.Length; i++)
        {
            WorldCharacter character = characters[i];
            if (character != null && character.gameObject.scene == SceneManager.GetActiveScene())
                character.SetActiveForWorld(CurrentWorld);
        }
    }

    private WorldCharacter FindCharacter(WorldLayer world)
    {
        WorldCharacter[] characters = FindObjectsByType<WorldCharacter>(FindObjectsInactive.Include);
        for (int i = 0; i < characters.Length; i++)
        {
            WorldCharacter character = characters[i];
            if (character != null &&
                character.gameObject.scene == SceneManager.GetActiveScene() &&
                character.World == world)
                return character;
        }

        return null;
    }

    private bool AddAbility(WorldLayer world, string abilityId, bool notify)
    {
        string normalizedId = NormalizeAbilityId(abilityId);
        if (normalizedId.Length == 0) return false;

        if (!unlockedAbilities.TryGetValue(world, out HashSet<string> abilities))
        {
            abilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            unlockedAbilities[world] = abilities;
        }

        if (!abilities.Add(normalizedId)) return false;
        if (notify) OnAbilityUnlocked?.Invoke(world, normalizedId);
        return true;
    }

    private static string NormalizeAbilityId(string abilityId)
    {
        return string.IsNullOrWhiteSpace(abilityId)
            ? string.Empty
            : abilityId.Trim().ToLowerInvariant();
    }
}
