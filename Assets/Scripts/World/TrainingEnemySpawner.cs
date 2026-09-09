using UnityEngine;

/// <summary>
/// Lets the player talk to a green box to summon one repeatable training enemy. Cancelling
/// the conversation does not summon; a defeated enemy is cleaned up before the next round.
/// Unity setup:
///   1. Attach to a green SpriteRenderer box with a Collider2D on an interactable layer.
///   2. Assign Enemy Prefab (active Enemy NPC with NpcDashMeleeController), Spawn Point,
///      and world-space Arena Bounds. Set interaction range and the two dialogue lines.
///   3. Keep the spawn point clear of the box and walls. No NPC dialogue component is needed.
/// Runtime API: TrySpawn summons if the room is clear and no living enemy remains;
/// ActiveEnemy reports the current opponent. IInteractable drives the conversation.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class TrainingEnemySpawner : MonoBehaviour, IInteractable
{
    [SerializeField] private NpcController enemyPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Rect arenaBounds = new Rect(61f, -7f, 18f, 14f);
    [SerializeField, Min(0.25f)] private float interactionRange = 2f;
    [SerializeField] private string readyLine = "Another challenger awaits.";
    [SerializeField] private string busyLine = "Your challenger is still standing.";
    private bool confirmed;
    private NpcController activeEnemy;
    private readonly Collider2D[] overlaps = new Collider2D[32];
    public NpcController ActiveEnemy => activeEnemy;

    public bool CanInteract(Vector3 position) => isActiveAndEnabled &&
        Vector2.Distance(position, transform.position) <= interactionRange;
    public string GetDisplayName() => "Green Box";
    public bool TryGetCurrentLine(out string line)
    {
        line = activeEnemy != null && activeEnemy.Stats != null && activeEnemy.Stats.IsAlive ? busyLine : readyLine;
        return !confirmed;
    }
    public void Advance() => confirmed = true;
    public void EndInteraction(GameObject interactor)
    {
        bool summon = confirmed;
        confirmed = false;
        if (summon && interactor != null && CanInteract(interactor.transform.position)) TrySpawn();
    }

    public bool TrySpawn()
    {
        if (!isActiveAndEnabled || enemyPrefab == null || spawnPoint == null || activeEnemy != null) return false;
        // A one-unit square body plus clearance; try nearby positions if the player occupies the marker.
        Vector2 origin = spawnPoint.position;
        Vector2[] offsets = { Vector2.zero, Vector2.right * 2f, Vector2.left * 2f, Vector2.up * 2f, Vector2.down * 2f };
        foreach (Vector2 offset in offsets)
        {
            Vector2 point = origin + offset;
            if (!arenaBounds.Contains(point - Vector2.one * 0.6f) ||
                !arenaBounds.Contains(point + Vector2.one * 0.6f)) continue;
            var filter = new ContactFilter2D { useTriggers = false };
            if (Physics2D.OverlapBox(point, Vector2.one * 1.2f, 0f, filter, overlaps) > 0) continue;
            activeEnemy = Instantiate(enemyPrefab, point, Quaternion.identity);
            activeEnemy.name = "Training Challenger";
            activeEnemy.GetComponent<NpcDashMeleeController>().SetArenaBounds(arenaBounds);
            activeEnemy.CombatReceiver.OnDeath += HandleEnemyDeath;
            return true;
        }
        return false;
    }

    private void HandleEnemyDeath(CombatReceiver receiver)
    {
        receiver.OnDeath -= HandleEnemyDeath;
        activeEnemy = null;
        // Stop attack coroutines and hitboxes immediately; remove the defeated runtime object.
        receiver.gameObject.SetActive(false);
        Destroy(receiver.gameObject);
    }

    private void OnDestroy()
    {
        if (activeEnemy == null) return;
        if (activeEnemy.CombatReceiver != null) activeEnemy.CombatReceiver.OnDeath -= HandleEnemyDeath;
        Destroy(activeEnemy.gameObject);
    }
}
