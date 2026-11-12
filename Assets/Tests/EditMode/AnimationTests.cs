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
