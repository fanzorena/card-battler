using System.Linq;
using CardBattler.Combat;
using CardBattler.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

public class AnimationTests
{
    [Test]
    public void ReducedMotion_IsSavedAndToggledFromTheTitle()
    {
        bool before = Fx.Reduced;
        var go = new GameObject("UITest");
        try
        {
            Fx.Reduced = false;
            var ui = go.AddComponent<GameUI>();
            ui.logBattles = false;
            ui.Init();
            Theme.Press(ui.ButtonNamed("ReduzirAnimacoes"));
            Assert.IsTrue(Fx.Reduced);
            Assert.AreEqual(1, PlayerPrefs.GetInt("reduzirAnimacoes"));
            StringAssert.StartsWith("[x]", Theme.LabelOf(ui.ButtonNamed("ReduzirAnimacoes")));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Fx.Reduced = before;
        }
    }

    [Test]
    public void Portraits_KnownCharactersAndFallback()
    {
        var parent = new UnityEngine.UIElements.VisualElement();
        {
            foreach (var who in new[] { "Celeste", "Kiro Sato (rival)", "Mandíbula, Campeão dos Fossos", "Apostador velho", "Mecânica", "Locutor" })
            {
                Assert.IsTrue(Portraits.Has(who), who);
                Assert.Greater(Portraits.Create(parent, who, 100).Query().ToList().Count, 6, who);
            }
            Assert.IsFalse(Portraits.Has("Placa"));
            Assert.AreEqual("P", Portraits.Create(parent, "Placa", 100).Q<UnityEngine.UIElements.Label>().text);
        }
    }

    [Test]
    public void AttackEvents_CarryTheElementForHitEffects()
    {
        var c = Content.Load();
        var b = new Battle(c, 1);
        b.player.energy = 10;
        var soco = new Card(c.Card("soco"), Slot.BracoEsq);
        b.player.hand.Add(soco);
        b.Play(b.player, soco);
        Assert.AreEqual("Cinético", b.history.Last(e => e.evt == "attack").element);
    }
}
