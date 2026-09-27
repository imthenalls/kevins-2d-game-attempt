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

    [Header("Config (Game.Data)")]
    [SerializeField] private NpcMeleeEngagementConfig config = new NpcMeleeEngagementConfig();

    private NpcChaseNavigator navigator;

    public bool IsEngaged { get; private set; }

    private void Awake()
    {
        if (npcController == null)
            npcController = GetComponent<NpcController>();
        if (body == null)
            body = GetComponent<Rigidbody>();
        if (attacker == null)
            attacker = GetComponent<CombatAttacker>();
        navigator = GetComponent<NpcChaseNavigator>();
    }

    private void OnValidate()
    {
        config.ChaseSpeed = Mathf.Max(0f, config.ChaseSpeed);
        config.DisengageRangeMultiplier = Mathf.Max(1f, config.DisengageRangeMultiplier);
        config.RepathInterval = Mathf.Max(0.05f, config.RepathInterval);
    }

    private void LateUpdate()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerControllerBase>();

        bool hasTarget = player != null && player.Stats != null && player.Stats.IsAlive;
        if (!hasTarget || npcController == null || attacker == null || !attacker.isActiveAndEnabled)
        {
            LeaveCombatState();
            return;
        }

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        // The engage/blocked/chase/attack/disengage decision lives in Game.Core (engine-free, shared
        // with 2D); this component only applies the returned intent.
        MeleeEngagement decision = MeleeEngagementPolicy.Evaluate(
            distance, attacker.AttackRange, config.DisengageRangeMultiplier,
            targetAlive: true, npcController.BehaviorState);

        switch (decision)
        {
            case MeleeEngagement.Idle:
            case MeleeEngagement.Disengage:
                LeaveCombatState();
                return;

            case MeleeEngagement.Blocked:
                StopMoving();
                IsEngaged = false;
                return;

            case MeleeEngagement.Chase:
                IsEngaged = true;
                npcController.SetBehaviorState(NpcBehaviorState.Combat);
                // Route around obstacles instead of pressing straight into them.
                Vector3 step = navigator != null
                    ? navigator.TryGetStepDirection(player.transform.position, transform.position, config.RepathInterval)
                    : (distance > 0.0001f ? toPlayer / distance : Vector3.zero);
                body.linearVelocity = step * config.ChaseSpeed;
                return;

            default: // Attack
                IsEngaged = true;
                npcController.SetBehaviorState(NpcBehaviorState.Combat);
                StopMoving();
                attacker.TryAttack();
                return;
        }
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
