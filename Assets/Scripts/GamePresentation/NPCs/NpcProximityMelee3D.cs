using Game.Core;
using UnityEngine;

/// <summary>
/// 3D counterpart of <see cref="NpcProximityMeleeController"/> for planar-isometric scenes. Chases a
/// living player on the XZ plane until inside <see cref="CombatAttacker"/> range, then stops and
/// swings. While engaged it holds the NPC in <see cref="NpcBehaviorState.Combat"/> so the wanderer
/// pauses; when the player leaves it returns the NPC to Idle and wandering resumes.
///
/// Unity setup:
///   1. Add to an enemy NPC root with NpcController, Rigidbody, CapsuleCollider and CombatAttacker.
///   2. Disable Use Player Input on the CombatAttacker.
///   3. Add NpcWander3D so the enemy wanders when the player is not nearby.
///   4. Leave references empty for automatic discovery.
///
/// Runtime API: IsEngaged reports whether the player is currently being chased/attacked.
/// </summary>
[RequireComponent(typeof(NpcController))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CombatAttacker))]
[DisallowMultipleComponent]
public class NpcProximityMelee3D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerControllerBase player;
    [SerializeField] private NpcController npcController;
    [SerializeField] private Rigidbody body;
    [SerializeField] private CombatAttacker attacker;

    [Tooltip("Movement speed while closing on the player.")]
    [SerializeField, Min(0f)] private float chaseSpeed = 2.6f;

    [Tooltip("Give up and resume wandering once the player is this many attack ranges away.")]
    [SerializeField, Min(1f)] private float disengageRangeMultiplier = 4f;

    public bool IsEngaged { get; private set; }

    private void Awake()
    {
        if (npcController == null)
            npcController = GetComponent<NpcController>();
        if (body == null)
            body = GetComponent<Rigidbody>();
        if (attacker == null)
            attacker = GetComponent<CombatAttacker>();
    }

    private void LateUpdate()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerControllerBase>();

        if (player == null || player.Stats == null || !player.Stats.IsAlive ||
            npcController == null || attacker == null || !attacker.isActiveAndEnabled)
        {
            LeaveCombatState();
            return;
        }

        NpcBehaviorState state = npcController.BehaviorState;
        if (state == NpcBehaviorState.Talking || state == NpcBehaviorState.Disabled)
        {
            StopMoving();
            IsEngaged = false;
            return;
        }

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        float range = attacker.AttackRange;

        // The engage/chase/attack decision lives in Game.Core (engine-free, shared with 2D).
        MeleeEngagement decision = MeleeEngagementPolicy.Evaluate(distance, range, disengageRangeMultiplier);

        if (decision == MeleeEngagement.Disengage)
        {
            LeaveCombatState();
            return;
        }

        IsEngaged = true;
        npcController.SetBehaviorState(NpcBehaviorState.Combat);

        if (decision == MeleeEngagement.Chase)
        {
            body.linearVelocity = distance > 0.0001f
                ? toPlayer / distance * chaseSpeed
                : Vector3.zero;
            return;
        }

        StopMoving();
        attacker.TryAttack();
    }

    private void OnDisable()
    {
        LeaveCombatState();
        StopMoving();
    }

    private void LeaveCombatState()
    {
        IsEngaged = false;
        if (npcController != null && npcController.BehaviorState == NpcBehaviorState.Combat)
            npcController.SetBehaviorState(NpcBehaviorState.Idle);
    }

    private void StopMoving()
    {
        if (body != null)
            body.linearVelocity = Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        CombatAttacker source = attacker != null ? attacker : GetComponent<CombatAttacker>();
        if (source == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, source.AttackRange);
    }
}
