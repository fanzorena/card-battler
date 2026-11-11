using System.Collections.Generic;
using System.Linq;
using CardBattler.Combat;
using NUnit.Framework;

public class DossierTests
{
    Content content;

    [SetUp]
    public void SetUp() => content = Content.Load();

    [Test]
    public void EveryNonEasyEnemy_HasADossierPointingAtRealBuilds()
    {
        var presets = content.presets.Select(p => p.id).ToList();
        foreach (var e in content.robots.Where(r => !string.IsNullOrEmpty(r.tier) && r.tier != "Fácil"))
        {
            var d = e.dossier;
            Assert.IsTrue(d.Exists, e.id);
            Assert.IsNotEmpty(d.worksWell, e.id);
            Assert.IsNotEmpty(d.avoid, e.id);
            Assert.IsNotEmpty(d.goodBuilds, e.id);
            CollectionAssert.IsSubsetOf(d.goodBuilds.Concat(d.badBuilds), presets, e.id);
            CollectionAssert.IsEmpty(d.goodBuilds.Intersect(d.badBuilds), e.id);
        }
        Assert.IsFalse(content.Robot("enemy_sucata").dossier.Exists); // easy enemies need none
    }

    [Test]
    public void Describe_UsesPlainWords_AndMentionsBossPhases()
    {
        StringAssert.Contains("mira na sua parte mais valiosa", content.Describe(content.Robot("enemy_kiro")));
        StringAssert.Contains("muda de fase 2x", content.Describe(content.Robot("boss_mandibula")));
        StringAssert.Contains("improviso", content.Describe(content.Robot("enemy_sucata")));
        Assert.AreEqual("Bastion", content.MainMaker(content.Robot("boss_mandibula")));
    }

    static MatchupResult R(string build, int wins) => new MatchupResult
        { build = build, buildId = build, enemy = "E", enemyId = "e", policy = "greedy", matches = 100, wins = wins };

    [Test]
    public void DossierWarnings_FlagGoodBuildsBelowTheMiddle_AndBadBuildsAbove()
    {
        var rs = new List<MatchupResult> { R("a", 90), R("b", 70), R("c", 50), R("d", 30) };
        var ok = new DossierDef { rumors = new[] { "x" }, goodBuilds = new[] { "a", "b" }, badBuilds = new[] { "d" } };
        Assert.IsEmpty(Simulator.DossierWarnings(rs, _ => ok, out var lines));
        StringAssert.Contains("de acordo", lines.Single());

        var wrong = new DossierDef { rumors = new[] { "x" }, goodBuilds = new[] { "c" }, badBuilds = new[] { "a" } };
        Assert.AreEqual(2, Simulator.DossierWarnings(rs, _ => wrong, out _).Count);
    }

    // Keeps the written hints honest: fails if a balance change makes a dossier wrong.
    [Test]
    public void Dossiers_AgreeWithSimulatorResults()
    {
        var enemies = content.robots.Where(r => r.dossier.Exists).Select(r => r.id).ToArray();
        var results = Simulator.RunAll(content, new SimConfig { matches = 500, seed = 1, policies = new[] { "greedy" }, strategyImpact = false, enemies = enemies });
        var warnings = Simulator.DossierWarnings(results, id => content.Robot(id).dossier, out _);
        Assert.IsEmpty(warnings, string.Join("\n", warnings));
    }
}
