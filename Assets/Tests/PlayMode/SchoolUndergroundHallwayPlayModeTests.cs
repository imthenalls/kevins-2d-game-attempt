using System.Collections;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>
    /// Play Mode coverage of the locked Industrial Arts Shop route into the underground hallway and
    /// out to the town park: the physical 3D door, the key-holding NPC's branching gift, the
    /// same-scene hallway portals, and the one-way park arrival with no return route.
    /// </summary>
    public class SchoolUndergroundHallwayPlayModeTests : PlayModeTestBase
    {
        private const string TownScene = "Assets/Scenes/Town.unity";
        private const string WorkshopKey = "wood_shop_key";
        private const string AccessFlag = "school_workshop_access";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return LoadScene(TownScene);

            // Play Mode shares one session: reset the mutable slice these tests depend on.
            PlayerKeyring.GetOrCreate().Clear();
            WorldStateManager.Instance?.ClearFlag(AccessFlag);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Workshop_And_Hall_Portals_Are_Wired_In_Same_Scene()
        {
            PortalManager manager = PortalManager.Instance;
            Assert.IsNotNull(manager);

            Assert.IsTrue(manager.TryFindPortal("school_workshop_portal", out IPortalRoute toHall));
            Assert.IsTrue(string.IsNullOrEmpty(toHall.DestinationScene), "the workshop route stays in this scene.");
            Assert.AreEqual("hall_entrance", toHall.DestinationPortalId);
            Assert.IsFalse(toHall.ChangesWorld);

            Assert.IsTrue(manager.TryFindPortal("hall_entrance", out IPortalRoute backToWorkshop));
            Assert.IsTrue(string.IsNullOrEmpty(backToWorkshop.DestinationScene));
            Assert.AreEqual("school_workshop_portal", backToWorkshop.DestinationPortalId);

            Assert.IsTrue(manager.TryFindPortal("hall_exit", out IPortalRoute toPark));
            Assert.IsTrue(string.IsNullOrEmpty(toPark.DestinationScene));
            Assert.AreEqual("park_arrival", toPark.DestinationPortalId);

            Assert.IsTrue(manager.TryFindPortal("park_arrival", out IPortalRoute arrival));
            Assert.IsTrue(arrival.IsArrivalOnly, "the park destination must be arrival-only.");
            GameObject arrivalObject = arrival.Self.gameObject;
            Assert.IsNull(arrivalObject.GetComponent<Collider>(), "the park arrival must have no trigger collider.");
            Assert.IsNull(arrivalObject.GetComponentInChildren<Renderer>(), "the park arrival must not be visible.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dialogue_Data_Has_A_Correct_Branch_And_Wrong_Branches()
        {
            Assert.IsTrue(DialogueDatabase.TryGetDialogue("school_workshop_keeper", out DialogueGraphDefinition graph));

            DialogueNodeDefinition start = graph.nodes.Find(n => n.id == "start");
            Assert.IsNotNull(start);
            Assert.IsNotNull(start.choices);

            DialogueChoiceDefinition correct = start.choices.Find(c => !string.IsNullOrEmpty(c.setWorldFlag));
            Assert.IsNotNull(correct, "one branch must mark the successful answer.");
            Assert.AreEqual(AccessFlag, correct.setWorldFlag);
            Assert.AreEqual("reason_ok", correct.nextNodeId);

            int wrongBranches = 0;
            foreach (DialogueChoiceDefinition choice in start.choices)
            {
                if (choice == correct)
                    continue;
                Assert.IsTrue(string.IsNullOrEmpty(choice.setWorldFlag),
                    "wrong answers must not set the access flag.");
                wrongBranches++;
            }

            Assert.GreaterOrEqual(wrongBranches, 2, "expected at least two non-awarding responses.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Locked_Workshop_Door_Blocks_Until_The_Key_Is_Owned()
        {
            LockedDoor3D door = Object.FindAnyObjectByType<LockedDoor3D>();
            Assert.IsNotNull(door, "the Industrial Arts Shop must have a physical locked 3D door.");

            PlayerController3D player = FindPlayer();
            PlayerKeyring ring = PlayerKeyring.GetOrCreate();

            Assert.IsTrue(door.IsLocked, "with no key the door must be locked.");
            Assert.AreEqual(GateUseResult.Locked, door.TryUse(player.gameObject));
            Assert.IsFalse(door.IsOpen, "a locked door must stay closed.");
            Assert.IsTrue(BlockerEnabled(door), "the blocker must still block the doorway while locked.");

            ring.AddKey(GetItem(WorkshopKey), 1);
            Assert.AreEqual(GateUseResult.Opened, door.TryUse(player.gameObject));
            Assert.IsTrue(door.IsOpen, "with the key the door must open.");
            yield return new WaitForSeconds(0.5f);
            Assert.IsFalse(BlockerEnabled(door), "an open door must stop blocking the doorway.");
        }

        [UnityTest]
        public IEnumerator Workshop_Hall_RoundTrip_Keeps_The_Scene_And_World()
        {
            PortalManager manager = PortalManager.Instance;
            WorldTravelState travel = WorldTravelState.Instance;
            PlayerController3D player = FindPlayer();
            Assert.IsNotNull(manager);
            Assert.IsNotNull(travel);
            Assert.AreEqual(WorldLayer.WorldA, travel.CurrentWorld);

            int sceneHandle = SceneManager.GetActiveScene().handle;

            Assert.IsTrue(manager.TryUsePortal("school_workshop_portal", player.transform));
            yield return null;
            Assert.IsTrue(sceneHandle == SceneManager.GetActiveScene().handle, "the hallway is in the same scene.");
            Assert.AreEqual(WorldLayer.WorldA, travel.CurrentWorld);
            Assert.IsTrue(manager.TryFindPortal("hall_entrance", out IPortalRoute entrance));
            Assert.Less(Vector3.Distance(player.transform.position, entrance.ArrivalPosition), 1.5f);

            yield return WaitForCooldown();

            Assert.IsTrue(manager.TryUsePortal("hall_entrance", player.transform));
            yield return null;
            Assert.IsTrue(sceneHandle == SceneManager.GetActiveScene().handle);
            Assert.AreEqual(WorldLayer.WorldA, travel.CurrentWorld);
            Assert.IsTrue(manager.TryFindPortal("school_workshop_portal", out IPortalRoute workshop));
            Assert.Less(Vector3.Distance(player.transform.position, workshop.ArrivalPosition), 1.5f,
                "the round trip must land back at the workshop portal.");
        }

        [UnityTest]
        public IEnumerator Hall_Exit_Reaches_The_Park_With_No_Return_Route()
        {
            PortalManager manager = PortalManager.Instance;
            PlayerController3D player = FindPlayer();

            Assert.IsTrue(manager.TryUsePortal("hall_exit", player.transform));
            yield return null;

            Assert.IsTrue(manager.TryFindPortal("park_arrival", out IPortalRoute arrival));
            Assert.Less(Vector3.Distance(player.transform.position, arrival.ArrivalPosition), 1.5f,
                "the far end must send the player to the park arrival.");

            // No active travel trigger may sit on the park arrival, so walking over it is inert.
            foreach (PortalTrigger3D trigger in Object.FindObjectsByType<PortalTrigger3D>(FindObjectsInactive.Include))
            {
                float distance = Vector3.Distance(trigger.transform.position, arrival.ArrivalPosition);
                Assert.Greater(distance, 2f,
                    "park arrival must not overlap an active portal trigger; found '" + trigger.PortalId + "'.");
            }

            foreach (PortalTrigger2D trigger in Object.FindObjectsByType<PortalTrigger2D>(FindObjectsInactive.Include))
            {
                float distance = Vector3.Distance(trigger.transform.position, arrival.ArrivalPosition);
                Assert.Greater(distance, 2f,
                    "park arrival must not overlap an active 2D portal; found '" + trigger.PortalId + "'.");
            }
        }

        [UnityTest]
        public IEnumerator Keeper_Gift_Requires_The_Correct_Branch_And_Never_Duplicates()
        {
            NpcController keeper = FindNpc("school_workshop_keeper");
            Assert.IsNotNull(keeper, "the corridor key holder must exist.");
            NpcDialogue dialogue = keeper.GetComponent<NpcDialogue>();
            Assert.IsNotNull(dialogue);
            Assert.AreEqual("school_workshop_keeper", dialogue.DialogueId);

            ItemData key = GetItem(WorkshopKey);
            InventoryModel inventory = keeper.EnsureInventory();
            if (!inventory.HasItem(key, 1))
                inventory.AddItem(key, 1);

            PlayerKeyring ring = PlayerKeyring.GetOrCreate();
            PlayerController3D player = FindPlayer();
            ring.Clear();
            WorldStateManager.Instance.ClearFlag(AccessFlag);

            // Wrong answer / cancellation: the flag is unset, so nothing is awarded.
            Assert.AreEqual(0, dialogue.GiveInventoryGift(player.gameObject));
            Assert.IsFalse(ring.HasKey(WorkshopKey));

            // The successful branch sets the flag; the single key transfers.
            WorldStateManager.Instance.SetFlag(AccessFlag);
            Assert.AreEqual(1, dialogue.GiveInventoryGift(player.gameObject));
            Assert.IsTrue(ring.HasKey(WorkshopKey));

            // Repeating the conversation cannot duplicate the key.
            Assert.AreEqual(0, dialogue.GiveInventoryGift(player.gameObject));
            Assert.AreEqual(1, ring.CountKey(WorkshopKey));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Key_Gift_Succeeds_When_Ordinary_Inventory_Is_Full()
        {
            NpcController keeper = FindNpc("school_workshop_keeper");
            NpcDialogue dialogue = keeper.GetComponent<NpcDialogue>();
            ItemData key = GetItem(WorkshopKey);
            ItemData filler = GetItem("iron_ore");

            InventoryModel inventory = keeper.EnsureInventory();
            if (!inventory.HasItem(key, 1))
                inventory.AddItem(key, 1);

            PlayerKeyring.GetOrCreate().Clear();
            WorldStateManager.Instance.ClearFlag(AccessFlag);

            InventoryModel playerInventory = InventoryUI.Model;
            Assert.IsNotNull(playerInventory, "the boot layer supplies the player inventory.");
            for (int i = 0; i < playerInventory.SlotCount; i++)
                playerInventory.AddItem(filler, filler.maxStackSize);
            Assert.IsFalse(playerInventory.CanAddItem(filler, 1), "the ordinary inventory must be full.");

            WorldStateManager.Instance.SetFlag(AccessFlag);
            Assert.AreEqual(1, dialogue.GiveInventoryGift(FindPlayer().gameObject),
                "a key must route to the keyring even when the ordinary inventory is full.");
            Assert.IsTrue(PlayerKeyring.GetOrCreate().HasKey(WorkshopKey));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Consumed_Keeper_Key_Is_Not_Reseeded()
        {
            NpcController keeper = FindNpc("school_workshop_keeper");
            NpcDialogue dialogue = keeper.GetComponent<NpcDialogue>();
            ItemData key = GetItem(WorkshopKey);

            InventoryModel inventory = keeper.EnsureInventory();
            if (!inventory.HasItem(key, 1))
                inventory.AddItem(key, 1);

            PlayerKeyring.GetOrCreate().Clear();
            WorldStateManager.Instance.SetFlag(AccessFlag);
            Assert.AreEqual(1, dialogue.GiveInventoryGift(FindPlayer().gameObject));
            Assert.IsFalse(inventory.HasItem(key, 1), "the transfer empties the NPC of the key.");

            // A scene reload (or save restore) must not seed a replacement key.
            NpcInventoryDatabase.Instance.ApplyToLoadedScene();
            yield return null;
            Assert.IsFalse(keeper.Inventory.HasItem(key, 1),
                "an initialized NPC must never be reseeded after its key was legitimately taken.");
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static PlayerController3D FindPlayer() =>
            Object.FindAnyObjectByType<PlayerController3D>();

        private static NpcController FindNpc(string npcId)
        {
            foreach (NpcController npc in Object.FindObjectsByType<NpcController>(FindObjectsInactive.Exclude))
                if (npc.NpcId == npcId)
                    return npc;
            return null;
        }

        private static ItemData GetItem(string itemId)
        {
            Assert.IsTrue(ItemDatabase.Instance.TryGet(itemId, out ItemData item), "missing item '" + itemId + "'.");
            return item;
        }

        private static bool BlockerEnabled(LockedDoor3D door)
        {
            Transform blocker = door.transform.Find("DoorBlocker");
            if (blocker == null)
                return false;
            var collider = blocker.GetComponent<Collider>();
            return collider != null && collider.enabled;
        }

        private static IEnumerator WaitForCooldown()
        {
            yield return new WaitForSeconds(0.5f);
            yield return null;
        }

        private static IEnumerator LoadScene(string path)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            while (operation != null && !operation.isDone)
                yield return null;
            yield return null;
            yield return null;
        }
    }
}
