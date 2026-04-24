using UnityEngine;

/// <summary>
/// NPC stands still for a random duration between minDuration and maxDuration seconds.
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
