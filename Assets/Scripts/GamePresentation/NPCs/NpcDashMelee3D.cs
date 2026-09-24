using Game.Core;
using UnityEngine;

/// <summary>
/// Thin Unity facade over the engine-free <see cref="NpcDashMeleeModel"/>. This component only
/// samples the world (distance/direction, wall casts) and applies movement, the warning tint, and the
/// swing; the Approach → Warning → Dash → Swing → Recovery decision lives in Game.Data and is
/// unit-tested there.
///
/// Unity setup:
///   1. Add to an Enemy NpcController with a Rigidbody (no gravity, Y frozen), a CapsuleCollider,
///      and a CombatAttacker with Use Player Input off. Do not add another movement AI.
///   2. Assign Body Collider and Body Renderer (the billboard visual, for the warning flash).
///   3. Tune Warning/dash/recovery on the nested Npc Dash Melee config; the weapon rig aims itself.
///   4. Optionally set obstacles (Walls) and enable Arena Bounds to confine pursuit.
///
/// Runtime API: Phase reports the current attack phase (from the model).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NpcController), typeof(Rigidbody), typeof(CombatAttacker))]
public sealed class NpcDashMelee3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerControllerBase player;
    [SerializeField] private CapsuleCollider bodyCollider;
    [SerializeField] private SpriteRenderer bodyRenderer;

    [Header("Config (Game.Data)")]
    [SerializeField] private NpcDashMeleeConfig config = new NpcDashMeleeConfig();

    [Header("Movement Limits (Unity)")]
    [SerializeField] private LayerMask obstacleLayers = ~0;
    [SerializeField] private bool useArenaBounds;

    private Color WarningColor => new Color(config.WarningR, config.WarningG, config.WarningB, config.WarningA);

    private NpcController npc;
    private Rigidbody body;
    private CombatAttacker attacker;
    private NpcDashMeleeModel model;
    private Color restingColor;

    private static readonly RaycastHit[] Hits = new RaycastHit[32];

    /// <summary>Current attack phase, owned by the Core model.</summary>
    public NpcDashPhase Phase => model != null ? model.Phase : NpcDashPhase.Approach;

    public float WarningDuration => config.WarningDuration;

    private void Awake()
    {
        npc = GetComponent<NpcController>();
        body = GetComponent<Rigidbody>();
        attacker = GetComponent<CombatAttacker>();
        if (bodyCollider == null) bodyCollider = GetComponent<CapsuleCollider>();
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyRenderer != null) restingColor = bodyRenderer.color;

        model = new NpcDashMeleeModel(config);

        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    public void SetArenaBounds(Rect bounds)
    {
        config.ArenaX = bounds.x;
        config.ArenaY = bounds.y;
        config.ArenaWidth = bounds.width;
        config.ArenaHeight = bounds.height;
        useArenaBounds = true;
    }

    private void FixedUpdate()
    {
        if (player == null) player = FindAnyObjectByType<PlayerControllerBase>();

        if (npc.Stats == null || !npc.Stats.IsAlive || npc.BehaviorState == NpcBehaviorState.Disabled ||
            npc.BehaviorState == NpcBehaviorState.Talking || !attacker.isActiveAndEnabled ||
            player == null || player.Stats == null || !player.Stats.IsAlive)
        {
            ResetAttack();
            return;
        }

        Vector3 toPlayer = player.transform.position - body.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        // Give up and idle when the player leaves the aggro area.
        if (distance > npc.AggroRange * 1.5f)
        {
            ResetAttack();
            return;
        }

        npc.SetBehaviorState(NpcBehaviorState.Combat);
        body.linearVelocity = Vector3.zero;

        float directionX = distance > 0.001f ? toPlayer.x / distance : 0f;
        float directionZ = distance > 0.001f ? toPlayer.z / distance : 0f;

        NpcDashDecision decision = model.Tick(
            Time.fixedDeltaTime, distance, npc.AggroRange, directionX, directionZ,
            attacker.AttackDuration, attacker.isActiveAndEnabled);

        if (decision.WarningActive)
        {
            if (bodyRenderer != null) bodyRenderer.color = WarningColor;
        }
        else
        {
            RestoreColor();
        }

        Vector3 direction = new Vector3(decision.DirectionX, 0f, decision.DirectionZ);

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

    // Moves the body by up to distance, stopping at walls and (optionally) arena bounds.
    private float MoveSafely(Vector3 direction, float distance)
    {
        if (bodyCollider == null || !bodyCollider.enabled || distance <= 0f)
            return 0f;

        const float skin = 0.03f;
        float radius = Mathf.Max(0.05f, bodyCollider.radius * 0.95f);
        Vector3 origin = body.position + Vector3.up * bodyCollider.center.y;

        int count = Physics.SphereCastNonAlloc(
            origin, radius, direction, Hits, distance + skin, obstacleLayers, QueryTriggerInteraction.Ignore);

        float permitted = distance;
        for (int i = 0; i < count; i++)
        {
            if (Hits[i].collider == null || Hits[i].rigidbody == body)
                continue;
            permitted = Mathf.Min(permitted, Mathf.Max(0f, Hits[i].distance - skin));
        }

        Vector3 next = body.position + direction * permitted;
        Vector3 half = bodyCollider.bounds.extents;

        if (useArenaBounds)
        {
            next.x = Mathf.Clamp(next.x, config.ArenaX + half.x, config.ArenaX + config.ArenaWidth - half.x);
            next.z = Mathf.Clamp(next.z, config.ArenaY + half.z, config.ArenaY + config.ArenaHeight - half.z);
        }

        Vector3 delta = next - body.position;
        delta.y = 0f;
        body.linearVelocity = delta / Time.fixedDeltaTime;
        return delta.magnitude;
    }

    private void RestoreColor()
    {
        if (bodyRenderer != null) bodyRenderer.color = restingColor;
    }

    private void ResetAttack()
    {
        if (body != null) body.linearVelocity = Vector3.zero;
        model?.Reset();
        RestoreColor();
        if (npc != null && npc.BehaviorState == NpcBehaviorState.Combat)
            npc.SetBehaviorState(NpcBehaviorState.Idle);
    }

    private void OnDisable() => ResetAttack();
}
