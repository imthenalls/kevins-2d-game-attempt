using UnityEngine;

/// <summary>
/// Data describing a single hit. Pass to Combatant.ReceiveHit().
///
/// To add attack types / elements later:
///   1. Define an AttackType enum.
///   2. Add a public AttackType Type field here.
///   3. Read it in Combatant.ReceiveHit() for resistances, critical logic, etc.
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
