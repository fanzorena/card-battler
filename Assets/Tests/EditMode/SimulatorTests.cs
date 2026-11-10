using System;
using System.Linq;
using CardBattler.Combat;
using NUnit.Framework;

public class SimulatorTests
{
    Content content;

    [SetUp]
    public void SetUp() => content = Content.Load();

    [Test]
    public void SameSeed_SameResults()
    {
        var build = content.presets[0];
        foreach (var policy in new[] { "greedy", "random" })
        {
            var a = Simulator.Run(content, build, "enemy", policy, 20, 42);
            var b = Simulator.Run(content, build, "enemy", policy, 20, 42);
            Assert.AreEqual(a.wins, b.wins, policy);
            CollectionAssert.AreEqual(a.turns, b.turns, policy);
            Assert.AreEqual(a.dealt, b.dealt, policy);
            CollectionAssert.AreEqual(a.cards.Select(kv => kv.Key + kv.Value.played + "/" + kv.Value.damage),
                                      b.cards.Select(kv => kv.Key + kv.Value.played + "/" + kv.Value.damage), policy);
        }
    }

    [Test]
    public void Aggregate_CountsPlaysDamageEnergyAndDraws()
    {
        var battle = new Battle(content, 3);
        battle.player.hand.Clear();
        battle.enemy.armor = 0;
        var soco = new Card(content.Card("soco"), Slot.BracoEsq);
        battle.player.hand.Add(soco);
        battle.Play(battle.player, soco);

        var r = new MatchupResult();
        Simulator.Aggregate(r, battle);
        Assert.AreEqual((1, 0, 0, 1), (r.matches, r.wins, r.losses, r.draws)); // unfinished counts as a draw
        Assert.AreEqual(1, r.cards["Soco"].played);
        Assert.AreEqual(6, r.cards["Soco"].damage);
        Assert.AreEqual(6, r.dealt);
        Assert.AreEqual(1, r.energyUsed);
        Assert.AreEqual(3, r.energyGen);
        Assert.AreEqual(5, r.cards.Values.Sum(c => c.drawn)); // opening hand
        Assert.AreEqual(4, r.enemyCards.Values.Sum(c => c.drawn)); // enemy's first hand
    }

    [Test]
    public void Run_EveryMatchEnds_AndTotalsAddUp()
    {
        var r = Simulator.Run(content, content.presets[1], "enemy_nyx", "greedy", 30, 1);
        Assert.AreEqual(30, r.matches);
        Assert.AreEqual(30, r.wins + r.losses + r.draws);
        Assert.IsTrue(r.turns.All(t => t >= 1 && t <= Simulator.TurnLimit));
        Assert.Greater(r.cards.Values.Sum(c => c.played), 0);
    }

    [Test]
    public void Presets_AreValidBuilds_EnemiesExist()
    {
        foreach (var p in content.presets) Assert.IsEmpty(content.Validate(p), p.name);
        Assert.GreaterOrEqual(content.robots.Count(r => r.id.StartsWith("enemy")), 3);
    }

    [Test]
    public void RunAll_RejectsBadConfig()
    {
        Assert.Throws<ArgumentException>(() => Simulator.RunAll(content, new SimConfig { matches = 1, policies = new[] { "magic" } }));
        Assert.Throws<ArgumentException>(() => Simulator.RunAll(content, new SimConfig { matches = 1, enemies = new[] { "nao_existe" } }));
        var heavy = content.Robot("enemy").Clone(); // overweight for a player
        Assert.Throws<ArgumentException>(() => Simulator.RunAll(content, new SimConfig { matches = 1, builds = new[] { heavy } }));
    }
}
