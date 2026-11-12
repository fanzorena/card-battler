using System.IO;
using System.Linq;
using CardBattler.Combat;
using CardBattler.UI;
using NUnit.Framework;
using UnityEngine;

// Distrito Neon: unlock by the first clue, Circuito Regional ladder in the rain, Glitch and the second clue.
public class NeonTests
{
    Content c;
    [SetUp] public void SetUp() => c = Content.Load();

    Battle Ended(Career k, Result r, string ladder)
    {
        var b = k.StartBattle(c, 1, k.LadderStage(c, ladder).enemy, Career.ArenaOf(ladder));
        b.result = r;
        return b;
    }

    [Test]
    public void Content_CircuitEnemiesAndClue()
    {
        Assert.AreEqual(3, c.circuit.Length);
        Assert.AreEqual("enemy_kiro_revanche", c.circuit.Last().enemy, "o topo é a revanche do Kiro");
        foreach (var st in c.circuit)
        {
            var r = c.Robot(st.enemy);
            foreach (var id in r.PartIds) c.Part(id); // catalog parts only (enemies may exceed the carry limit, like the original Kiro)
            Assert.AreEqual("neon_chuva", r.terrain, r.id);
        }
        Assert.DoesNotThrow(() => c.Clue(Career.ClueSale));
        foreach (var id in new[] { "neon_open", "glitch_first", "glitch_wait", "glitch_clue", "glitch_done" }) Assert.IsNotEmpty(c.Dialogue(id).lines, id);
    }

    [Test]
    public void FirstClue_OpensTheNeon_CircuitFightsInTheRain()
    {
        var k = Career.New(c);
        Assert.IsFalse(k.IsOpen("neon"));
        k.LearnClue(c, Career.ClueRigged);
        Assert.IsTrue(k.IsOpen("neon"));
        Assert.AreEqual("neon_chuva", k.StartBattle(c, 1, k.LadderStage(c, "circuit").enemy, Career.ArenaOf("circuit")).terrain.id);
    }

    [Test]
    public void Circuit_WinClimbsLossDrops_PitUntouched_GlitchAfterTwoWins()
    {
        var k = Career.New(c);
        k.FinishLadderFight(c, Ended(k, Result.Win, "circuit"), "circuit");
        Assert.AreEqual(1, k.circuitRank);
        Assert.AreEqual(c.circuit[0].credits, k.credits);
        Assert.AreEqual(0, k.pitRank);
        Assert.IsFalse(k.GlitchReady);
        int credits = k.credits;
        k.FinishLadderFight(c, Ended(k, Result.Lose, "circuit"), "circuit");
        Assert.AreEqual(0, k.circuitRank);
        Assert.AreEqual(credits, k.credits, "derrota só custa a posição");
        for (int i = 0; i < 2; i++) { k.FinishLadderFight(c, Ended(k, Result.Win, "circuit"), "circuit"); k.damaged = new PartHp[0]; k.chassisDamage = 0; }
        Assert.IsTrue(k.GlitchReady);
        k.LearnClue(c, Career.ClueSale);
        Assert.IsFalse(k.GlitchReady);
        StringAssert.Contains("Circuito", k.lastCircuit);
    }

    [Test]
    public void SaveLoad_KeepsRegionAndCircuit()
    {
        var path = Path.Combine(Path.GetTempPath(), "cb_neon_" + System.Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var k = Career.New(c);
            k.LearnClue(c, Career.ClueRigged);
            k.region = "neon"; k.circuitRank = 2;
            k.Save(path);
            var l = Career.Load(path, c);
            Assert.AreEqual("neon", l.region);
            Assert.AreEqual(2, l.circuitRank);
            var k2 = Career.New(c);
            k2.region = "neon"; // region without the unlock goes back to the port
            k2.Save(path);
            Assert.AreEqual("porto", Career.Load(path, c).region);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Test]
    public void Flow_TrainGlitchCircuit()
    {
        var path = Path.Combine(Path.GetTempPath(), "cb_neon_" + System.Guid.NewGuid().ToString("N") + ".json");
        var go = new GameObject("UITest");
        try
        {
            var ui = go.AddComponent<GameUI>();
            ui.logBattles = false;
            ui.careerPath = path;
            ui.Init();
            ui.PlayCareer();
            ui.map.Open("neon");
            Assert.AreEqual("porto", ui.career.region, "trancado sem a pista");
            ui.career.LearnClue(ui.content, Career.ClueRigged);
            ui.ShowMap();
            ui.map.Open("neon");
            Assert.AreEqual("neon", ui.career.region);
            Assert.AreEqual("map", ui.screen);
            ui.map.Open("glitch");
            Assert.AreEqual(c.Dialogue("glitch_first").lines[0].text, ui.map.CurrentLine);
            ui.career.circuitRank = 2;
            ui.map.Glitch();
            int guard = 0;
            while (ui.map.CurrentChoices.Length == 0 && guard++ < 20) ui.map.Next();
            ui.map.Choose("Anotar nas pistas");
            CollectionAssert.Contains(ui.career.clues, Career.ClueSale);
            ui.map.Open("circuito");
            Assert.AreEqual("circuit", ui.screen);
            ui.Back();
            ui.map.Open("porto");
            Assert.AreEqual("porto", ui.career.region);
        }
        finally
        {
            Object.DestroyImmediate(go);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
