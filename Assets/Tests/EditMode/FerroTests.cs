using System.IO;
using System.Linq;
using CardBattler.Combat;
using CardBattler.UI;
using NUnit.Framework;
using UnityEngine;

// Ferro-velho: unlock, shop (buy/sell), blueprints and crafting, Duda's job.
public class FerroTests
{
    Content c;
    [SetUp] public void SetUp() => c = Content.Load();

    Battle Ended(Career k, Result r, string enemy)
    {
        var b = k.StartBattle(c, 1, enemy);
        b.result = r;
        return b;
    }

    void WinJob(Career k, string jobId)
    {
        k.StartJob(c, jobId);
        while (!k.JobDone(c))
        {
            k.FinishJobFight(c, Ended(k, Result.Win, k.Stage(c).enemy), new System.Random(1));
            k.SkipReward();
            k.damaged = new PartHp[0]; k.chassisDamage = 0;
        }
    }

    [Test]
    public void Content_ShopBlueprintsAndDudaJobAreValid()
    {
        foreach (var s in c.shop)
        {
            Assert.AreEqual(1, new[] { s.part, s.frame, s.blueprint }.Count(x => x != null), "um item por linha");
            if (s.part != null) c.Part(s.part);
            if (s.frame != null) c.Frame(s.frame);
            if (s.blueprint != null) c.Blueprint(s.blueprint);
            Assert.Greater(s.price, 0);
        }
        foreach (var b in c.blueprints) c.Part(b.part);
        var duda = c.Job("duda_patio");
        Assert.DoesNotThrow(() => c.Blueprint(duda.blueprint));
        CollectionAssert.Contains(c.Job("celeste_fossos").unlocks, "ferro");
    }

    [Test]
    public void Unlock_AfterCelesteJobOrPitTitle()
    {
        var k = Career.New(c);
        Assert.IsFalse(k.IsOpen("ferro"));
        WinJob(k, "celeste_fossos");
        Assert.IsTrue(k.IsOpen("ferro"));

        var k2 = Career.New(c);
        for (int i = 0; i < c.pit.Length; i++) k2.FinishPitFight(c, Ended(k2, Result.Win, k2.PitStage(c).enemy));
        Assert.IsTrue(k2.IsOpen("ferro"), "virar campeão também abre");
    }

    [Test]
    public void Shop_BuyLimitsAndPrices()
    {
        var k = Career.New(c);
        var esteiras = c.shop.First(s => s.part == "esteiras");
        Assert.IsFalse(k.Buy(c, esteiras), "sem créditos");
        k.credits = 1000;
        Assert.IsTrue(k.Buy(c, esteiras));
        Assert.AreEqual(1000 - esteiras.price, k.credits);
        Assert.IsFalse(k.CanBuy(c, esteiras), "pernas: só uma");
        var punho = c.shop.First(s => s.part == "punho_hidraulico");
        Assert.IsTrue(k.Buy(c, punho), "braço: a segunda cópia pode");
        Assert.IsFalse(k.CanBuy(c, punho));
        var frame = c.shop.First(s => s.frame != null);
        Assert.IsTrue(k.Buy(c, frame));
        CollectionAssert.Contains(k.frames, frame.frame);
        Assert.IsFalse(k.CanBuy(c, frame));
        var bp = c.shop.First(s => s.blueprint != null);
        Assert.IsTrue(k.Buy(c, bp));
        CollectionAssert.Contains(k.blueprints, bp.blueprint);
    }

    [Test]
    public void Sell_OnlyWhatNoBuildUses()
    {
        var k = Career.New(c);
        var head = k.Build.head;
        Assert.IsFalse(k.CanSell(head), "peça equipada");
        k.credits = 1000;
        k.Buy(c, c.shop.First(s => s.part == "esteiras"));
        int credits = k.credits;
        Assert.IsTrue(k.Sell(c, "esteiras"));
        Assert.AreEqual(credits + c.shop.First(s => s.part == "esteiras").price / 2, k.credits);
        Assert.IsFalse(k.owned.Contains("esteiras"));

        k.Buy(c, c.shop.First(s => s.part == "punho_hidraulico")); // second copy of an equipped arm
        Assert.IsTrue(k.CanSell("punho_hidraulico"), "a cópia extra pode ser vendida");
        k.garage.slots[1].armR = "punho_hidraulico";            // now another build uses both copies
        Assert.IsFalse(k.CanSell("punho_hidraulico"));
    }

    [Test]
    public void DudaJob_GivesTheHoverBlueprint_ThenCrafting()
    {
        var k = Career.New(c);
        WinJob(k, "duda_patio");
        CollectionAssert.Contains(k.blueprints, "bp_hover");
        var bp = c.Blueprint("bp_hover");
        k.scrap = bp.scrap - 1; k.credits = bp.credits;
        Assert.IsFalse(k.Craft(c, "bp_hover"), "falta sucata");
        k.scrap = bp.scrap;
        Assert.IsTrue(k.Craft(c, "bp_hover"));
        CollectionAssert.Contains(k.owned, "hover");
        Assert.AreEqual(0, k.scrap); Assert.AreEqual(0, k.credits);
        k.scrap = 99; k.credits = 999;
        Assert.IsFalse(k.CanCraft(c, "bp_hover"), "pernas: só uma");
        Assert.IsFalse(k.CanCraft(c, "bp_reator"), "planta desconhecida");
    }

    [Test]
    public void SaveLoad_KeepsBlueprints()
    {
        var path = Path.Combine(Path.GetTempPath(), "cb_ferro_" + System.Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var k = Career.New(c);
            k.LearnBlueprint(c, "bp_hover");
            k.Save(path);
            CollectionAssert.AreEqual(new[] { "bp_hover" }, Career.Load(path, c).blueprints);
            File.WriteAllText(path, File.ReadAllText(path).Replace("bp_hover", "bp_sumiu"));
            Assert.IsNull(Career.Load(path, c));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Test]
    public void Flow_MapToFerroAndDudaJob()
    {
        var path = Path.Combine(Path.GetTempPath(), "cb_ferro_" + System.Guid.NewGuid().ToString("N") + ".json");
        var go = new GameObject("UITest");
        try
        {
            var ui = go.AddComponent<GameUI>();
            ui.logBattles = false;
            ui.careerPath = path;
            ui.Init();
            ui.PlayCareer();
            ui.map.Open("ferro");
            Assert.AreEqual("map", ui.screen, "trancado: só a placa");
            ui.career.Unlock("ferro");
            ui.ShowMap();
            ui.map.Open("ferro");
            Assert.AreEqual("ferro", ui.screen);
            ui.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "TrabalhoDuda").onClick.Invoke();
            Assert.AreEqual("job", ui.screen);
            Assert.AreEqual("duda_patio", ui.career.job);
            ui.ShowCareer("workshop");
            Assert.IsFalse(ui.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.name.StartsWith("Fabricar:")), "sem plantas ainda");
            ui.career.LearnBlueprint(ui.content, "bp_hover");
            ui.ShowCareer("workshop");
            Assert.IsTrue(ui.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.name == "Fabricar:bp_hover"));
            ui.Back();
            Assert.AreEqual("map", ui.screen);
        }
        finally
        {
            Object.DestroyImmediate(go);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
