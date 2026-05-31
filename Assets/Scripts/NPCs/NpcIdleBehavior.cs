using UnityEngine;

/// <summary>
/// NPC behavior that makes the NPC stand still for a random duration.
/// Completes when the timer runs out, at which point NpcBehaviorManager picks the next behavior.
///
/// Unity setup:
///   1. Add to an NPC GameObject alongside NpcBehaviorManager.
///   2. Set Min Duration and Max Duration (seconds the NPC stands still).
///   3. Set Weight (0–100) to control how often idle is chosen vs other behaviors.
/// </summary>
public class NpcIdleBehavior : MonoBehaviour, INpcBehavior
{
    [SerializeField, Range(0f, 100f)] private float weight = 50f;
    [SerializeField, Min(0f)] private float minDuration = 2f;
    [SerializeField, Min(0f)] private float maxDuration = 5f;

    public float Weight => weight;

    private float _timer;
    private float _duration;

    public void OnEnter()
    {
        _duration = Random.Range(minDuration, maxDuration);
        _timer = 0f;
    }

    public void Tick()
    {
        _timer += Time.deltaTime;
    }

    public void OnExit() { }

    public bool IsComplete() => _timer >= _duration;
}
