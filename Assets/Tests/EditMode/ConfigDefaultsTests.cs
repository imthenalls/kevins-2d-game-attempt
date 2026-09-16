using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Guards the default values of every Game.Data config class. These defaults are the values a
    /// freshly added component (or a component whose legacy fields were migrated) falls back to, so
    /// accidental drift here silently changes gameplay. Runs in Edit Mode with no scene.
    ///
    /// Unity setup: none. Run via the Test Runner (EditMode) or `unity command run_tests`/`dotnet test`.
    /// </summary>
    public class ConfigDefaultsTests
    {
        [Test]
        public void PlayerMovementConfig_Defaults()
        {
            var c = new PlayerMovementConfig();
            Assert.AreEqual(6f, c.MoveSpeed);
            Assert.AreEqual(90f, c.SpriteForwardAngle);
            Assert.AreEqual(3, c.MaxDashCharges);
            Assert.AreEqual(5f, c.DashDistanceInPlayerLengths);
            Assert.AreEqual(6f, c.DashSpeedMultiplier);
            Assert.AreEqual(304, c.LegacyDashKeyCode); // LeftShift
            Assert.AreEqual(1f, c.DefaultPlayerLength);
        }

        [Test]
        public void EntityStatsConfig_Defaults()
        {
            var c = new EntityStatsConfig();
            Assert.AreEqual(100, c.MaxHp);
            Assert.AreEqual(100, c.StartingHp);
            Assert.AreEqual(50, c.MaxMp);
            Assert.AreEqual(50, c.StartingMp);
        }

        [Test]
        public void CombatAttackerConfig_Defaults()
        {
            var c = new CombatAttackerConfig();
            Assert.AreEqual(10, c.AttackDamage);
            Assert.AreEqual(1.5f, c.AttackRange);
            Assert.AreEqual(0.5f, c.AttackCooldown);
            Assert.AreEqual(0.3f, c.AttackDuration);
            Assert.IsTrue(c.UsePlayerInput);
            Assert.AreEqual(32, c.LegacyAttackKeyCode); // Space
        }

        [Test]
        public void NpcControllerConfig_Defaults()
        {
            var c = new NpcControllerConfig();
            Assert.AreEqual(30, c.EnemyMaxHp);
            Assert.AreEqual(3f, c.AggroRange);
            Assert.AreEqual(1.5f, c.InteractionRange);
            Assert.AreEqual(3, c.InventoryRows);
            Assert.AreEqual(4, c.InventoryColumns);
            Assert.AreEqual(72f, c.HealthBarScreenWidth);
        }

        [Test]
        public void NpcBehaviorConfigs_Defaults()
        {
            var b = new NpcBehaviorConfig();
            Assert.AreEqual(50f, b.Weight);
            Assert.AreEqual(2f, b.MoveSpeed);
            Assert.AreEqual(0.5f, b.StallTimeout);

            var w = new NpcWanderConfig();
            Assert.AreEqual(3f, w.WanderRadius);
            Assert.AreEqual(0.2f, w.ArrivalThreshold);
            Assert.AreEqual(0.3f, w.WallLookAhead);

            var i = new NpcIdleConfig();
            Assert.AreEqual(2f, i.MinDuration);
            Assert.AreEqual(5f, i.MaxDuration);

            var d = new NpcUseDoorConfig();
            Assert.AreEqual(6f, d.DetectionRadius);
            Assert.AreEqual(0.2f, d.WaypointThreshold);
            Assert.AreEqual(2f, d.PassThroughDistance);

            var m = new NpcDashMeleeConfig();
            Assert.AreEqual(0.5f, m.WarningDuration);
            Assert.AreEqual(2.8f, m.ApproachSpeed);
            Assert.AreEqual(6f, m.DashRange);
            Assert.AreEqual(14f, m.DashSpeed);
        }

        [Test]
        public void SlidingDoorConfig_Defaults()
        {
            var c = new SlidingDoorConfig();
            Assert.AreEqual(DoorAxis.GridX, c.Axis);
            Assert.AreEqual(2, c.CellLength);
            Assert.AreEqual(0.45f, c.SlideDuration);
            Assert.AreEqual(1f, c.InteractionRange);
            Assert.AreEqual("golden_key", c.RequiredKeyId);
            Assert.IsTrue(c.CloseAfterPassing);
        }

        [Test]
        public void PortalConfigs_Defaults()
        {
            var t = new PortalTriggerConfig();
            Assert.AreEqual(0.2f, t.TravelCooldown);
            Assert.AreEqual("Player", t.RequiredTag);

            var m = new PortalManagerConfig();
            Assert.AreEqual("Player", m.DefaultTravelerTag);
            Assert.AreEqual(0.2f, m.TravelerCooldownSeconds);
            Assert.IsTrue(m.ResetVelocityOnTeleport);
        }

        [Test]
        public void PlayerInteractionConfig_Defaults()
        {
            var c = new PlayerInteractionConfig();
            Assert.AreEqual(2f, c.InteractionSearchRadius);
            Assert.AreEqual(101, c.LegacyInteractKeyCode); // E
            Assert.AreEqual(32, c.LegacyAdvanceKeyCode);   // Space
        }

        [Test]
        public void WeaponAndEquipmentConfigs_Defaults()
        {
            var w = new WeaponVisualConfig();
            Assert.AreEqual(-20f, w.StartAngleOffset);
            Assert.AreEqual(200f, w.EndAngleOffset);
            Assert.AreEqual(1.25f, w.AttackRadiusMultiplier);

            var e = new EquipmentLoadoutConfig();
            Assert.AreEqual(string.Empty, e.StartingWeaponItemId);
            Assert.AreEqual(string.Empty, e.StartingArmorItemId);
            Assert.AreEqual(string.Empty, e.StartingAccessoryItemId);
        }

        [Test]
        public void WorldObjectConfigs_Defaults()
        {
            var i = new ItemPickupConfig();
            Assert.AreEqual(1, i.Quantity);

            var l = new LootContainerConfig();
            Assert.AreEqual(1.5f, l.InteractionRange);
            Assert.AreEqual(2, l.InventoryRows);
            Assert.AreEqual(4, l.InventoryColumns);

            var o = new WorldObjectConfig();
            Assert.AreEqual(1.5f, o.InteractionRange);
            Assert.AreEqual(1, o.RewardQuantity);
            Assert.IsTrue(o.OneTimeOnly);
        }

        [Test]
        public void Enums_Have_Expected_Members()
        {
            Assert.AreEqual(2, System.Enum.GetValues(typeof(DoorAxis)).Length);
            Assert.AreEqual(4, System.Enum.GetValues(typeof(GateUseResult)).Length);
        }
    }
}
