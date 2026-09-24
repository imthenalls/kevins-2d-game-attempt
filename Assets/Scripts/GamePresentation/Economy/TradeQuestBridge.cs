using Game.Core;
using UnityEngine;

/// <summary>
/// Shell bridge that turns a successful Core trade into the Unity-side "TradeCompleted" quest event.
/// Created automatically before any scene loads, so quest objectives keep working now that
/// <see cref="TradeService"/> lives in Game.Data and cannot raise Unity events itself.
///
/// Unity setup: none — created automatically. Do not add to a scene.
///
/// Runtime API: none.
/// </summary>
[DisallowMultipleComponent]
public sealed class TradeQuestBridge : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureExists()
    {
        var go = new GameObject("Trade Quest Bridge");
        DontDestroyOnLoad(go);
        go.AddComponent<TradeQuestBridge>();
    }

    private void OnEnable() => TradeService.OnTradeCompleted += HandleTradeCompleted;

    private void OnDisable() => TradeService.OnTradeCompleted -= HandleTradeCompleted;

    private static void HandleTradeCompleted(MarketTransaction transaction)
    {
        if (transaction == null)
            return;

        QuestEventBus.Raise("TradeCompleted", transaction.itemId, transaction.quantity);
    }
}
