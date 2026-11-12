using System.Linq;
using CardBattler.Combat;
using CardBattler.UI;
using NUnit.Framework;
using UnityEngine;

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
            var toggle = ui.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "ReduzirAnimacoes");
            toggle.onClick.Invoke();
            Assert.IsTrue(Fx.Reduced);
            Assert.AreEqual(1, PlayerPrefs.GetInt("reduzirAnimacoes"));
            StringAssert.StartsWith("[x]", ui.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.name == "ReduzirAnimacoes").GetComponentInChildren<UnityEngine.UI.Text>().text);
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
        var go = new GameObject("PortraitTest", typeof(RectTransform));
        try
        {
            foreach (var who in new[] { "Celeste", "Kiro Sato (rival)", "Mandíbula, Campeão dos Fossos", "Apostador velho", "Mecânica", "Locutor" })
            {
                Assert.IsTrue(Portraits.Has(who), who);
                Assert.Greater(Portraits.Create(go.transform, who, 100).GetComponentsInChildren<UnityEngine.UI.Image>().Length, 6, who);
            }
            Assert.IsFalse(Portraits.Has("Placa"));
            Assert.AreEqual("P", Portraits.Create(go.transform, "Placa", 100).GetComponentInChildren<UnityEngine.UI.Text>().text);
        }
        finally { Object.DestroyImmediate(go); }
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
