using UnityEngine;

/// <summary>
/// Drives a melee enemy through approach, a visible warning, a straight dash, sword swing,
/// and recovery. Sweeps its body collider before moving so walls stop even fast dashes.
/// Unity setup:
///   1. Attach to an Enemy NpcController with a zero-gravity Rigidbody2D, solid Collider2D,
///      and CombatAttacker with Use Player Input off. Do not add another movement AI.
///   2. Assign Body Collider, Body Renderer, and an Aim Pivot containing the body and
///      EquippedWeaponVisual. The pivot's unrotated sword arc points up.
///   3. Configure warning duration/color, approach speed, dash range/speed, stopping
///      distance, recovery, obstacle layers, and world-space Arena Bounds.
///   4. Player is optional; it is found automatically. The spawner sets Arena Bounds.
/// Runtime API: Phase reports attack state; SetArenaBounds limits pursuit to the room.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NpcController), typeof(Rigidbody2D), typeof(CombatAttacker))]
public sealed class NpcDashMeleeController : MonoBehaviour
{
    public enum AttackPhase { Approach, Warning, Dash, Swing, Recovery }

    [Header("References")]
    [SerializeField] private PlayerController2D player;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform aimPivot;
    [Header("Attack")]
    [SerializeField, Min(0.01f)] private float warningDuration = 0.5f;
    [SerializeField] private Color warningColor = Color.yellow;
    [SerializeField, Min(0.1f)] private float approachSpeed = 2.8f;
    [SerializeField, Min(0.1f)] private float dashRange = 6f;
    [SerializeField, Min(0.1f)] private float dashSpeed = 14f;
    [SerializeField, Min(0.1f)] private float stoppingDistance = 1.1f;
    [SerializeField, Min(0.1f)] private float recoveryDuration = 1.1f;
    [Header("Movement Limits")]
    [SerializeField] private LayerMask obstacleLayers = ~0;
    [SerializeField] private Rect arenaBounds = new Rect(61f, -7f, 18f, 14f);

    private NpcController npc;
    private Rigidbody2D body;
    private CombatAttacker attacker;
    private Color restingColor;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
    private Vector2 dashDirection;
    private float dashRemaining;
    private float phaseTime;
    public AttackPhase Phase { get; private set; }
    public float WarningDuration => warningDuration;

    private void Awake()
    {
        npc = GetComponent<NpcController>();
        body = GetComponent<Rigidbody2D>();
        attacker = GetComponent<CombatAttacker>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyRenderer != null) restingColor = bodyRenderer.color;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public void SetArenaBounds(Rect bounds) => arenaBounds = bounds;

    private void FixedUpdate()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController2D>();
        if (npc.Stats == null || !npc.Stats.IsAlive || npc.BehaviorState == NpcBehaviorState.Disabled ||
            npc.BehaviorState == NpcBehaviorState.Talking || !attacker.isActiveAndEnabled ||
            player == null || player.Stats == null || !player.Stats.IsAlive ||
            !arenaBounds.Contains(player.transform.position))
        {
            ResetAttack();
            return;
        }

        npc.SetBehaviorState(NpcBehaviorState.Combat);
        body.linearVelocity = Vector2.zero;
        phaseTime += Time.fixedDeltaTime;
        Vector2 toPlayer = (Vector2)player.transform.position - body.position;
        switch (Phase)
        {
            case AttackPhase.Approach:
                Aim(toPlayer);
                if (toPlayer.magnitude <= dashRange)
                {
                    Enter(AttackPhase.Warning);
                    if (bodyRenderer != null) bodyRenderer.color = warningColor;
                }
                else if (toPlayer.magnitude <= npc.AggroRange)
                    MoveSafely(toPlayer.normalized, approachSpeed * Time.fixedDeltaTime);
                break;
            case AttackPhase.Warning:
                Aim(toPlayer);
                if (phaseTime + 0.0001f >= warningDuration)
                {
                    // Aim is committed here; dodging afterward does not steer the dash.
                    dashDirection = toPlayer.normalized;
                    dashRemaining = Mathf.Clamp(toPlayer.magnitude - stoppingDistance, 0f, dashRange);
                    RestoreColor();
                    Enter(AttackPhase.Dash);
                }
                break;
            case AttackPhase.Dash:
                float step = Mathf.Min(dashRemaining, dashSpeed * Time.fixedDeltaTime);
                float moved = MoveSafely(dashDirection, step);
                dashRemaining -= moved;
                if (dashRemaining <= 0.01f || moved + 0.001f < step)
                {
                    // Swing on the following physics tick, after the final movement.
                    Enter(AttackPhase.Swing);
                }
                break;
            case AttackPhase.Swing:
                if (phaseTime <= Time.fixedDeltaTime + 0.0001f) attacker.TryAttack();
                if (phaseTime >= attacker.AttackDuration + Time.fixedDeltaTime)
                    Enter(AttackPhase.Recovery);
                break;
            case AttackPhase.Recovery:
                if (phaseTime >= recoveryDuration) Enter(AttackPhase.Approach);
                break;
        }
    }

    private float MoveSafely(Vector2 direction, float distance)
    {
        if (bodyCollider == null || !bodyCollider.enabled || distance <= 0f) return 0f;
        const float skin = 0.03f;
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = obstacleLayers, useTriggers = false };
        int count = bodyCollider.Cast(direction, filter, hits, distance + skin);
        float permitted = distance;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider == null || hits[i].collider.attachedRigidbody == body) continue;
            permitted = Mathf.Min(permitted, Mathf.Max(0f, hits[i].distance - skin));
        }
        Vector2 next = body.position + direction * permitted;
        Vector2 half = bodyCollider.bounds.extents;
        next.x = Mathf.Clamp(next.x, arenaBounds.xMin + half.x, arenaBounds.xMax - half.x);
        next.y = Mathf.Clamp(next.y, arenaBounds.yMin + half.y, arenaBounds.yMax - half.y);
        Vector2 delta = next - body.position;
        body.linearVelocity = delta / Time.fixedDeltaTime;
        return delta.magnitude;
    }

    private void Aim(Vector2 direction)
    {
        if (aimPivot != null && direction.sqrMagnitude > 0.001f)
            aimPivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
    }

    private void Enter(AttackPhase phase) { Phase = phase; phaseTime = 0f; }
    private void RestoreColor() { if (bodyRenderer != null) bodyRenderer.color = restingColor; }
    private void ResetAttack()
    {
        if (body != null) body.linearVelocity = Vector2.zero;
        RestoreColor();
        Enter(AttackPhase.Approach);
        if (npc != null && npc.BehaviorState == NpcBehaviorState.Combat)
            npc.SetBehaviorState(NpcBehaviorState.Idle);
    }
    private void OnDisable() => ResetAttack();
}
