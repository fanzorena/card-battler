using System.Collections.Generic;
using System.Linq;
using CardBattler.Combat;
using NUnit.Framework;

public class EnemyAITests
{
    Content content;
    Battle b;
    Robot E => b.enemy;
    Robot P => b.player;

    [SetUp]
    public void SetUp() => content = Content.Load();

    // Battle against `enemyId` with its strategies/maxCards overridden, and a hand we control.
    Battle With(string enemyId, string[] strategies, int maxCards, params (string id, Slot src)[] hand)
    {
        var def = content.Robot(enemyId);
        def.strategies = strategies;
        def.maxCardsPerTurn = maxCards;
        b = new Battle(content, 1, null, enemyId);
        E.discard.AddRange(E.hand);
        E.hand.Clear();
        foreach (var (id, src) in hand) E.hand.Add(new Card(content.Card(id), src));
        E.armor = 0; E.heat = 0; E.overload = 0;
        P.armor = 0;
        EnemyAI.Plan(b);
        return b;
    }

    static List<string> Ids(IEnumerable<Card> cards) => cards.Select(c => c.def.id).ToList();

    [Test]
    public void Sucateiro_ScrapsWeakCardsToPlayMore_AndExecutesThePlanExactly()
    {
        var hand = new[] { ("gancho", Slot.BracoEsq), ("desmontar", Slot.BracoDir), ("soco", Slot.BracoEsq), ("varredura", Slot.Cabeca), ("bloquear", Slot.Torso) };
        With("enemy_sucata", new[] { "cirurgiao" }, 3, hand); // smart scoring, no scrapping
        Assert.IsEmpty(b.enemyScraps);
        CollectionAssert.DoesNotContain(Ids(b.enemyIntent), "desmontar");

        // Vira-lata's first scrap draws instead of giving energy, so paying 1 for Desmontar takes 2 scraps.
        With("enemy_sucata", new[] { "sucateiro" }, 3, hand);
        CollectionAssert.AreEquivalent(new[] { "varredura", "bloquear" }, Ids(b.enemyScraps));
        CollectionAssert.AreEquivalent(new[] { "gancho", "desmontar" }, Ids(b.enemyIntent));

        int before = b.history.Count;
        b.EndPlayerTurn();
        var turn = b.history.Skip(before).ToList();
        Assert.AreEqual(2, turn.Count(e => e.actor == "enemy" && e.evt == "scrap"));
        Assert.AreEqual(2, turn.Count(e => e.actor == "enemy" && e.evt == "play"));
        Assert.IsFalse(turn.Any(e => e.evt == "intent_skip"));
    }

    [Test]
    public void Combista_AlternatesLimbsSoCardsChain()
    {
        var hand = new[] { ("soco", Slot.BracoEsq), ("soco", Slot.BracoEsq), ("serrar", Slot.BracoDir) };
        With("enemy_sucata", new[] { "combista" }, 3, hand);
        var src = b.enemyIntent.Select(c => c.source).ToList();
        Assert.AreEqual(3, src.Count);
        Assert.AreNotEqual(src[0], src[1]);
        Assert.AreNotEqual(src[1], src[2]);

        b.EndPlayerTurn();
        Assert.AreEqual(2, b.history.Count(e => e.actor == "enemy" && e.evt == "attack" && e.chained));
    }

    [Test]
    public void Cirurgiao_AimsAtTheMostValuablePlayerPart_OrTorsoForLethal()
    {
        var hand = new[] { ("mirar", Slot.Cabeca), ("disparo", Slot.BracoEsq) };
        With("enemy", new[] { "cirurgiao" }, 2, hand);
        Assert.AreEqual(Slot.BracoDir, b.enemyAimTarget); // Serra Circular: most damage per integrity
        Assert.AreEqual("mirar", b.enemyIntent[0].def.id);

        Assert.AreEqual(Slot.BracoDir, EnemyAI.StrategicPart(b, 7)); // 7 breaks the saw (7 hp)
        P.parts[Slot.Torso].hp = 2;
        P.chassis = 0;
        EnemyAI.Plan(b);
        Assert.AreEqual(Slot.Torso, b.enemyAimTarget);
    }

    [Test]
    public void Defensivo_PrefersArmor()
    {
        var hand = new[] { ("bloquear", Slot.Torso), ("disparo", Slot.BracoEsq) };
        With("enemy", new string[] { "cirurgiao" }, 1, hand);
        Assert.AreEqual("disparo", b.enemyIntent.Single().def.id);
        With("enemy", new[] { "defensivo" }, 1, hand);
        Assert.AreEqual("bloquear", b.enemyIntent.Single().def.id);
    }

    [Test]
    public void Status_PrefersStatusCards()
    {
        var hand = new[] { ("soco", Slot.BracoEsq), ("choque", Slot.BracoDir) };
        With("enemy_sucata", new[] { "cirurgiao" }, 1, hand);
        Assert.AreEqual("soco", b.enemyIntent.Single().def.id);
        With("enemy_sucata", new[] { "status" }, 1, hand);
        Assert.AreEqual("choque", b.enemyIntent.Single().def.id);
    }

    [Test]
    public void Adaptativo_BlocksAfterBigHits_AndGoesForLethal()
    {
        var hand = new[] { ("bloquear", Slot.Torso), ("disparo", Slot.BracoEsq) };
        With("enemy", new[] { "adaptativo" }, 1, hand);
        Assert.AreEqual("disparo", b.enemyIntent.Single().def.id);

        var gancho = new Card(content.Card("gancho"), Slot.BracoEsq);
        P.hand.Add(gancho);
        P.energy = 5;
        b.Play(P, gancho); // 10 damage this turn
        EnemyAI.Plan(b);
        Assert.AreEqual("bloquear", b.enemyIntent.Single().def.id);

        // Lethal needs an exposed chassis and an attack that chooses (cost 2+).
        E.discard.AddRange(E.hand); E.hand.Clear();
        E.hand.Add(new Card(content.Card("bloquear"), Slot.Torso));
        E.hand.Add(new Card(content.Card("esmagar"), Slot.Pernas));
        P.parts[Slot.Cabeca].hp = 0;
        P.chassis = 3;
        EnemyAI.Plan(b);
        Assert.AreEqual("esmagar", b.enemyIntent.Single().def.id);
        Assert.AreEqual(Slot.Chassi, b.enemyAimTarget);
    }

    [Test]
    public void Boss_PhaseTriggersOnce_AddsSignatureFirst_AndBuffsEnergy()
    {
        b = new Battle(content, 1, null, "boss_mandibula");
        Assert.AreEqual(0, E.phasesDone);
        E.chassis = (int)(E.chassisMax * 0.4f); // below 50%, above 20%
        b.enemyIntent.Clear(); b.enemyScraps.Clear();
        b.EndPlayerTurn();
        Assert.AreEqual(1, E.phasesDone);
        Assert.AreEqual(0, E.phaseEnergy); // phase 2 only brings the signature move
        Assert.AreEqual("mordida_campeao", b.enemyIntent[0].def.id);
        Assert.AreEqual(1, b.history.Count(e => e.evt == "boss_phase"));

        int playerLife = P.Life;
        b.EndPlayerTurn();
        Assert.IsTrue(b.history.Any(e => e.evt == "attack" && e.card == "Mordida do Campeão"));
        Assert.IsFalse(E.discard.Concat(E.deck).Concat(E.hand).Any(c => c.def.id == "mordida_campeao")); // exhausted
        Assert.AreEqual(1, b.history.Count(e => e.evt == "boss_phase"));
    }

    [Test]
    public void Roster_EveryCategoryHasEnemies_WithKnownStrategies()
    {
        var known = new[] { "sucateiro", "combista", "cirurgiao", "defensivo", "status", "adaptativo" };
        var enemies = content.robots.Where(r => !string.IsNullOrEmpty(r.tier)).ToList();
        foreach (var tier in RobotDef.Tiers)
            Assert.GreaterOrEqual(enemies.Count(r => r.tier == tier), tier == "Chefe" ? 1 : 2, tier);
        foreach (var r in enemies)
        {
            CollectionAssert.IsSubsetOf(r.strategies, known, r.id);
            CollectionAssert.Contains(RobotDef.Tiers, r.tier);
        }
        CollectionAssert.AreEquivalent(RobotDef.Tiers, Simulator.Targets.Keys);
        foreach (var s in known) Assert.IsTrue(enemies.Any(r => r.strategies.Contains(s)), "estratégia sem inimigo: " + s);
    }
}
