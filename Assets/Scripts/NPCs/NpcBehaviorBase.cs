using UnityEngine;

/// <summary>
/// Shared base class for NPC behaviors driven by NpcBehaviorManager. It caches the pieces every
/// behavior needs (Rigidbody2D, NpcController, NpcPerception, NpcMemory, IKeyHolder, and a facing
/// SpriteRenderer) and provides movement, facing, arrival, and stall helpers so individual
/// behaviors stay small.
///
/// Unity setup:
///   1. Derive from this class instead of implementing INpcBehavior directly.
///   2. Add the derived component to an NPC GameObject alongside NpcBehaviorManager.
///   3. Requires a Rigidbody2D (Gravity Scale = 0, freeze Z rotation).
///   4. Override Enter / TickBehavior / Exit and call Complete() when the behavior is done.
///   NpcPerception and NpcMemory are used when present; add NpcPerception to share scan results.
///
/// Runtime API (protected, for derived behaviors):
///   Body, Controller, Perception, Memory, KeyHolder, IsDone.
///   MoveToward(target, speed), StopMoving(), Face(dir), Arrived(target, threshold), Complete().
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class NpcBehaviorBase : MonoBehaviour, INpcBehavior
{
    [SerializeField, Range(0f, 100f)] protected float weight = 50f;
    [SerializeField, Min(0.1f)] protected float moveSpeed = 2f;
    [Tooltip("Seconds without progress before OnStalled fires.")]
    [SerializeField, Min(0.1f)] protected float stallTimeout = 0.5f;
    [SerializeField] protected SpriteRenderer spriteRenderer;

    protected Rigidbody2D Body { get; private set; }
    protected NpcController Controller { get; private set; }
    protected NpcPerception Perception { get; private set; }
    protected NpcMemory Memory { get; private set; }
    protected NpcPathfinder Pathfinder { get; private set; }
    protected IKeyHolder KeyHolder { get; private set; }
    protected bool IsDone { get; private set; }

    private Vector2 lastProgressPosition;
    private float stalledTime;

    private const float ProgressDistance = 0.01f;

    public float Weight => weight;

    protected virtual void Awake()
    {
        Body = GetComponent<Rigidbody2D>();
        Controller = GetComponent<NpcController>();
        Perception = GetComponent<NpcPerception>();
        Memory = GetComponent<NpcMemory>();
        Pathfinder = GetComponent<NpcPathfinder>();
        KeyHolder = GetComponentInParent<IKeyHolder>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>Ensures an NpcMemory exists on this NPC and returns it.</summary>
    protected NpcMemory EnsureMemory()
    {
        if (Memory == null)
            Memory = gameObject.AddComponent<NpcMemory>();
        return Memory;
    }

    public void OnEnter()
    {
        IsDone = false;
        stalledTime = 0f;
        lastProgressPosition = Body != null ? Body.position : (Vector2)transform.position;
        Enter();
    }

    public void Tick()
    {
        if (IsDone)
            return;

        TickBehavior();

        if (!IsDone && Body != null && UpdateStall(Body.position))
            OnStalled();
    }

    public void OnExit()
    {
        StopMoving();
        Exit();
    }

    public bool IsComplete() => IsDone;

    // ---- Derived hooks -------------------------------------------------

    /// <summary>Called once when the behavior starts.</summary>
    protected virtual void Enter() { }

    /// <summary>Called every frame while active. Call Complete() when finished.</summary>
    protected abstract void TickBehavior();

    /// <summary>Called once when the behavior ends.</summary>
    protected virtual void Exit() { }

    /// <summary>Called when the NPC stops making progress. Defaults to completing the behavior.</summary>
    protected virtual void OnStalled() => Complete();

    // ---- Helpers -------------------------------------------------------

    /// <summary>Marks the behavior complete; the manager will pick the next one.</summary>
    protected void Complete() => IsDone = true;

    /// <summary>Drives the Rigidbody2D toward a world position and faces the travel direction.</summary>
    protected void MoveToward(Vector2 target, float speed)
    {
        if (Body == null)
            return;

        Vector2 delta = target - Body.position;
        float distance = delta.magnitude;
        if (distance < 0.0001f)
        {
            StopMoving();
            return;
        }

        Vector2 direction = delta / distance;
        Face(direction);
        Body.linearVelocity = direction * speed;
    }

    protected void StopMoving()
    {
        if (Body != null)
            Body.linearVelocity = Vector2.zero;
    }

    protected bool Arrived(Vector2 target, float threshold) =>
        Body != null && Vector2.Distance(Body.position, target) <= threshold;

    /// <summary>Flips the facing sprite toward the given direction.</summary>
    protected void Face(Vector2 direction)
    {
        if (spriteRenderer != null)
            spriteRenderer.flipX = direction.x < 0f;
    }

    private bool UpdateStall(Vector2 position)
    {
        if ((position - lastProgressPosition).sqrMagnitude >= ProgressDistance * ProgressDistance)
        {
            lastProgressPosition = position;
            stalledTime = 0f;
            return false;
        }

        stalledTime += Time.deltaTime;
        return stalledTime >= stallTimeout;
    }
}
