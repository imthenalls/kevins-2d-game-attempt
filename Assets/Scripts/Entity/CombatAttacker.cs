using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Melee attack component shared by the player and NPCs.
///
/// Player: enable Use Player Input — Update reads keyboard/gamepad and calls TryAttack().
/// NPC:    disable Use Player Input — an AI behavior script calls TryAttack() directly.
///
/// Set Target Layers to the layer(s) this entity is allowed to hit.
/// The attacker's own GameObject is always excluded from hits.
///
/// The attack range is visualised as a red wire circle in Scene view.
/// </summary>
[DisallowMultipleComponent]
public class CombatAttacker : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField, Min(1)]    private int       attackDamage   = 10;
    [SerializeField, Min(0.1f)] private float     attackRange    = 1.5f;
    [SerializeField, Min(0f)]   private float     attackCooldown = 0.5f;
    [SerializeField]            private LayerMask targetLayers   = Physics2D.DefaultRaycastLayers;

    [Header("Input")]
    [Tooltip("Enable for the player. Disable for NPCs driven by AI behavior scripts.")]
    [SerializeField] private bool    usePlayerInput  = true;
    [SerializeField] private KeyCode legacyAttackKey = KeyCode.Space;

    private readonly Collider2D[] _overlapResults = new Collider2D[16];
    private float _cooldownTimer;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        if (usePlayerInput && WasAttackPressedThisFrame())
            TryAttack();
    }

    // ── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Attempt an attack. Finds the nearest living Combatant within attackRange
    /// on targetLayers and calls ReceiveHit on it.
    /// No-op while the cooldown is active.
    /// Called automatically by Update when usePlayerInput is true.
    /// Call directly from AI behavior scripts when usePlayerInput is false.
    /// </summary>
    public void TryAttack()
    {
        if (_cooldownTimer > 0f) return;

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position, attackRange, _overlapResults, targetLayers);

        Combatant nearest        = null;
        float     nearestDistSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            // Walk up to parent in case the collider is on a child object.
            var combatant = _overlapResults[i].GetComponentInParent<Combatant>();

            if (combatant == null)                  continue;
            if (!combatant.Stats.IsAlive)            continue;
            if (combatant.gameObject == gameObject)  continue; // never hit self

            float distSqr = (combatant.transform.position - transform.position).sqrMagnitude;
            if (distSqr < nearestDistSqr)
            {
                nearestDistSqr = distSqr;
                nearest        = combatant;
            }
        }

        if (nearest == null) return;

        nearest.ReceiveHit(new DamageInfo(attackDamage, gameObject));
        _cooldownTimer = attackCooldown;
    }

    // ── Input ────────────────────────────────────────────────────────────────

    private bool WasAttackPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            return true;
        if (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame)
            return true;
        return false;
#else
        return Input.GetKeyDown(legacyAttackKey);
#endif
    }

    // ── Gizmos ───────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}

    [Header("Attack")]
    [SerializeField, Min(1)] private int   attackDamage   = 10;
    [SerializeField, Min(0.1f)] private float attackRange   = 1.5f;
    [SerializeField, Min(0f)]   private float attackCooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayers = Physics2D.DefaultRaycastLayers;

    [Header("Legacy Input Fallback")]
    [SerializeField] private KeyCode legacyAttackKey = KeyCode.Space;

    private readonly Collider2D[] _overlapResults = new Collider2D[16];
    private float _cooldownTimer;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    private void Update()
    {
        if (_cooldownTimer > 0f)
            _cooldownTimer -= Time.deltaTime;

        if (WasAttackPressedThisFrame())
            TryAttack();
    }

    // ── Attack ──────────────────────────────────────────────────────────────

    private void TryAttack()
    {
        if (_cooldownTimer > 0f) return;

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            transform.position, attackRange, _overlapResults, enemyLayers);

        Combatant nearest        = null;
        float     nearestDistSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            // Walk up to parent in case collider is on a child object.
            var combatant = _overlapResults[i].GetComponentInParent<Combatant>();

            if (combatant == null)                            continue;
            if (!combatant.Stats.IsAlive)                     continue;
            if (combatant.gameObject == gameObject)           continue; // never hit self

            float distSqr = (combatant.transform.position - transform.position).sqrMagnitude;
            if (distSqr < nearestDistSqr)
            {
                nearestDistSqr = distSqr;
                nearest        = combatant;
            }
        }

        if (nearest == null) return;

        nearest.ReceiveHit(new DamageInfo(attackDamage, gameObject));
        _cooldownTimer = attackCooldown;
    }

    // ── Input ────────────────────────────────────────────────────────────────

    private bool WasAttackPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            return true;
        if (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame)
            return true;
        return false;
#else
        return Input.GetKeyDown(legacyAttackKey);
#endif
    }

    // ── Gizmos ───────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
