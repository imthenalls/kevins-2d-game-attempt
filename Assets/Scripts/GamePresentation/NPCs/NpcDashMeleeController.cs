using Game.Core;
using UnityEngine;

/// <summary>
/// Thin Unity facade over the engine-free <see cref="NpcDashMeleeModel"/>. Drives a melee enemy
/// through approach, a visible warning (body flashes), a straight dash, a sword swing, and recovery.
/// The phase machine lives in Game.Data; this component only samples geometry, aims the sprite,
/// moves the body safely (casting its collider so walls stop the dash), and applies the tint/attack.
///
/// Unity setup:
///   1. Attach to an Enemy NpcController with a zero-gravity Rigidbody2D, solid Collider2D,
///      and CombatAttacker with Use Player Input off. Do not add another movement AI.
///   2. Assign Body Collider, Body Renderer, and an Aim Pivot containing the body and weapon.
///   3. The spawner sets Arena Bounds via SetArenaBounds.
///
/// Runtime API: Phase reports the current attack phase (from the model).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NpcController), typeof(Rigidbody2D), typeof(CombatAttacker))]
public sealed class NpcDashMeleeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController2D player;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Transform aimPivot;

    [Header("Config (Game.Data)")]
    [SerializeField] private NpcDashMeleeConfig config = new NpcDashMeleeConfig();

    [Header("Movement Limits (Unity)")]
    [SerializeField] private LayerMask obstacleLayers = ~0;

    // Pure config flattened into Unity types where needed.
    private Color WarningColor => new Color(config.WarningR, config.WarningG, config.WarningB, config.WarningA);
    private Rect ArenaBounds => new Rect(config.ArenaX, config.ArenaY, config.ArenaWidth, config.ArenaHeight);

    private NpcController npc;
    private Rigidbody2D body;
    private CombatAttacker attacker;
    private NpcDashMeleeModel model;
    private Color restingColor;
    private readonly RaycastHit2D[] hits = new RaycastHit2D[32];

    /// <summary>Current attack phase, owned by the Core model.</summary>
    public NpcDashPhase Phase => model != null ? model.Phase : NpcDashPhase.Approach;

    public float WarningDuration => config.WarningDuration;

    private void Awake()
    {
        npc = GetComponent<NpcController>();
        body = GetComponent<Rigidbody2D>();
        attacker = GetComponent<CombatAttacker>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyRenderer != null) restingColor = bodyRenderer.color;

        model = new NpcDashMeleeModel(config);
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public void SetArenaBounds(Rect bounds)
    {
        config.ArenaX = bounds.x;
        config.ArenaY = bounds.y;
        config.ArenaWidth = bounds.width;
        config.ArenaHeight = bounds.height;
    }

    private void FixedUpdate()
    {
        if (player == null) player = FindAnyObjectByType<PlayerController2D>();
        if (npc.Stats == null || !npc.Stats.IsAlive || npc.BehaviorState == NpcBehaviorState.Disabled ||
            npc.BehaviorState == NpcBehaviorState.Talking || !attacker.isActiveAndEnabled ||
            player == null || player.Stats == null || !player.Stats.IsAlive ||
            !ArenaBounds.Contains(player.transform.position))
        {
            ResetAttack();
            return;
        }

        npc.SetBehaviorState(NpcBehaviorState.Combat);
        body.linearVelocity = Vector2.zero;

        Vector2 toPlayer = (Vector2)player.transform.position - body.position;
        float distance = toPlayer.magnitude;
        float directionX = distance > 0.001f ? toPlayer.x / distance : 0f;
        float directionY = distance > 0.001f ? toPlayer.y / distance : 0f;

        NpcDashDecision decision = model.Tick(
            Time.fixedDeltaTime, distance, npc.AggroRange, directionX, directionY,
            attacker.AttackDuration, attacker.isActiveAndEnabled);

        // Aim only while lining up; the dash direction is committed by the model.
        if (model.Phase == NpcDashPhase.Approach || model.Phase == NpcDashPhase.Warning)
            Aim(toPlayer);

        if (decision.WarningActive)
        {
            if (bodyRenderer != null) bodyRenderer.color = WarningColor;
        }
        else
        {
            RestoreColor();
        }

        Vector2 direction = new Vector2(decision.DirectionX, decision.DirectionZ);

        switch (decision.Intent)
        {
            case NpcDashIntent.Approach:
                MoveSafely(direction, decision.Distance);
                break;

            case NpcDashIntent.Dash:
                float moved = MoveSafely(direction, decision.Distance);
                model.ReportDashMoved(moved, decision.Distance);
                break;

            case NpcDashIntent.Attack:
                attacker.TryAttack();
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
        next.x = Mathf.Clamp(next.x, ArenaBounds.xMin + half.x, ArenaBounds.xMax - half.x);
        next.y = Mathf.Clamp(next.y, ArenaBounds.yMin + half.y, ArenaBounds.yMax - half.y);
        Vector2 delta = next - body.position;
        body.linearVelocity = delta / Time.fixedDeltaTime;
        return delta.magnitude;
    }

    private void Aim(Vector2 direction)
    {
        if (aimPivot != null && direction.sqrMagnitude > 0.001f)
            aimPivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
    }

    private void RestoreColor()
    {
        if (bodyRenderer != null) bodyRenderer.color = restingColor;
    }

    private void ResetAttack()
    {
        if (body != null) body.linearVelocity = Vector2.zero;
        model?.Reset();
        RestoreColor();
        if (npc != null && npc.BehaviorState == NpcBehaviorState.Combat)
            npc.SetBehaviorState(NpcBehaviorState.Idle);
    }

    private void OnDisable() => ResetAttack();
}
