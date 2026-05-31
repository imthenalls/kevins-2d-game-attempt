using UnityEngine;

/// <summary>
/// Lightweight value type that carries the details of one hit between attacker and target.
/// Passed from CombatAttacker into CombatReceiver.ReceiveHit() so the receiver knows
/// how much damage was dealt and who dealt it (for retaliation, kill credit, VFX, etc.).
///
/// Unity setup: none — this is a plain struct, not a MonoBehaviour.
///   Create one inline wherever a hit needs to be delivered:
///   receiver.ReceiveHit(new DamageInfo(25, gameObject));
///
/// To add attack types / elements later:
///   1. Define an AttackType enum.
///   2. Add a public AttackType Type field here.
///   3. Read it in CombatReceiver.ReceiveHit() for resistances, critical logic, etc.
/// </summary>
public struct DamageInfo
{
    /// <summary>Raw damage before any reductions.</summary>
    public int Amount;

    /// <summary>
    /// The GameObject that dealt this damage.
    /// May be null for environmental or trap damage.
    /// </summary>
    public GameObject Source;

    public DamageInfo(int amount, GameObject source = null)
    {
        Amount  = amount;
        Source  = source;
    }
}
