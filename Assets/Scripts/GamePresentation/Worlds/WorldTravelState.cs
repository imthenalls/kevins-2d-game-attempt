using Game.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Unity adapter for world travel. The authoritative state — active world, each world's remembered
/// logical return position, unlocked abilities, and the shared wallet snapshot — lives in the
/// engine-free <see cref="WorldTravelModel"/> owned by <see cref="GameSession"/>; this component only
/// performs the Unity operations: scene loading, Grid conversion, character activation, and
/// transform/stats binding. It creates itself before scene load.
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
    public static WorldTravelState Instance { get; private set; }

    public event Action<WorldLayer> OnWorldChanged;
    public event Action<WorldLayer, string> OnAbilityUnlocked;

    private WorldTravelModel model;

    public WorldLayer CurrentWorld => model != null ? model.CurrentWorld : WorldLayer.WorldA;

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
        GameSessionHost.EnsureExists();
        model = GameSessionHost.Session.WorldTravel;
        model.AbilityUnlocked += HandleAbilityUnlocked;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (model != null)
            model.AbilityUnlocked -= HandleAbilityUnlocked;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Instance = null;
    }

    private void HandleAbilityUnlocked(WorldLayer world, string abilityId)
        => OnAbilityUnlocked?.Invoke(world, abilityId);

    public void RememberPosition(WorldLayer world, string scene, Vector3 position)
    {
        if (string.IsNullOrWhiteSpace(scene)) return;

        var remembered = new RememberedWorldPosition
        {
            Scene = scene.Trim(),
            LegacyX = position.x,
            LegacyY = position.y,
            LegacyZ = position.z,
        };

        Grid grid = FindAnyObjectByType<Grid>();
        if (grid != null)
        {
            Vector3Int cell = grid.WorldToCell(position);
            Vector3 center = grid.GetCellCenterWorld(cell);
            remembered.HasCell = true;
            remembered.CellX = cell.x;
            remembered.CellY = cell.y;
            remembered.OffsetX = position.x - center.x;
            remembered.OffsetY = position.y - center.y;
        }

        model.SetPosition(world, remembered);
    }

    public void RememberTravelerPosition(Transform traveler)
    {
        if (traveler == null) return;

        string scene = SceneManager.GetActiveScene().name;

        // Prefer the player's authoritative logical position so travel uses the same grid cell as
        // save/load (Engine-Free Core PositionModel) instead of recomputing from the transform.
        PositionModel position = traveler.GetComponent<PlayerControllerBase>() != null
            ? GameSessionHost.Session?.PlayerPosition
            : null;

        if (position != null)
        {
            model.SetPosition(CurrentWorld, new RememberedWorldPosition
            {
                Scene = scene,
                HasCell = true,
                CellX = position.CellX,
                CellY = position.CellY,
                OffsetX = position.OffsetX,
                OffsetY = position.OffsetY,
                LegacyX = traveler.position.x,
                LegacyY = traveler.position.y,
                LegacyZ = traveler.position.z,
            });
        }
        else
        {
            RememberPosition(CurrentWorld, scene, traveler.position);
        }

        CaptureSharedPlayerState(traveler);
    }

    public bool TryGetRememberedPosition(
        WorldLayer world,
        out string scene,
        out Vector3 position)
    {
        if (model.TryGetPosition(world, out RememberedWorldPosition remembered))
        {
            scene = remembered.Scene;
            position = ResolveWorldPosition(remembered);
            return true;
        }

        scene = string.Empty;
        position = Vector3.zero;
        return false;
    }

    /// <summary>Converts a remembered position back to world space, preferring the grid cell.</summary>
    private static Vector3 ResolveWorldPosition(RememberedWorldPosition remembered)
    {
        if (remembered.HasCell)
        {
            Grid grid = FindAnyObjectByType<Grid>();
            if (grid != null)
            {
                Vector3 center = grid.GetCellCenterWorld(new Vector3Int(remembered.CellX, remembered.CellY, 0));
                return new Vector3(center.x + remembered.OffsetX, center.y + remembered.OffsetY, 0f);
            }
        }

        return new Vector3(remembered.LegacyX, remembered.LegacyY, remembered.LegacyZ);
    }

    public void SetCurrentWorld(WorldLayer world)
    {
        bool changed = model.CurrentWorld != world;
        if (changed)
        {
            WorldCharacter currentCharacter = FindCharacter(model.CurrentWorld);
            if (currentCharacter != null && currentCharacter.gameObject.activeInHierarchy)
                CaptureSharedPlayerState(currentCharacter.transform);
        }

        model.SetCurrentWorld(world);
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
        => model.WritePositions(destination);

    public void RegisterCharacter(WorldCharacter character)
    {
        if (character == null) return;

        PlayerAvatarProfile profile = character.Profile;
        if (profile != null && profile.StartingAbilityIds != null)
        {
            for (int i = 0; i < profile.StartingAbilityIds.Length; i++)
                model.GrantAbility(profile.World, profile.StartingAbilityIds[i]);
        }

        if (character.World != CurrentWorld)
        {
            character.SetActiveForWorld(CurrentWorld);
            return;
        }

        character.ApplyProfile();
        if (model.HasSharedPlayerState)
            ApplySharedPlayerState(character.transform);
        else
            CaptureSharedPlayerState(character.transform);
    }

    public bool HasAbility(WorldLayer world, string abilityId)
        => model.HasAbility(world, abilityId);

    public bool HasAbility(string abilityId) => HasAbility(CurrentWorld, abilityId);

    public bool UnlockAbility(WorldLayer world, string abilityId)
        => model.UnlockAbility(world, abilityId);

    public void WriteAbilities(List<WorldAbilitySaveEntry> destination)
        => model.WriteAbilities(destination);

    public void LoadAbilities(List<WorldAbilitySaveEntry> savedAbilities)
        => model.LoadAbilities(savedAbilities);

    public void CaptureSharedPlayerState(Transform traveler)
    {
        if (traveler == null || !traveler.TryGetComponent(out PlayerControllerBase controller))
            return;

        EntityStats stats = controller.Stats != null
            ? controller.Stats
            : traveler.GetComponent<EntityStats>();
        Wallet wallet = controller.ManaWallet != null
            ? controller.ManaWallet
            : traveler.GetComponent<Wallet>();
        if (stats == null) return;

        model.CaptureSharedWallet(wallet != null ? wallet.GetSaveData() : null);
    }

    public void ApplySharedPlayerState(Transform traveler)
    {
        if (!model.HasSharedPlayerState || traveler == null ||
            !traveler.TryGetComponent(out PlayerControllerBase controller))
            return;

        EntityStats stats = controller.Stats != null
            ? controller.Stats
            : traveler.GetComponent<EntityStats>();
        Wallet wallet = controller.ManaWallet != null
            ? controller.ManaWallet
            : traveler.GetComponent<Wallet>();
        if (stats == null) return;

        WalletSaveData sharedWallet = model.SharedWallet;
        if (wallet != null && sharedWallet != null)
            wallet.LoadSaveData(sharedWallet);
    }

    public void LoadSharedPlayerState(WalletSaveData wallet)
        => model.LoadSharedWallet(wallet);

    public void LoadState(string activeWorld, List<WorldPositionSaveEntry> savedPositions)
    {
        model.ClearPositions();
        if (savedPositions != null)
        {
            for (int i = 0; i < savedPositions.Count; i++)
            {
                WorldPositionSaveEntry entry = savedPositions[i];
                if (entry == null || !Enum.TryParse(entry.world, out WorldLayer world))
                    continue;

                if (entry.hasCell)
                {
                    model.SetPosition(world, RememberedWorldPosition.FromSave(entry));
                }
                else
                {
                    // Legacy save: convert the stored world floats through the scene Grid.
                    RememberPosition(world, entry.scene, new Vector3(entry.x, entry.y, entry.z));
                }
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
}
