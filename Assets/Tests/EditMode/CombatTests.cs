using System.Linq;
using CardBattler.Combat;
using NUnit.Framework;

public class CombatTests
{
    Content content;
    Battle b;
    Robot P => b.player;
    Robot E => b.enemy;

    [SetUp]
    public void SetUp()
    {
        content = Content.Load();
        b = new Battle(content, 1);
        P.hand.Clear();
        P.energy = 10;
        E.armor = 0; // Bastion set bonus starts the enemy with 6 armor
    }

    Card Give(string id, Slot src)
    {
        var c = new Card(content.Card(id), src);
        P.hand.Add(c);
        return c;
    }

    [Test]
    public void StarterBuild_Has19PartCardsPlusSignature_AndStartsWithHandAndEnergy()
    {
        var fresh = new Battle(content, 7);
        Assert.AreEqual(20, fresh.player.deck.Count + fresh.player.hand.Count);
        Assert.AreEqual(5, fresh.player.hand.Count);
        Assert.AreEqual(3, fresh.player.energy);
        Assert.IsNotEmpty(fresh.enemyIntent);
    }

    [Test]
    public void Attack_HitsTorso_ArmorAbsorbsFirst()
    {
        E.armor = 4;
        b.Play(P, Give("soco", Slot.BracoEsq));
        Assert.AreEqual(0, E.armor);
        Assert.AreEqual(20 - 2, E.parts[Slot.Torso].hp);
    }

    [Test]
    public void Mirar_LetsAttackPickPart_AndOverflowSpillsToTorso()
    {
        b.Play(P, Give("mirar", Slot.Cabeca));
        b.Play(P, Give("gancho", Slot.BracoEsq), Slot.BracoDir); // plasma blade has 6
        Assert.IsTrue(E.parts[Slot.BracoDir].Broken);
        Assert.AreEqual(20 - 4, E.parts[Slot.Torso].hp);
    }

    [Test]
    public void BrokenPart_CardsCostOneMore()
    {
        P.parts[Slot.BracoEsq].hp = 0;
        Assert.AreEqual(2, P.CostOf(Give("soco", Slot.BracoEsq)));
    }

    [Test]
    public void Encadear_GancoAfterOtherArm_AddsImpactVsBrokenPart()
    {
        E.parts[Slot.Cabeca].hp = 0;
        b.Play(P, Give("mirar", Slot.Cabeca));
        b.Play(P, Give("arrancada", Slot.Pernas)); // legs card right before the hook
        int before = E.parts[Slot.Torso].hp;
        b.Play(P, Give("gancho", Slot.BracoEsq), Slot.Cabeca); // broken head: everything spills to torso
        Assert.AreEqual(before - (10 + Robot.ImpactBonus), E.parts[Slot.Torso].hp);
    }

    [Test]
    public void Encadear_NoBonusWhenNotChained()
    {
        E.parts[Slot.Cabeca].hp = 0;
        b.Play(P, Give("mirar", Slot.Cabeca));
        int before = E.parts[Slot.Torso].hp;
        b.Play(P, Give("gancho", Slot.BracoEsq), Slot.Cabeca);
        Assert.AreEqual(before - 10, E.parts[Slot.Torso].hp);
    }

    [Test]
    public void Esquivar_ReducesNextAttack_ButBrokenLegsCancelIt()
    {
        b.Play(P, Give("esquivar", Slot.Pernas));
        Assert.AreEqual(6, P.dodge);
        b.DamagePart(P, Slot.Pernas, 8); // legs 8 -> 0
        Assert.AreEqual(0, P.dodge);
        b.Play(P, Give("esquivar", Slot.Pernas));
        Assert.AreEqual(0, P.dodge);
    }

    [Test]
    public void Scrap_GivesEnergy_FirstScrapDrawsOne()
    {
        P.energy = 0;
        int deck = P.deck.Count;
        b.Scrap(P, Give("soco", Slot.BracoEsq));
        b.Scrap(P, Give("soco", Slot.BracoEsq));
        Assert.AreEqual(2, P.energy);
        Assert.AreEqual(deck - 1, P.deck.Count);
        Assert.AreEqual(1, P.hand.Count);
    }

    [Test]
    public void Desmontar_DrawsTwoWhenItBreaksThePart()
    {
        b.Play(P, Give("mirar", Slot.Cabeca));
        int hand = P.hand.Count;
        b.Play(P, Give("desmontar", Slot.BracoDir), Slot.BracoDir);
        Assert.IsTrue(E.parts[Slot.BracoDir].Broken);
        Assert.AreEqual(hand + 2, P.hand.Count);
    }

    [Test]
    public void Heat_Overload_Corrosion_ApplyAtTurnStart()
    {
        P.heat = 10;
        P.overload = 1;
        P.parts[Slot.BracoEsq].corrosion = 2;
        E.armor = 0;
        b.enemyIntent.Clear();
        b.EndPlayerTurn();
        // heat 10 -> 8 after dissipation, so no heat penalty; overload costs 1
        Assert.AreEqual(2, P.energy);
        Assert.AreEqual(6, P.parts[Slot.BracoEsq].hp);
        Assert.AreEqual(1, P.parts[Slot.BracoEsq].corrosion);

        P.heat = 12;
        b.enemyIntent.Clear();
        b.EndPlayerTurn();
        Assert.AreEqual(2, P.energy); // 12 -> 10 after dissipation: -1 energy
    }

    [Test]
    public void TorsoAndChassisAtZero_EndsBattle()
    {
        E.parts[Slot.Torso].hp = 3;
        E.chassis = 3;
        E.armor = 0;
        b.Play(P, Give("soco", Slot.BracoEsq));
        Assert.AreEqual(Result.Win, b.result);
    }

    [Test]
    public void EnemyExecutesIntent_ThenRevealsNext()
    {
        var intent = b.enemyIntent.ToList();
        b.EndPlayerTurn();
        Assert.IsTrue(intent.All(c => E.discard.Contains(c)));
        Assert.IsNotEmpty(b.enemyIntent);
        Assert.AreEqual(5, P.hand.Count);
    }
}
