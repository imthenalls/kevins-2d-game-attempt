using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR

/// <summary>
/// Runs integration checks against the saved training scene in Play mode, then exits without
/// saving runtime changes. Results are written to Temp/training-arena-verification.txt.
/// Unity setup: none. Open NewScene and use Tools > Training Arena > Verify In Play Mode.
/// A Temp/training-arena-verify.request file requests the same check after compilation.
/// Do not interact with the game during verification; it temporarily moves and damages the player.
/// Runtime API: none. This editor-only runner removes itself when Play mode ends.
/// </summary>
public sealed class TrainingArenaVerification : MonoBehaviour
{
    private const string ArenaRoot = "Training Arena Wing";
    private const string SessionKey = "TrainingArenaVerification.Running";
    private const string SpawnedKey = "TrainingArenaVerification.Spawned";
    private const string Report = "Temp/training-arena-verification.txt";
    private const string Request = "Temp/training-arena-verify.request";
    private bool failed;
    private float startTime;
    private static TrainingArenaVerification current;

    private void Awake()
    {
        if (current != null && current != this)
        {
            Destroy(gameObject);
            return;
        }
        current = this;
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
    }

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(SessionKey, false))
                new GameObject("Training Arena Verification").AddComponent<TrainingArenaVerification>();
        };
        EditorApplication.update += () =>
        {
            if (EditorApplication.isPlaying && SessionState.GetBool(SessionKey, false) &&
                !SessionState.GetBool(SpawnedKey, false))
            {
                SessionState.SetBool(SpawnedKey, true);
                new GameObject("Training Arena Verification").AddComponent<TrainingArenaVerification>();
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            File.Delete(Request);
            Verify();
        };
    }

    [MenuItem("Tools/Training Arena/Verify In Play Mode")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before starting verification.");
        EditorSceneManager.OpenScene("Assets/Scenes/NewScene.unity", OpenSceneMode.Single);
        if (GameObject.Find(ArenaRoot) == null)
            throw new InvalidOperationException("The saved NewScene does not contain the training arena.");
        File.WriteAllText(Report, "Training arena integration verification\n");
        SessionState.SetBool(SessionKey, true);
        SessionState.SetBool(SpawnedKey, false);
        EditorApplication.isPlaying = true;
    }

    private IEnumerator Start()
    {
        if (current != this) yield break;
        startTime = Time.realtimeSinceStartup;
        Application.logMessageReceived += OnLog;
        IEnumerator tests = RunChecks();
        while (true)
        {
            object next;
            try
            {
                if (!tests.MoveNext()) break;
                next = tests.Current;
            }
            catch (Exception e)
            {
                File.AppendAllText(Report, "FAIL: " + e + "\n"); failed = true; break;
            }
            yield return next;
        }
        Application.logMessageReceived -= OnLog;
        File.AppendAllText(Report, failed ? "RESULT: FAIL\n" : "RESULT: PASS\n");
        SessionState.SetBool(SessionKey, false);
        SessionState.SetBool(SpawnedKey, false);
        EditorApplication.isPlaying = false;
    }

    private void Update()
    {
        if (Time.realtimeSinceStartup - startTime < 70f) return;
        File.AppendAllText(Report, "FAIL: verification timeout\nRESULT: FAIL\n");
        SessionState.SetBool(SessionKey, false);
        SessionState.SetBool(SpawnedKey, false);
        EditorApplication.isPlaying = false;
    }
    private void OnLog(string text, string trace, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        failed = true; File.AppendAllText(Report, "UNITY ERROR: " + text + "\n" + trace + "\n");
    }
    private static void Check(bool success, string message)
    {
        if (!success) throw new InvalidOperationException(message);
        File.AppendAllText(Report, "PASS: " + message + "\n");
    }
    private static void Place(PlayerController2D player, Vector2 position)
    {
        player.transform.position = position;
        var body = player.GetComponent<Rigidbody2D>(); body.position = position; body.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    private IEnumerator RunChecks()
    {
        yield return new WaitForSeconds(0.6f);
        var player = FindAnyObjectByType<PlayerController2D>();
        var playerBody = player.GetComponent<Rigidbody2D>();
        var playerReceiver = player.GetComponent<CombatReceiver>();
        player.SetMovementEnabled(false);
        player.enabled = false;
        var spawner = FindAnyObjectByType<TrainingEnemySpawner>();
        var floor = GameObject.Find("Floor - small room, hallway, arena").GetComponent<Tilemap>();
        var walls = GameObject.Find(ArenaRoot + "/Training Grid/Walls").GetComponent<Tilemap>();
        Check(playerReceiver.Invincible, "Authored player invincibility is preserved");
        Check(floor.GetUsedTilesCount() > 0 && walls.GetComponent<TilemapCollider2D>() != null,
            "Rooms use authored tilemaps and solid tilemap walls");
        var reachable = new HashSet<Vector3Int>(); var queue = new Queue<Vector3Int>();
        queue.Enqueue(new Vector3Int(46, 0, 0));
        Vector3Int[] directions = { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (!floor.HasTile(cell) || cell.x == 56 || !reachable.Add(cell)) continue;
            foreach (var d in directions) queue.Enqueue(cell + d);
        }
        Check(!reachable.Contains(new Vector3Int(70, 0, 0)), "Hallway door is the only walking route to the arena");
        foreach (var cell in floor.cellBounds.allPositionsWithin)
        {
            if (!floor.HasTile(cell)) continue;
            foreach (var d in directions)
                if (!floor.HasTile(cell + d)) CheckWall(walls.HasTile(cell + d), cell + d);
        }
        Check(true, "Every exterior floor edge has a wall");

        var manager = PortalManager.Instance;
        Check(manager.TryUsePortal("training_hub", player.transform), "Hub portal accepts travel");
        Check(player.transform.position.x > 44 && player.transform.position.x < 52, "Hub portal arrives in the small room");
        Vector3 arrival = player.transform.position;
        yield return new WaitForSeconds(0.6f);
        Check(Vector3.Distance(arrival, player.transform.position) < 0.05f, "Arrival does not immediately teleport back");
        Check(manager.TryUsePortal("training_entry", player.transform), "Return portal accepts travel");
        Check(player.transform.position.x < 10, "Return portal arrives in the NPC hub");

        var door = GameObject.Find("Training Arena Locked Door").GetComponent<SlidingDoor>();
        var keyring = PlayerKeyring.GetOrCreate(); keyring.Clear();
        Place(player, new Vector2(54, 0.5f));
        Check(!door.TryOpen(player.gameObject) && door.IsLocked, "Missing key keeps the door locked");
        keyring.AddKey(ItemDatabase.Instance.Get("golden_key"));
        Check(!door.TryOpen(player.gameObject) && door.IsLocked, "Existing golden key cannot unlock the arena");
        playerBody.linearVelocity = Vector2.right * 36f;
        yield return new WaitForSeconds(0.2f);
        Check(playerBody.position.x < 56.5f, "Closed door blocks the player at dash speed");
        playerBody.linearVelocity = Vector2.zero;

        var giver = FindObjectsByType<NpcDialogue>(FindObjectsSortMode.None).Single(n => n.Controller.NpcId == "training_key_keeper");
        Check(giver.TryGetStartNode(out var line) && line.endConversation, "Key NPC has a complete dialogue");
        Check(giver.GiveInventoryGift(player.gameObject) == 1 && keyring.HasKey("training_arena_key"), "NPC transfers the correct key into the keyring");
        Check(giver.GiveInventoryGift(player.gameObject) == 0 && keyring.CountKey("training_arena_key") == 1, "Repeated dialogue does not duplicate the key");
        InventoryUI.Instance?.Close();
        Check(door.TryOpen(player.gameObject), "Correct key unlocks the door");
        yield return new WaitForSeconds(0.6f);
        Check(door.IsOpen && !door.IsLocked, "Door finishes opening and remains unlocked");
        Place(player, new Vector2(54, 0.5f)); playerBody.linearVelocity = Vector2.right * 20f;
        yield return new WaitForSeconds(0.25f);
        Check(playerBody.position.x > 57, "Player can pass through the open door");
        playerBody.linearVelocity = Vector2.zero;

        Place(player, new Vector2(64, 2.5f));
        Check(spawner.TryGetCurrentLine(out _), "Green box presents dialogue");
        spawner.EndInteraction(player.gameObject);
        Check(spawner.ActiveEnemy == null, "Cancelling the box dialogue does not spawn an enemy");
        spawner.Advance(); spawner.EndInteraction(player.gameObject);
        Check(spawner.ActiveEnemy != null && !spawner.TrySpawn(), "Completing dialogue spawns exactly one opponent");
        var enemy = spawner.ActiveEnemy;
        var ai = enemy.GetComponent<NpcDashMeleeController>();
        ai.enabled = false;
        Place(player, new Vector2(74, 0));
        yield return new WaitForSeconds(0.2f);
        Check(enemy.Stats.IsAlive && enemy.Stats.Hp == 60, "Spawned enemy starts at full health");
        var weapon = enemy.GetComponentInChildren<EquippedWeaponVisual>();
        Check(weapon.GetComponent<SpriteRenderer>().sprite != null, "Enemy equips the existing sword and binds its visual");
        playerReceiver.Invincible = false;
        int beforeHit = player.Stats.Hp;
        ai.enabled = true;
        float timeout = Time.time + 2f;
        while (ai.Phase != NpcDashMeleeController.AttackPhase.Warning && Time.time < timeout) yield return null;
        Check(ai.Phase == NpcDashMeleeController.AttackPhase.Warning, "Enemy begins its warning");
        float warningStarted = Time.fixedTime;
        Vector3 warningPosition = enemy.transform.position;
        Check(enemy.transform.Find("Aim Pivot").GetComponent<SpriteRenderer>().color == Color.yellow, "Enemy visibly turns yellow during the warning");
        yield return new WaitForSeconds(0.4f);
        Check(ai.Phase == NpcDashMeleeController.AttackPhase.Warning && Vector3.Distance(warningPosition, enemy.transform.position) < 0.05f,
            "Enemy holds still through the warning");
        while (ai.Phase == NpcDashMeleeController.AttackPhase.Warning) yield return null;
        Check(Time.fixedTime - warningStarted >= 0.48f, "Warning lasts approximately 0.5 seconds before the dash");
        yield return new WaitForSeconds(0.8f);
        File.AppendAllText(Report, $"INFO: enemy={enemy.transform.position}, player={player.transform.position}, hp={player.Stats.Hp}, phase={ai.Phase}\n");
        Check(player.Stats.Hp < beforeHit, "Dash ends with a sword-contact hit that damages a vulnerable player");
        ai.enabled = false;
        Check(ai.Phase == NpcDashMeleeController.AttackPhase.Approach, "Disabling AI clears its attack state and warning");
        playerReceiver.Invincible = true;
        int protectedHp = player.Stats.Hp;
        playerReceiver.ReceiveHit(new DamageInfo(10, enemy.gameObject));
        Check(player.Stats.Hp == protectedHp, "Invincibility still blocks incoming damage");

        // Kill through the player's real animated blade contact, not direct HP mutation.
        Place(player, new Vector2(70, 0));
        player.transform.Find("PlayerVisual").localRotation = Quaternion.identity;
        enemy.GetComponent<Rigidbody2D>().position = new Vector2(70, 1.1f);
        enemy.transform.position = new Vector3(70, 1.1f, 0);
        player.GetComponent<EquipmentManager>().Equip(EquipSlotType.Weapon, ItemDatabase.Instance.Get("iron_sword"));
        Physics2D.SyncTransforms();
        yield return new WaitForSeconds(0.2f);
        for (int swing = 0; swing < 6 && spawner.ActiveEnemy != null; swing++)
        {
            player.GetComponent<CombatAttacker>().TryAttack();
            yield return new WaitForSeconds(0.65f);
        }
        Check(spawner.ActiveEnemy == null, "Player sword kills the enemy and clears the occupied spawn slot");
        yield return null;
        Check(!FindObjectsByType<NpcController>(FindObjectsSortMode.None).Any(n => n.NpcId == "training_challenger"), "Defeated runtime body is removed");
        Place(player, new Vector2(74, 0));
        for (int round = 0; round < 3; round++)
        {
            Check(spawner.TrySpawn(), "Repeat round " + (round + 1) + " spawns successfully");
            yield return null;
            var opponent = spawner.ActiveEnemy;
            Check(opponent.Stats.Hp == 60, "Repeat round starts with fresh health");
            opponent.CombatReceiver.ReceiveHit(new DamageInfo(999, player.gameObject));
            yield return null;
        }

        // A physical obstacle must stop the full dash body, with the player on its far side.
        Check(spawner.TrySpawn(), "Wall-collision opponent spawns");
        enemy = spawner.ActiveEnemy;
        var obstacle = new GameObject("Verification wall", typeof(BoxCollider2D));
        obstacle.transform.position = new Vector3(72, 0, 0);
        obstacle.GetComponent<BoxCollider2D>().size = new Vector2(0.5f, 4f);
        Physics2D.SyncTransforms();
        yield return new WaitForSeconds(1.5f);
        Check(enemy.transform.position.x < 71.4f, "Dash body stops before a solid obstacle");
        Destroy(obstacle);
        enemy.CombatReceiver.ReceiveHit(new DamageInfo(999, player.gameObject));
        yield return null;
        Check(spawner.TrySpawn(), "Enemy can spawn after the wall test");
        enemy = spawner.ActiveEnemy;
        ai = enemy.GetComponent<NpcDashMeleeController>();
        timeout = Time.time + 2f;
        while (ai.Phase != NpcDashMeleeController.AttackPhase.Warning && Time.time < timeout) yield return null;
        enemy.CombatReceiver.ReceiveHit(new DamageInfo(999, player.gameObject));
        yield return new WaitForSeconds(0.7f);
        Check(spawner.ActiveEnemy == null, "Death during the warning cancels the dash and sword attack");
        Check(spawner.TrySpawn(), "Box remains reusable after a warning-phase kill");
        Place(player, new Vector2(50, 0));
        yield return new WaitForSeconds(0.2f);
        Check(spawner.ActiveEnemy.GetComponent<NpcDashMeleeController>().Phase == NpcDashMeleeController.AttackPhase.Approach,
            "Enemy stops attacking when the player leaves the arena");
    }
    private static void CheckWall(bool present, Vector3Int cell)
    {
        if (!present) throw new InvalidOperationException("Missing exterior wall at " + cell);
    }
}
#endif
