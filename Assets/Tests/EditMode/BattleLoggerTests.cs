using System.IO;
using System.Linq;
using CardBattler.Combat;
using NUnit.Framework;
using UnityEngine;

public class BattleLoggerTests
{
    string dir;
    Content content;

    [SetUp]
    public void SetUp()
    {
        dir = Path.Combine(Path.GetTempPath(), "cb_logs_" + System.Guid.NewGuid().ToString("N"));
        content = Content.Load();
    }

    [TearDown]
    public void TearDown() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

    [Test]
    public void BattleStart_LogsBothRobotsWithDecks_AndFirstHand()
    {
        var b = new Battle(content, 3);
        var starts = b.history.Where(e => e.evt == "battle_start").ToList();
        Assert.AreEqual(new[] { "player", "enemy" }, starts.Select(e => e.actor).ToArray());
        Assert.AreEqual(20, starts[0].cards.Length);
        Assert.IsTrue(b.history.Any(e => e.evt == "intent_reveal" && e.actor == "enemy"));
        var hand = b.history.Last(e => e.evt == "hand");
        Assert.AreEqual(5, hand.cards.Length);
        Assert.AreEqual(1, hand.turn);
    }

    [Test]
    public void PlayAttack_LogsCostTargetAbsorbedAndSnapshots()
    {
        var b = new Battle(content, 3);
        b.player.energy = 5;
        b.enemy.armor = 2;
        var soco = new Card(content.Card("soco"), Slot.BracoEsq);
        b.player.hand.Add(soco);
        b.Play(b.player, soco);
        var play = b.history.Last(e => e.evt == "play");
        var atk = b.history.Last(e => e.evt == "attack");
        Assert.AreEqual(("Soco", "Braço Esq", 1), (play.card, play.source, play.cost));
        Assert.AreEqual(("Torso", 4, 2), (atk.target, atk.value, atk.absorbed));
        Assert.AreEqual(4, atk.player.energy);
        Assert.AreEqual(16, atk.enemy.torso);
    }

    [Test]
    public void Logger_WritesOneJsonLinePerEvent_AndSummaryOnBattleEnd()
    {
        var b = new Battle(content, 5);
        var logger = new BattleLogger(b, dir);
        b.enemy.parts[Slot.Torso].hp = 1;
        b.enemy.chassis = 0;
        var soco = new Card(content.Card("soco"), Slot.BracoEsq);
        b.player.hand.Add(soco);
        b.player.energy = 1;
        b.Play(b.player, soco);

        Assert.AreEqual(Result.Win, b.result);
        var lines = File.ReadAllLines(logger.jsonlPath);
        Assert.AreEqual(b.history.Count, lines.Length);
        var last = JsonUtility.FromJson<BattleEvent>(lines.Last());
        Assert.AreEqual("battle_end", last.evt);
        var summary = File.ReadAllText(logger.summaryPath);
        StringAssert.Contains("Vitória", summary);
        StringAssert.Contains("Soco: 1x", summary);
        StringAssert.Contains("Cartas nunca jogadas", summary);
    }

    [Test]
    public void EnemyTurn_LogsIntentExecution_StatusTicks_AndTurnEnd()
    {
        var b = new Battle(content, 9);
        b.player.parts[Slot.Cabeca].corrosion = 1;
        b.player.overload = 1;
        b.EndPlayerTurn();
        foreach (var evt in new[] { "turn_end", "intent_execute", "corrosion_tick", "overload_penalty" })
            Assert.IsTrue(b.history.Any(e => e.evt == evt), evt);
    }
}
