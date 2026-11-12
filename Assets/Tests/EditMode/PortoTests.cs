using System.IO;
using System.Linq;
using CardBattler.Combat;
using CardBattler.UI;
using NUnit.Framework;
using UnityEngine;

public class PortoTests
{
    Content c;
    [SetUp] public void SetUp() => c = Content.Load();

    static Battle Ended(Career k, Content c, Result r, string enemy)
    {
        var b = k.StartBattle(c, 1, enemy);
        b.result = r;
        return b;
    }

    [Test]
    public void Content_PitDialoguesAndCluesExist()
    {
        Assert.AreEqual(4, c.pit.Length);
        Assert.AreEqual("boss_mandibula", c.pit.Last().enemy, "o último degrau é o campeão");
        foreach (var st in c.pit) Assert.DoesNotThrow(() => c.Robot(st.enemy));
        foreach (var id in new[] { "celeste_first", "celeste_offer", "celeste_busy", "celeste_unlock", "bettor_clue", "oficina", "ferro_velho", "locked" })
            Assert.IsNotEmpty(c.Dialogue(id).lines, id);
        Assert.DoesNotThrow(() => c.Clue(Career.ClueRigged));
    }

    [Test]
    public void Unlocks_FirstJobWinOpensOficinaAndFosso()
    {
        var k = Career.New(c);
        Assert.IsTrue(k.IsOpen("bar"));
        Assert.IsFalse(k.IsOpen("oficina"));
        Assert.IsFalse(k.IsOpen("fosso"));
        k.StartJob(c, "celeste_fossos");
        k.FinishJobFight(c, Ended(k, c, Result.Lose, k.Stage(c).enemy), new System.Random(1));
        Assert.IsFalse(k.IsOpen("fosso"), "derrota não libera nada");
        k.StartJob(c, "celeste_fossos");
        k.FinishJobFight(c, Ended(k, c, Result.Win, k.Stage(c).enemy), new System.Random(1));
        Assert.IsTrue(k.IsOpen("oficina"));
        Assert.IsTrue(k.IsOpen("fosso"));
    }

    [Test]
    public void Pit_WinClimbsAndPays_LossCostsOnlyRanking()
    {
        var k = Career.New(c);
        k.credits = 50;
        var st = k.PitStage(c);
        k.FinishPitFight(c, Ended(k, c, Result.Win, st.enemy));
        Assert.AreEqual(1, k.pitRank);
        Assert.AreEqual(50 + st.credits, k.credits);
        Assert.AreEqual(st.scrap, k.scrap);

        int credits = k.credits, scrap = k.scrap, parts = k.owned.Length;
        var b = Ended(k, c, Result.Lose, k.PitStage(c).enemy);
        b.player.parts[Slot.Torso].hp -= 5;
        k.FinishPitFight(c, b);
        Assert.AreEqual(0, k.pitRank);
        Assert.AreEqual(credits, k.credits, "derrota no Fosso não cobra créditos");
        Assert.AreEqual(scrap, k.scrap);
        Assert.AreEqual(parts, k.owned.Length);
        Assert.AreEqual(0, k.MissingIntegrity(c), "o pessoal do Fosso remenda o robô");

        k.FinishPitFight(c, Ended(k, c, Result.Lose, k.PitStage(c).enemy));
        Assert.AreEqual(0, k.pitRank, "não desce abaixo de zero");
    }

    [Test]
    public void Champion_BettorTellsTheClueOnce()
    {
        var k = Career.New(c);
        for (int i = 0; i < c.pit.Length; i++)
        {
            Assert.IsFalse(k.BettorWaiting(c));
            k.FinishPitFight(c, Ended(k, c, Result.Win, k.PitStage(c).enemy));
            k.damaged = new PartHp[0]; k.chassisDamage = 0;
        }
        Assert.IsTrue(k.Champion(c));
        Assert.IsTrue(k.BettorWaiting(c));
        k.LearnClue(c, Career.ClueRigged);
        k.LearnClue(c, Career.ClueRigged);
        Assert.AreEqual(1, k.clues.Length);
        Assert.IsFalse(k.BettorWaiting(c));
        // Defending the title: a win keeps it.
        k.FinishPitFight(c, Ended(k, c, Result.Win, k.PitStage(c).enemy));
        Assert.AreEqual(c.pit.Length, k.pitRank);
    }

    [Test]
    public void SaveLoad_KeepsUnlocksRankCluesAndSeen()
    {
        var path = Path.Combine(Path.GetTempPath(), "cb_porto_" + System.Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var k = Career.New(c);
            k.Unlock("oficina"); k.Unlock("fosso");
            k.pitRank = 3;
            k.LearnClue(c, Career.ClueRigged);
            k.MarkSeen("celeste_first");
            k.Save(path);
            var l = Career.Load(path, c);
            Assert.IsTrue(l.IsOpen("fosso"));
            Assert.AreEqual(3, l.pitRank);
            CollectionAssert.AreEqual(new[] { Career.ClueRigged }, l.clues);
            Assert.IsTrue(l.Seen("celeste_first"));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Test]
    public void MapFlow_BarDialogueToJobAndPlaces()
    {
        var path = Path.Combine(Path.GetTempPath(), "cb_porto_" + System.Guid.NewGuid().ToString("N") + ".json");
        var go = new GameObject("UITest");
        try
        {
            var ui = go.AddComponent<GameUI>();
            ui.logBattles = false;
            ui.careerPath = path;
            ui.Init();
            ui.PlayCareer();
            Assert.AreEqual("map", ui.screen);
            var map = ui.map;
            Assert.IsFalse(map.IsOpen("oficina"));
            Assert.IsFalse(map.IsOpen("ferro"));

            map.Open("fosso"); // locked: someone tells you so, the screen doesn't change
            Assert.AreEqual("map", ui.screen);
            Assert.IsNotNull(map.CurrentLine);
            map.CloseTalk();

            map.Celeste();
            Assert.AreEqual(c.Dialogue("celeste_first").lines[0].text, map.CurrentLine);
            Assert.IsEmpty(map.CurrentChoices); // choices only after the last line
            int guard = 0;
            while (map.CurrentChoices.Length == 0 && guard++ < 20) map.Next();
            CollectionAssert.Contains(map.CurrentChoices, "Aceitar o trabalho");
            map.Choose("Rumores sobre a primeira luta");
            Assert.IsNotNull(map.CurrentLine);
            map.Celeste(); // already met: no intro this time
            Assert.AreEqual(c.Dialogue("celeste_offer").lines[0].text, map.CurrentLine);
            while (map.CurrentChoices.Length == 0 && guard++ < 40) map.Next();
            map.Choose("Aceitar o trabalho");
            Assert.AreEqual("job", ui.screen);
            Assert.IsTrue(ui.career.InJob);

            ui.StartJobFight();
            ui.battle.result = Result.Win;
            ui.afterBattle(ui.battle);
            ui.Back();
            Assert.AreEqual("map", ui.screen);
            Assert.IsTrue(ui.map.IsOpen("oficina") && ui.map.IsOpen("fosso"));

            ui.map.Open("oficina");
            Assert.AreEqual("workshop", ui.screen);
            ui.ShowGarage();
            ui.Back();
            Assert.AreEqual("workshop", ui.screen, "a garagem volta para onde foi aberta");
            ui.Back();
            ui.map.Open("fosso");
            Assert.AreEqual("pit", ui.screen);
            ui.Back();
            Assert.AreEqual("map", ui.screen);
            ui.Back();
            Assert.AreEqual("title", ui.screen);
        }
        finally
        {
            Object.DestroyImmediate(go);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
