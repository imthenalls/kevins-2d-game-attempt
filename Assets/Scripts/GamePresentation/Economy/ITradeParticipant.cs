/// <summary>
/// Exposes the stable identity, mana account, and inventory required by TradeService.
///
/// Unity setup: none. Implement this interface on player, NPC, settlement, or organization
/// controllers that can own goods and mana. PlayerController2D and NpcController provide
/// the built-in implementations.
///
/// Runtime API:
///   Pass an ITradeParticipant as TradeRequest.buyer or TradeRequest.seller.
/// </summary>
public interface ITradeParticipant
{
    string TradeParticipantId { get; }
    Wallet TradeWallet { get; }
    InventoryModel TradeInventory { get; }
}
