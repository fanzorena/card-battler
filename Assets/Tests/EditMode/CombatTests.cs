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

    static int Lost(Robot r) => r.parts.Values.Sum(p => p.Max - p.hp); // integrity lost across parts

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
    public void CostOneAttack_HitsARandomPart_ArmorAbsorbsFirst_ChassisUntouched()
    {
        E.armor = 4;
        b.Play(P, Give("soco", Slot.BracoEsq), Slot.Torso); // the chosen slot is ignored: cost 1 is random
        Assert.AreEqual(0, E.armor);
        Assert.AreEqual(2, Lost(E));
        Assert.AreEqual(E.chassisMax, E.chassis);
    }

    [Test]
    public void Targeting_CostTwoChooses_CostOneOnlyWithMirar()
    {
        Assert.AreEqual(Target.None, b.NeedsTarget(P, Give("soco", Slot.BracoEsq)));
        Assert.AreEqual(Target.EnemyPart, b.NeedsTarget(P, Give("gancho", Slot.BracoEsq)));
        b.Play(P, Give("mirar", Slot.Cabeca));
        Assert.AreEqual(Target.EnemyPart, b.NeedsTarget(P, Give("soco", Slot.BracoEsq)));
    }

    [Test]
    public void Mirar_LetsCheapAttackPickPart_BreakingItSpillsIntoTheChassis()
    {
        Assert.IsFalse(Battle.ValidTarget(E, Slot.Chassi));
        b.Play(P, Give("mirar", Slot.Cabeca));
        b.Play(P, Give("gancho", Slot.BracoEsq), Slot.BracoDir); // plasma blade has 6: 4 spills
        Assert.IsTrue(E.parts[Slot.BracoDir].Broken);
        Assert.AreEqual(6, Lost(E));
        Assert.AreEqual(E.chassisMax - 4, E.chassis);
        Assert.IsTrue(E.ChassisOpen);
        Assert.IsTrue(b.history.Any(e => e.evt == "chassis_open" && e.actor == "enemy"));
    }

    [Test]
    public void Chassis_CannotBeChosenUntilAPartIsDestroyed()
    {
        b.Play(P, Give("soco", Slot.BracoEsq), Slot.Chassi); // invalid: falls back to a random part
        Assert.AreEqual(E.chassisMax, E.chassis);   // 6 damage can break the 6-hp blade but nothing spills
        Assert.AreEqual(6, Lost(E));
        E.parts[Slot.Cabeca].hp = 1;
        b.DamagePart(E, Slot.Cabeca, 5);            // breaking it exposes the chassis and the rest spills
        Assert.AreEqual(E.chassisMax - 4, E.chassis);
    }

    [Test]
    public void RandomHits_OnlyLandOnIntactParts()
    {
        foreach (var s in new[] { Slot.Cabeca, Slot.Torso, Slot.BracoEsq, Slot.BracoDir }) E.parts[s].hp = 0;
        for (int i = 0; i < 2; i++) b.Play(P, Give("soco", Slot.BracoEsq));
        Assert.AreEqual(E.parts[Slot.Pernas].Max - 12, E.parts[Slot.Pernas].hp); // every random hit went to the only intact part
        Assert.AreEqual(E.chassisMax, E.chassis);
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
        E.parts[Slot.Cabeca].hp = 0; // chassis exposed
        E.chassis = 30;
        b.Play(P, Give("arrancada", Slot.Pernas)); // legs card right before the hook
        b.Play(P, Give("gancho", Slot.BracoEsq), Slot.Chassi);
        Assert.AreEqual(30 - (10 + Robot.ImpactBonus), E.chassis); // Impacto hits the exposed chassis harder
    }

    [Test]
    public void Encadear_NoBonusWhenNotChained()
    {
        E.parts[Slot.Cabeca].hp = 0;
        E.chassis = 30;
        b.Play(P, Give("gancho", Slot.BracoEsq), Slot.Chassi);
        Assert.AreEqual(30 - 10, E.chassis);
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
    public void Scrap_VirLataFirstScrapDrawsInsteadOfEnergy_ThenGivesEnergy()
    {
        P.energy = 0;
        int deck = P.deck.Count;
        b.Scrap(P, Give("soco", Slot.BracoEsq));
        b.Scrap(P, Give("soco", Slot.BracoEsq));
        Assert.AreEqual(1, P.energy);
        Assert.AreEqual(deck - 1, P.deck.Count);
        Assert.AreEqual(1, P.hand.Count);
    }

    [Test]
    public void Desmontar_DrawsOneWhenItBreaksThePart()
    {
        int hand = P.hand.Count;
        b.Play(P, Give("desmontar", Slot.BracoDir), Slot.BracoDir);
        Assert.IsTrue(E.parts[Slot.BracoDir].Broken);
        Assert.AreEqual(hand + 1, P.hand.Count);
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
        Assert.AreEqual(7, P.parts[Slot.BracoEsq].hp); // Corrosão: 1 damage per turn
        Assert.AreEqual(1, P.parts[Slot.BracoEsq].corrosion);

        P.heat = 12;
        b.enemyIntent.Clear();
        b.EndPlayerTurn();
        Assert.AreEqual(2, P.energy); // 12 -> 10 after dissipation: -1 energy
    }

    [Test]
    public void DestroyedTorso_DoesNotEndTheFight_ChassisAtZeroDoes()
    {
        E.chassis = 3;
        b.Play(P, Give("mirar", Slot.Cabeca));
        b.Play(P, Give("gancho", Slot.BracoEsq), Slot.Torso);
        b.DamagePart(E, Slot.Torso, 99);
        Assert.IsTrue(E.parts[Slot.Torso].Broken);
        Assert.AreEqual(Result.None, b.result);
        b.Play(P, Give("gancho", Slot.BracoEsq), Slot.Chassi);
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
