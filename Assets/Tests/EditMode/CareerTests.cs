using System.IO;
using System.Linq;
using CardBattler.Combat;
using CardBattler.UI;
using NUnit.Framework;
using UnityEngine;

public class CareerTests
{
    Content c;
    const string Job = "celeste_fossos";

    [SetUp] public void SetUp() => c = Content.Load();

    [Test]
    public void New_OwnsOnlyTheStarter()
    {
        var k = Career.New(c);
        var starter = c.Robot("player");
        CollectionAssert.AreEquivalent(starter.PartIds, k.owned);
        CollectionAssert.AreEqual(new[] { starter.frame }, k.frames);
        Assert.IsEmpty(k.Validate(c));
        Assert.AreEqual(GarageSave.SlotCount, k.garage.slots.Length);
    }

    [Test]
    public void Validate_RejectsPartsAndFramesNotOwned()
    {
        var k = Career.New(c);
        var b = k.Build.Clone();
        b.head = c.PartsFor("Cabeca").First(p => !k.Owns(p)).id;
        Assert.IsTrue(k.Validate(c, b).Any(e => e.Contains("não tem a peça")));
        b = k.Build.Clone();
        b.frame = c.frames.First(f => !k.OwnsFrame(f)).id;
        Assert.IsTrue(k.Validate(c, b).Any(e => e.Contains("frame")));
    }

    [Test]
    public void Damage_CarriesIntoTheNextFight()
    {
        var k = Career.New(c);
        k.StartJob(c, Job);
        var b = k.StartBattle(c, 1, k.Stage(c).enemy);
        var head = b.player.parts[Slot.Cabeca];
        head.hp -= 3;
        b.player.chassis -= 2;
        k.KeepDamage(c, b);
        Assert.AreEqual(head.def.integrity - 3, k.HpOf(c, head.def.id));
        Assert.AreEqual(2, k.chassisDamage);
        var next = k.StartBattle(c, 2, "enemy");
        Assert.AreEqual(head.def.integrity - 3, next.player.parts[Slot.Cabeca].hp);
        Assert.AreEqual(next.player.chassisMax - 2, next.player.chassis);
    }

    [Test]
    public void Win_PaysAndOffersPartsOfTheBeatenRobot()
    {
        var k = Career.New(c);
        k.StartJob(c, Job);
        var st = k.Stage(c);
        var b = k.StartBattle(c, 1, st.enemy);
        b.result = Result.Win;
        k.FinishJobFight(c, b, new System.Random(1));
        Assert.AreEqual(st.credits, k.credits);
        Assert.AreEqual(st.scrap, k.scrap);
        Assert.AreEqual(1, k.stage);
        Assert.AreEqual(2, k.offer.Length); // even the starter's twin offers its arms as second copies
        Assert.IsTrue(k.offer.All(id => c.Robot(st.enemy).PartIds.Contains(id)));
        Assert.IsTrue(k.offer.All(id => !k.owned.Contains(id) || c.Part(id).slot == "Braco"));
        Assert.Throws<System.InvalidOperationException>(() => k.StartBattle(c, 2, k.Stage(c).enemy)); // pick first
        var pick = k.offer[0];
        int before = k.owned.Count(o => o == pick);
        k.Choose(pick);
        Assert.AreEqual(before + 1, k.owned.Count(o => o == pick));
        Assert.IsEmpty(k.offer);
    }

    [Test]
    public void Loss_EndsTheJobChargesRepairAndKeepsParts()
    {
        var k = Career.New(c);
        k.credits = 100;
        k.StartJob(c, Job);
        var b = k.StartBattle(c, 1, k.Stage(c).enemy);
        b.player.parts[Slot.Torso].hp -= 4;
        b.player.chassis -= 1;
        b.result = Result.Lose;
        int owned = k.owned.Length;
        k.FinishJobFight(c, b, new System.Random(1));
        Assert.IsFalse(k.InJob);
        Assert.AreEqual(100 - 5 * Career.CreditsPerPoint, k.credits);
        Assert.AreEqual(0, k.MissingIntegrity(c));
        Assert.AreEqual(owned, k.owned.Length);

        // Broke: repaired for whatever is left, never negative.
        k.credits = 3;
        k.StartJob(c, Job);
        b = k.StartBattle(c, 1, k.Stage(c).enemy);
        b.player.parts[Slot.Torso].hp -= 4;
        b.result = Result.Lose;
        k.FinishJobFight(c, b, new System.Random(1));
        Assert.AreEqual(0, k.credits);
    }

    [Test]
    public void Repairs_WithScrapAndCredits()
    {
        var k = Career.New(c);
        var torso = k.Build.torso;
        int max = c.Part(torso).integrity;
        k.damaged = new[] { new PartHp { part = torso, hp = max - 5 } };
        k.chassisDamage = 2;
        Assert.IsFalse(k.RepairWithScrap(c, torso)); // no scrap
        k.scrap = 2;
        Assert.IsTrue(k.RepairWithScrap(c, torso));
        Assert.AreEqual(max - 5 + Career.RepairPerScrap, k.HpOf(c, torso));
        Assert.IsTrue(k.RepairWithScrap(c, null));
        Assert.AreEqual(0, k.chassisDamage);
        Assert.AreEqual(0, k.scrap);
        int cost = k.FullRepairCost(c);
        k.credits = cost - 1;
        Assert.IsFalse(k.RepairAllWithCredits(c));
        k.credits = cost;
        Assert.IsTrue(k.RepairAllWithCredits(c));
        Assert.AreEqual(0, k.credits);
        Assert.AreEqual(0, k.MissingIntegrity(c));
    }

    [Test]
    public void SaveLoad_RoundTripsAndRejectsUnknownIds()
    {
        var path = Path.Combine(Path.GetTempPath(), "cb_career_" + System.Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var k = Career.New(c);
            k.credits = 42; k.scrap = 3;
            k.owned = k.owned.Append("canhao_30mm").ToArray();
            k.StartJob(c, Job);
            k.stage = 2;
            k.damaged = new[] { new PartHp { part = k.Build.head, hp = 1 } };
            k.Save(path);
            var l = Career.Load(path, c);
            Assert.AreEqual(42, l.credits);
            Assert.AreEqual(3, l.scrap);
            Assert.Contains("canhao_30mm", l.owned);
            Assert.AreEqual(Job, l.job);
            Assert.AreEqual(2, l.stage);
            Assert.AreEqual(1, l.HpOf(c, k.Build.head));

            File.WriteAllText(path, File.ReadAllText(path).Replace("canhao_30mm", "peca_que_sumiu"));
            Assert.IsNull(Career.Load(path, c));
            File.WriteAllText(path, "{lixo");
            Assert.IsNull(Career.Load(path, c));
            Assert.IsNull(Career.Load(path + ".nope", c));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Test]
    public void Job_CanBeCompletedEndToEnd()
    {
        var r = Simulator.RunJob(c, Job, 30, 7);
        Assert.AreEqual(30, r.runs);
        Assert.Greater(r.completed, 0, "nenhuma run completou o trabalho");
        Assert.AreEqual(r.runs - r.completed, r.lostAt.Sum());
    }

    [Test]
    public void CareerFlow_TitleToJobAndBack()
    {
        var path = Path.Combine(Path.GetTempPath(), "cb_career_" + System.Guid.NewGuid().ToString("N") + ".json");
        var go = new GameObject("UITest");
        try
        {
            var ui = go.AddComponent<GameUI>();
            ui.logBattles = false;
            ui.careerPath = path;
            ui.Init();
            Assert.IsFalse(ui.HasCareer);
            ui.PlayCareer();
            Assert.AreEqual("map", ui.screen);
            ui.ShowJob();
            Assert.AreEqual("job", ui.screen);
            Assert.IsTrue(ui.HasCareer);
            Assert.IsFalse(ui.career.InJob);
            Click(ui, "Aceitar");
            Assert.IsTrue(ui.career.InJob);
            ui.ShowGarage();
            ui.Back();
            Assert.AreEqual("job", ui.screen);
            ui.StartJobFight();
            Assert.AreEqual("battle", ui.screen);
            Assert.IsNotNull(ui.afterBattle);
            ui.battle.result = Result.Win;
            ui.afterBattle(ui.battle);
            Assert.AreEqual("job", ui.screen);
            Assert.AreEqual(1, ui.career.stage);
            Assert.AreEqual(1, Career.Load(path, ui.content).stage); // saved
            ui.Back();
            Assert.AreEqual("map", ui.screen);
            ui.Back();
            Assert.AreEqual("title", ui.screen);
            ui.FreePlay();
            Assert.AreEqual("garage", ui.screen);
            Assert.AreSame(ui.garage, ui.Garage);
        }
        finally
        {
            Object.DestroyImmediate(go);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    static void Click(GameUI ui, string name) =>
        ui.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == name).onClick.Invoke();
}
