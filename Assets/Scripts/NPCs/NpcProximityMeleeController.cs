using UnityEngine;

/// <summary>
/// Suspends normal NPC behaviors and repeatedly starts melee attacks while a living player
/// is inside CombatAttacker range. It stops Rigidbody2D movement and faces the NPC sprite
/// toward the player before each swing.
///
/// Unity setup:
///   1. Add to an NPC root with NpcController, Rigidbody2D, and CombatAttacker.
///   2. Disable Use Player Input on CombatAttacker.
///   3. Assign Player, NPC Controller, Rigidbody, Attacker, and Body Renderer, or leave
///      references empty for automatic discovery.
///   4. Add NpcBehaviorManager + NpcWanderBehavior for movement outside melee range.
///
/// Runtime API:
///   IsEngaged reports whether the player is currently inside melee range.
/// </summary>
[RequireComponent(typeof(NpcController))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CombatAttacker))]
[DisallowMultipleComponent]
public class NpcProximityMeleeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController2D player;
    [SerializeField] private NpcController npcController;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private CombatAttacker attacker;
    [SerializeField] private SpriteRenderer bodyRenderer;

    public bool IsEngaged { get; private set; }

    private void Awake()
    {
        if (npcController == null)
            npcController = GetComponent<NpcController>();
        if (body == null)
            body = GetComponent<Rigidbody2D>();
        if (attacker == null)
            attacker = GetComponent<CombatAttacker>();
        if (bodyRenderer == null)
            bodyRenderer = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (player == null)
            player = FindAnyObjectByType<PlayerController2D>();

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

        Vector2 toPlayer = player.transform.position - transform.position;
        float range = attacker.AttackRange;
        if (toPlayer.sqrMagnitude > range * range)
        {
            LeaveCombatState();
            return;
        }

        IsEngaged = true;
        npcController.SetBehaviorState(NpcBehaviorState.Combat);
        StopMoving();

        if (bodyRenderer != null && Mathf.Abs(toPlayer.x) > 0.01f)
            bodyRenderer.flipX = toPlayer.x < 0f;

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
            body.linearVelocity = Vector2.zero;
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
