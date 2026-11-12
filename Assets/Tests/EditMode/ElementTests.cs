using System.Linq;
using CardBattler.Combat;
using NUnit.Framework;

// Automatic element effects (from the source part) and part resistances/weaknesses.
public class ElementTests
{
    Content content;
    Battle b;
    Robot P => b.player;
    Robot E => b.enemy;

    // Player arms swapped per test: Cinético (punho), Térmico (lâmina), Elétrico (tesla), Químico (injetor).
    void Setup(string armL, string enemy = "enemy")
    {
        content = Content.Load();
        var build = content.Robot("player").Clone();
        build.armL = armL;
        b = new Battle(content, 1, build, enemy);
        P.hand.Clear();
        P.energy = 10;
        E.armor = 0;
        E.dodge = 0;
    }

    Card Give(string id, Slot src = Slot.BracoEsq)
    {
        var c = new Card(content.Card(id), src);
        P.hand.Add(c);
        return c;
    }

    [Test]
    public void EveryResistAndWeaknessIsARealElement()
    {
        var c = Content.Load();
        var elements = c.parts.Select(p => p.element).Where(e => !string.IsNullOrEmpty(e)).Distinct().ToArray();
        foreach (var p in c.parts)
        {
            if (!string.IsNullOrEmpty(p.resist)) CollectionAssert.Contains(elements, p.resist, p.id);
            if (!string.IsNullOrEmpty(p.weakTo)) CollectionAssert.Contains(elements, p.weakTo, p.id);
            Assert.AreNotEqual(p.resist ?? "-", p.weakTo ?? "+", p.id);
        }
        foreach (var e in elements) Assert.IsNotNull(Battle.ElementEffect(e), e);
    }

    [Test]
    public void Cinetico_ImpactOnceAgainstExposedChassis()
    {
        Setup("punho_hidraulico");
        E.parts[Slot.Cabeca].hp = 0;
        E.chassis = 30;
        b.Play(P, Give("gancho"), Slot.Chassi);
        Assert.AreEqual(30 - (content.Card("gancho").damage + Robot.ElementImpact), E.chassis);
    }

    [Test]
    public void Termico_AddsHeat_Eletrico_AddsOverload()
    {
        Setup("lamina_plasma");
        int heat = E.heat;
        b.Play(P, Give("corte_preciso"), Slot.BracoEsq);
        Assert.AreEqual(heat + 1, E.heat);

        Setup("pulso_tesla");
        var arco = content.Card("arco");
        Assert.AreEqual(0, arco.overload, "arco não aplica Sobrecarga sozinho");
        b.Play(P, Give("arco"), Slot.BracoEsq);
        Assert.AreEqual(1, E.overload);
    }

    [Test]
    public void Quimico_CorrodesTheHitPart()
    {
        Setup("injetor");
        b.Play(P, Give("mirar", Slot.Cabeca));
        var acido = content.Card("acido");
        b.Play(P, Give("acido"), Slot.BracoEsq);
        Assert.AreEqual(acido.corrosion > 0 ? acido.corrosion : 1, E.parts[Slot.BracoEsq].corrosion, "não acumula com Corrosão da própria carta");
    }

    [Test]
    public void ResistAndWeakness_ChangeDamagePerHit()
    {
        var torso = Content.Load().Robot("enemy").torso;
        var def = Content.Load().Part(torso);
        Assert.AreEqual("Cinético", def.resist, "a placa da Bastion resiste a Cinético");
        Assert.AreEqual(-Robot.ElementMod, Battle.ElementModifier(def, "Cinético"));
        Assert.AreEqual(Robot.ElementMod, Battle.ElementModifier(def, def.weakTo));
        Assert.AreEqual(0, Battle.ElementModifier(def, null));
        Assert.AreEqual(0, Battle.ElementModifier(null, "Cinético"), "chassi não tem resistência");

        Setup("punho_hidraulico");
        b.Play(P, Give("mirar", Slot.Cabeca));
        int hp = E.parts[Slot.Torso].hp;
        b.Play(P, Give("soco"), Slot.Torso);
        Assert.AreEqual(content.Card("soco").damage - Robot.ElementMod, hp - E.parts[Slot.Torso].hp);
    }

    [Test]
    public void SignatureCardsHaveNoElement()
    {
        Setup("punho_hidraulico");
        Assert.IsNull(Battle.ElementOf(P, new Card(content.Card("gancho"), Slot.Frame)));
        Assert.AreEqual("Cinético", Battle.ElementOf(P, new Card(content.Card("gancho"), Slot.BracoEsq)));
    }
}
