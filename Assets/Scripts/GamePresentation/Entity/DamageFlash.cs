using Game.Core;
using UnityEngine;

/// <summary>
/// Flashes an entity's body sprite when it takes a hit, giving instant hit confirmation (a white
/// flash on a struck enemy) and damage feedback (a red flash on the player). Restores the resting
/// color after a short duration.
///
/// Unity setup:
///   1. Add to a GameObject that has a CombatReceiver.
///   2. Assign Body Renderer to the sprite that should flash (defaults to the first child renderer).
///   3. Tune Flash Color and Flash Duration.
///
/// Runtime API: none.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CombatReceiver))]
public sealed class DamageFlash : MonoBehaviour
{
    [Tooltip("The body sprite to flash. Defaults to the first SpriteRenderer in children.")]
    [SerializeField] private SpriteRenderer bodyRenderer;

    [Tooltip("Color the body flashes to on hit.")]
    [SerializeField] private Color flashColor = Color.white;

    [Tooltip("How long the flash lasts (seconds).")]
    [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;

    private CombatReceiver receiver;
    private Color restingColor;
    private float flashUntil = -1f;
    private bool subscribed;

    private void Awake()
    {
        receiver = GetComponent<CombatReceiver>();
        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyRenderer != null)
            restingColor = bodyRenderer.color;
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Update()
    {
        if (flashUntil < 0f || Time.time < flashUntil || bodyRenderer == null)
            return;

        flashUntil = -1f;
        bodyRenderer.color = restingColor;
    }

    private void HandleHit(DamageInfo _, EntityStats __)
    {
        if (bodyRenderer == null)
            return;

        bodyRenderer.color = flashColor;
        flashUntil = Time.time + flashDuration;
    }

    private void Subscribe()
    {
        if (subscribed || receiver == null)
            return;
        subscribed = true;
        receiver.OnHit += HandleHit;
    }

    private void Unsubscribe()
    {
        if (!subscribed || receiver == null)
            return;
        subscribed = false;
        receiver.OnHit -= HandleHit;
    }
}
