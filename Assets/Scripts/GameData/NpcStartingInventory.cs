using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// Plain data definition of one NPC's starting inventory, parsed from npc_inventories.json.
    /// The JSON loading and <see cref="IItem"/> resolution stay in the Shell; this DTO is the
    /// engine-free shape so the seed policy can be reasoned about without a scene.
    ///
    /// Unity setup: none. Created by JsonUtility inside NpcInventoryDatabase.
    /// </summary>
    [Serializable]
    public sealed class NpcStartingInventory
    {
        public string npcId;
        public List<NpcStartingItem> items = new List<NpcStartingItem>();
    }

    /// <summary>One item stack in an NPC's starting inventory. Unity setup: none.</summary>
    [Serializable]
    public sealed class NpcStartingItem
    {
        public string itemId;
        public int quantity = 1;
    }
}
