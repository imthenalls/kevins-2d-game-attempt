namespace Game.Core
{
    /// <summary>
    /// Engine-free trade participant: stable id, mana account, and inventory. Keeping this contract
    /// in Game.Data lets <see cref="TradeService"/> live entirely in Core; the Unity controllers
    /// implement it by exposing their <c>Wallet.AccountModel</c>.
    ///
    /// Unity setup: none — this is an interface. Implemented by PlayerControllerBase and NpcController.
    ///
    /// Runtime API: pass an ITradeParticipant as TradeRequest.buyer or TradeRequest.seller.
    /// </summary>
    public interface ITradeParticipant
    {
        string TradeParticipantId { get; }
        ManaAccount TradeWallet { get; }
        InventoryModel TradeInventory { get; }
    }
}
