using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// Engine-free tests for the authoritative per-character statistics model: accumulation,
    /// rejection of invalid negative values, change events, and save/load.
    ///
    /// Unity setup: none. Mirrored by `dotnet test`.
    /// </summary>
    public class CharacterStatisticsModelTests
    {
        [Test]
        public void Accumulates_Combat_And_Economy_Events()
        {
            var model = new CharacterStatisticsModel();

            model.RecordAttack(5);
            model.RecordAttack(7);
            model.RecordKill();
            model.RecordCriticalHit();
            model.RecordItemGathered(3);
            model.RecordMoneyGained(20);

            Assert.AreEqual(2, model.TotalAttacks);
            Assert.AreEqual(12, model.TotalDamageDealt);
            Assert.AreEqual(1, model.TotalKills);
            Assert.AreEqual(1, model.CriticalHits);
            Assert.AreEqual(3, model.TotalItemsGathered);
            Assert.AreEqual(20, model.TotalMoneyGained);
        }

        [Test]
        public void Negative_Additions_Are_Ignored()
        {
            var model = new CharacterStatisticsModel();
            model.RecordItemGathered(2);
            model.RecordMoneyGained(10);

            model.RecordItemGathered(-5);
            model.RecordMoneyGained(-1);

            Assert.AreEqual(2, model.TotalItemsGathered);
            Assert.AreEqual(10, model.TotalMoneyGained);
        }

        [Test]
        public void Attack_With_NonPositive_Damage_Still_Counts_The_Attack_But_Adds_No_Damage()
        {
            var model = new CharacterStatisticsModel();

            model.RecordAttack(-3);
            model.RecordAttack(0);

            Assert.AreEqual(2, model.TotalAttacks);
            Assert.AreEqual(0, model.TotalDamageDealt);
        }

        [Test]
        public void Change_Events_Report_The_New_Totals()
        {
            var model = new CharacterStatisticsModel();
            var attacks = new List<int>();
            var damage = new List<int>();
            var kills = new List<int>();
            var crits = new List<int>();
            var items = new List<int>();
            var money = new List<int>();

            model.AttacksChanged += attacks.Add;
            model.DamageDealtChanged += damage.Add;
            model.KillsChanged += kills.Add;
            model.CriticalHitsChanged += crits.Add;
            model.ItemsGatheredChanged += items.Add;
            model.MoneyGainedChanged += money.Add;

            model.RecordAttack(4);
            model.RecordKill();
            model.RecordCriticalHit();
            model.RecordItemGathered(2);
            model.RecordMoneyGained(9);

            CollectionAssert.AreEqual(new[] { 1 }, attacks);
            CollectionAssert.AreEqual(new[] { 4 }, damage);
            CollectionAssert.AreEqual(new[] { 1 }, kills);
            CollectionAssert.AreEqual(new[] { 1 }, crits);
            CollectionAssert.AreEqual(new[] { 2 }, items);
            CollectionAssert.AreEqual(new[] { 9 }, money);
        }

        [Test]
        public void Negative_Additions_Do_Not_Raise_Events()
        {
            var model = new CharacterStatisticsModel();
            int events = 0;
            model.ItemsGatheredChanged += _ => events++;
            model.MoneyGainedChanged += _ => events++;
            model.DamageDealtChanged += _ => events++;

            model.RecordItemGathered(-1);
            model.RecordMoneyGained(-1);
            model.RecordAttack(-1); // attack event fires, but not damage

            Assert.AreEqual(0, events);
        }

        [Test]
        public void Player_Statistics_Survive_A_Save_And_Restart_Through_SaveData()
        {
            var repository = new CharacterStatisticsRepository();
            CharacterStatisticsModel player = repository.GetOrCreate("player");
            player.RecordAttack(9);
            player.RecordItemGathered(4);
            player.RecordMoneyGained(15);

            var data = new SaveData();
            data.playerStatistics = player.GetSnapshot();

            // A restart rebuilds the session from the save and rehydrates the same "player" bucket.
            var restarted = new CharacterStatisticsRepository().GetOrCreate("player");
            restarted.Load(data.playerStatistics);

            Assert.AreEqual(1, restarted.TotalAttacks);
            Assert.AreEqual(9, restarted.TotalDamageDealt);
            Assert.AreEqual(4, restarted.TotalItemsGathered);
            Assert.AreEqual(15, restarted.TotalMoneyGained);
        }

        [Test]
        public void Snapshot_Round_Trips_Through_Save()
        {
            var model = new CharacterStatisticsModel();
            model.RecordAttack(6);
            model.RecordKill();
            model.RecordCriticalHit();
            model.RecordItemGathered(4);
            model.RecordMoneyGained(30);

            CharacterStatisticsSnapshot snapshot = model.GetSnapshot();

            var restored = new CharacterStatisticsModel();
            restored.Load(snapshot);

            Assert.AreEqual(1, restored.TotalAttacks);
            Assert.AreEqual(6, restored.TotalDamageDealt);
            Assert.AreEqual(1, restored.TotalKills);
            Assert.AreEqual(1, restored.CriticalHits);
            Assert.AreEqual(4, restored.TotalItemsGathered);
            Assert.AreEqual(30, restored.TotalMoneyGained);
        }

        [Test]
        public void Load_Treats_Missing_Or_Negative_Values_As_Zero()
        {
            var model = new CharacterStatisticsModel();
            model.Load(new CharacterStatisticsSnapshot { totalAttacks = -5, totalDamageDealt = 3 });
            Assert.AreEqual(0, model.TotalAttacks);
            Assert.AreEqual(3, model.TotalDamageDealt);

            model.Load(null);
            Assert.AreEqual(0, model.TotalDamageDealt);
        }

        [Test]
        public void Repository_Returns_The_Same_Model_Per_Id()
        {
            var repository = new CharacterStatisticsRepository();

            CharacterStatisticsModel player = repository.GetOrCreate("player");
            player.RecordKill();

            Assert.AreSame(player, repository.GetOrCreate("player"));
            Assert.AreSame(player, repository.GetOrCreate("  player  "));
            Assert.AreNotSame(player, repository.GetOrCreate("npc-1"));
            Assert.IsTrue(repository.TryGet("player", out CharacterStatisticsModel found));
            Assert.AreSame(player, found);
        }
    }
}
