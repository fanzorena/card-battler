using System.Linq;
using CardBattler.Combat;
using CardBattler.UI;
using NUnit.Framework;
using UnityEngine;

public class UITests
{
    Content content;

    [SetUp]
    public void SetUp() => content = Content.Load();

    [Test]
    public void Highlight_ColorsKeywords_AndHelpExplainsThem()
    {
        var text = content.Card("gancho").text; // "10 de dano. Encadear: Impacto."
        var hl = Theme.Highlight(text);
        StringAssert.Contains("<b>Encadear</b>", hl);
        StringAssert.Contains("<b>Impacto</b>", hl);
        var help = Theme.HelpFor(text).ToList();
        Assert.AreEqual(2, help.Count);
        Assert.IsEmpty(Theme.HelpFor(content.Card("varredura").text));
    }

    [Test]
    public void Kinds_ClassifyCards()
    {
        Assert.AreEqual(CardKind.Attack, Kinds.Of(content.Card("serrar")));
        Assert.AreEqual("3×2", Kinds.Value(content.Card("serrar")));
        Assert.AreEqual(CardKind.Armor, Kinds.Of(content.Card("bloquear")));
        Assert.AreEqual(CardKind.Draw, Kinds.Of(content.Card("varredura")));
        Assert.AreEqual(CardKind.Repair, Kinds.Of(content.Card("remendo")));
        Assert.AreEqual(CardKind.Aim, Kinds.Of(content.Card("mirar")));
        Assert.AreEqual(CardKind.Status, Kinds.Of(content.Card("toxico")));
        Assert.AreEqual("PARTE ALEATÓRIA", Kinds.TargetBadge(content.Card("soco"), false));
        Assert.AreEqual("ESCOLHE A PARTE", Kinds.TargetBadge(content.Card("gancho"), true));
    }

    [Test]
    public void IntentChips_ShowRandomTargetsAsQuestionMark_AndChosenTargetsByName()
    {
        var b = new Battle(content, 1);
        b.enemyIntent.Clear(); b.enemyScraps.Clear();
        b.enemyIntent.Add(new Card(content.Card("disparo"), Slot.BracoEsq));
        b.enemyIntent.Add(new Card(content.Card("esmagar"), Slot.Pernas));
        b.enemyAimTarget = Slot.BracoDir;
        var chips = BattleScreen.IntentChips(b);
        Assert.AreEqual("5 → ?", chips[0].text);
        Assert.IsNull(chips[0].target);
        Assert.AreEqual("7 →", chips[1].text);
        Assert.AreEqual(Slot.BracoDir, chips[1].target); // shown as the arm icon
        Assert.AreEqual(CardKind.Attack, chips[1].kind);
    }

    [Test]
    public void SlotIcons_EverySlotHasADistinctNameAndArmsAreMarkedLeftRight()
    {
        var keys = new[] { Slot.Cabeca, Slot.Torso, Slot.BracoEsq, Slot.BracoDir, Slot.Pernas, Slot.Frame, Slot.Chassi }.Select(SlotIcons.Key).Append(SlotIcons.Nucleo).ToList();
        Assert.AreEqual(keys.Count, keys.Select(SlotIcons.Name).Distinct().Count());
        var go = new GameObject("TestCanvas", typeof(RectTransform));
        try
        {
            string Mark(string key) => SlotIcons.Icon(go.transform, key, 40).GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text).SingleOrDefault();
            Assert.AreEqual("E", Mark("BracoEsq"));
            Assert.AreEqual("D", Mark("BracoDir"));
            Assert.IsNull(Mark("Cabeca"));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void Drop_ScrapZoneScraps_PartPlaysAt_PlayAreaPlays_ElsewhereCancels()
    {
        Assert.AreEqual(BattleScreen.DropKind.Scrap, BattleScreen.Decide(Target.EnemyPart, true, Slot.Torso, null, 900).kind);
        Assert.AreEqual((BattleScreen.DropKind.PlayAt, (Slot?)Slot.BracoEsq), BattleScreen.Decide(Target.EnemyPart, false, Slot.BracoEsq, null, 300));
        Assert.AreEqual((BattleScreen.DropKind.PlayAt, (Slot?)Slot.Torso), BattleScreen.Decide(Target.OwnPart, false, null, Slot.Torso, 500));
        Assert.AreEqual(BattleScreen.DropKind.Play, BattleScreen.Decide(Target.None, false, null, null, 400).kind);
        Assert.AreEqual(BattleScreen.DropKind.ChooseTarget, BattleScreen.Decide(Target.EnemyPart, false, null, null, 400).kind);
        Assert.AreEqual(BattleScreen.DropKind.Cancel, BattleScreen.Decide(Target.None, false, null, null, 1000).kind);
        Assert.AreEqual(BattleScreen.DropKind.Cancel, BattleScreen.Decide(Target.EnemyPart, false, null, Slot.Torso, 1000).kind); // own part is not a target for attacks
    }

    [Test]
    public void TextBox_ReplacesPartNamesWithIcons()
    {
        Assert.AreEqual("Robô 1 sofre 6.", BattleScreen.StripSlotNames("Robô 1 sofre 6 em Braço Dir."));
        Assert.AreEqual("Kiro joga Choque (custo 1).", BattleScreen.StripSlotNames("Kiro joga Choque (Braço Dir, custo 1)."));
        Assert.AreEqual("Robô 1: avariada!", BattleScreen.StripSlotNames("Robô 1: Pernas avariada!"));
        Assert.AreEqual("Mandíbula joga Cabeça Dura (custo 1).", BattleScreen.StripSlotNames("Mandíbula joga Cabeça Dura (Cabeça, custo 1)."));
    }

    [Test]
    public void RobotView_BuildsEveryPartOfTheCatalog()
    {
        var go = new GameObject("TestCanvas", typeof(RectTransform));
        try
        {
            foreach (var head in content.PartsFor("Cabeca"))
            foreach (var torso in content.PartsFor("Torso"))
            {
                var arms = content.PartsFor("Braco");
                var legs = content.PartsFor("Pernas");
                var def = new RobotDef { frame = "vira_lata", head = head.id, torso = torso.id, armL = arms[0].id, armR = arms[arms.Length - 1].id, legs = legs[0].id, core = "bateria_reciclada" };
                var v = RobotView.Create(go.transform, false, false);
                v.Build(content, def);
                Assert.AreEqual(5, v.parts.Count);
            }
            foreach (var arm in content.PartsFor("Braco"))
            foreach (var leg in content.PartsFor("Pernas"))
            {
                var def = new RobotDef { frame = "bastiao", head = "otica_sucata", torso = "placa_m2", armL = arm.id, armR = arm.id, legs = leg.id, core = "reator_k" };
                var v = RobotView.Create(go.transform, true, false);
                v.Build(content, def);
                Assert.IsTrue(v.parts.Values.All(p => p.body.Count > 0), arm.id + "/" + leg.id);
            }
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void RobotView_ShowsBrokenPartsExposedChassisAndArmor()
    {
        var go = new GameObject("TestCanvas", typeof(RectTransform));
        try
        {
            var b = new Battle(content, 1);
            var v = RobotView.Create(go.transform, false, false);
            v.Build(content, content.Robot("player"));
            b.player.parts[Slot.Cabeca].hp = 0;
            b.player.armor = 5;
            v.Show(b.player);
            Assert.Greater(v.parts[Slot.Cabeca].brokenA.color.a, 0.5f);
            Assert.AreEqual(0, v.parts[Slot.Torso].brokenA.color.a);
            Assert.IsTrue(v.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t => t.text == "BLINDAGEM 5"));
        }
        finally { Object.DestroyImmediate(go); }
    }
}

public class GarageFlowTests
{
    Content content;

    [SetUp]
    public void SetUp() => content = Content.Load();

    [Test]
    public void History_CountsRecordedBattles()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cb_hist_" + System.Guid.NewGuid().ToString("N"));
        try
        {
            Assert.AreEqual(0, TitleScreen.HistoryCount(dir)); // folder doesn't exist yet
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "battle_1.jsonl"), "");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "battle_1_resumo.txt"), "");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "battle_2.jsonl"), "");
            Assert.AreEqual(2, TitleScreen.HistoryCount(dir)); // summaries don't count twice
        }
        finally { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); }
    }

    [Test]
    public void BuildStats_ForTheStarterBuild()
    {
        var s = BuildStats.Of(content, content.Robot("player"));
        Assert.AreEqual(8, s.chassis);
        Assert.AreEqual(6 + 14 + 8 + 7 + 8, s.totalIntegrity);
        Assert.AreEqual((3, 0, 5), (s.energy, s.heatPerTurn, s.hand));
        Assert.AreEqual((10, 12), (s.weight, s.carry));
        CollectionAssert.AreEqual(new[] { "Ferrovelho" }, s.makers);
        Assert.AreEqual((20, 8, 7, 5), (s.deckSize, s.attacks, s.defense, s.utility));
        Assert.AreEqual((2, 6), (s.choose, s.random)); // Gancho, Desmontar choose; Soco and Serrar are random
        Assert.AreEqual(21f / 20f, s.avgCost, 0.001f);
    }

    [Test]
    public void Glossary_ExplainsEveryStatRowAndKeyword()
    {
        Theme.Texts = content;
        var ids = GarageScreen.SheetRows(BuildStats.Of(content, content.Robot("player"))).Select(r => r.help)
            .Concat(Theme.Keywords.Select(k => k.glossaryId)).Distinct().ToList();
        foreach (var id in ids)
        {
            var g = content.Glossary(id);
            Assert.IsNotNull(g, id);
            Assert.IsFalse(string.IsNullOrWhiteSpace(g.text), id);
        }
        StringAssert.StartsWith("Chassi:", Theme.Explain("chassi"));
    }

    [Test]
    public void Continue_IsBlockedForAnOverweightBuild()
    {
        Assert.IsNull(GarageScreen.ContinueBlocker(content, content.Robot("player")));
        var heavy = content.Robot("enemy").Clone(); // 22 of weight on a 13 carry frame
        StringAssert.Contains("Peso", GarageScreen.ContinueBlocker(content, heavy));
    }

    [Test]
    public void Navigation_GarageToOpponentsToBattleAndBack()
    {
        var go = new GameObject("GameUITest");
        var es = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        try
        {
            var ui = go.AddComponent<GameUI>();
            ui.logBattles = false;
            ui.Init();
            Assert.AreEqual("title", ui.screen);
            Assert.IsTrue(ui.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.name == "Menu:HISTÓRICO"));
            ui.ShowTitle("about");
            Assert.AreEqual("about", ui.screen);
            ui.Back();
            Assert.AreEqual("title", ui.screen);
            ui.ShowTitle("history");
            ui.Back();
            Assert.AreEqual("title", ui.screen);
            ui.garage.slots[ui.garage.selected] = content.Robot("player").Clone(); // in memory only
            ui.ShowGarage();
            Assert.AreEqual("garage", ui.screen);
            var cont = ui.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Continuar");
            Assert.IsTrue(cont.interactable);

            ui.ShowOpponents();
            Assert.AreEqual("opponents", ui.screen);
            ui.Back();
            Assert.AreEqual("garage", ui.screen);
            ui.Back();
            Assert.AreEqual("title", ui.screen);
            ui.ShowOpponents();
            Assert.AreEqual(OpponentScreen.Opponents(content).Length, ui.GetComponentsInChildren<Clicker>().Count(c => c.name.StartsWith("Opponent:")));

            ui.StartBattle(ui.garage.Current, "enemy_sucata");
            Assert.AreEqual("battle", ui.screen);
            Assert.AreEqual("enemy_sucata", ui.lastEnemy);
            Assert.IsNotNull(ui.battle);

            ui.ShowGarage();
            Assert.AreEqual("garage", ui.screen);
            Assert.IsNull(ui.battle);

            // An invalid build can't reach the opponent screen.
            ui.garage.slots[ui.garage.selected] = content.Robot("enemy").Clone();
            ui.ShowGarage();
            Assert.IsFalse(ui.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Continuar").interactable);
            ui.ShowOpponents();
            Assert.AreEqual("garage", ui.screen);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            if (!es) { var created = UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>(); if (created) UnityEngine.Object.DestroyImmediate(created.gameObject); }
        }
    }

    [Test]
    public void EveryVisiblePart_HasSvgArt()
    {
        // Aspect of the part box in RobotView: the SVG viewBox must match it (importer keeps the viewport).
        var box = new System.Collections.Generic.Dictionary<string, float> { ["Cabeca"] = 120f / 100, ["Torso"] = 180f / 165, ["Braco"] = 90f / 210, ["Pernas"] = 200f / 140 };
        foreach (var p in content.parts.Where(p => p.slot != "Nucleo"))
        {
            var art = Resources.Load<Sprite>("RobotParts/" + p.id);
            Assert.IsNotNull(art, p.id);
            Assert.AreEqual(box[p.slot], art.rect.width / art.rect.height, 0.03f, p.id);
        }
    }
}
