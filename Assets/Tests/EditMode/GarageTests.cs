using System;
using System.IO;
using System.Linq;
using CardBattler.Combat;
using NUnit.Framework;

public class GarageTests
{
    Content content;

    [SetUp]
    public void SetUp() => content = Content.Load();

    RobotDef Build(string frame, string head, string torso, string armL, string armR, string legs, string core) => new RobotDef
        { id = "t", name = "Teste", frame = frame, head = head, torso = torso, armL = armL, armR = armR, legs = legs, core = core };

    RobotDef Kagemori => Build("vira_lata", "sensor_k7", "chassi_aero", "lamina_plasma", "pulso_tesla", "pernas_digitigradas", "bateria_reciclada");
    RobotDef Nyx => Build("vira_lata", "coroa_nyx", "torax_refrigerado", "injetor", "lanca_chamas", "hover", "bateria_reciclada");

    [Test]
    public void Catalog_Has22PartsAnd3Frames()
    {
        Assert.AreEqual(22, content.parts.Length);
        Assert.AreEqual(3, content.frames.Length);
        foreach (var p in content.parts) foreach (var c in p.cards) Assert.DoesNotThrow(() => content.Card(c), p.id + ":" + c);
        foreach (var f in content.frames) Assert.DoesNotThrow(() => content.Card(f.signature));
    }

    [Test]
    public void Weight_StarterFits_OverweightIsRejected()
    {
        var starter = content.Robot("player");
        Assert.AreEqual(10, content.Weight(starter));
        Assert.IsEmpty(content.Validate(starter));

        var heavy = Build("vira_lata", "elmo_bastion", "placa_m2", "canhao_30mm", "escudo_braco", "esteiras", "reator_k");
        Assert.AreEqual(23, content.Weight(heavy));
        StringAssert.Contains("Peso 23", content.Validate(heavy).Single());
        Assert.Throws<ArgumentException>(() => new Battle(content, 1, heavy));
    }

    [Test]
    public void Validate_RejectsPartInWrongSlot()
    {
        var b = content.Robot("player").Clone();
        b.head = "placa_m2";
        StringAssert.Contains("não encaixa", content.Validate(b).Single());
    }

    [Test]
    public void Deck_IsSignaturePlus19PartCards()
    {
        var deck = content.Deck(Kagemori);
        Assert.AreEqual(20, deck.Count);
        Assert.AreEqual("gambiarra", deck[0].id);
        Assert.AreEqual(3, deck.Count(c => c.id == "choque"));
        Assert.AreEqual(2, deck.Count(c => c.id == "analise"));
        var b = new Battle(content, 1, Kagemori);
        Assert.AreEqual(20, b.player.deck.Count + b.player.hand.Count);
    }

    [Test]
    public void ActiveMaker_NeedsThreeParts()
    {
        Assert.AreEqual(new[] { "Ferrovelho" }, content.ActiveMakers(content.Robot("player")).Select(m => m.id).ToArray());
        Assert.AreEqual(new[] { "Bastion" }, content.ActiveMakers(content.Robot("enemy")).Select(m => m.id).ToArray());
        var split = Build("duelista", "otica_sucata", "chassi_aero", "lamina_plasma", "pulso_tesla", "rodas_entrega", "bateria_reciclada");
        CollectionAssert.AreEquivalent(new[] { "Kagemori", "Ferrovelho" }, content.ActiveMakers(split).Select(m => m.id));
        var mixed = Build("vira_lata", "sensor_k7", "carcaca_remendada", "injetor", "pulso_tesla", "hover", "bateria_reciclada");
        Assert.IsEmpty(content.ActiveMakers(mixed));
    }

    [Test]
    public void SaveFile_RoundTrips_AndResetsUnknownParts()
    {
        string path = Path.Combine(Path.GetTempPath(), "cb_garage_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var save = GarageSave.Load(path, content);
            Assert.AreEqual(3, save.slots.Length);
            save.slots[1] = Kagemori;
            save.selected = 1;
            save.Save(path);
            var loaded = GarageSave.Load(path, content);
            Assert.AreEqual(1, loaded.selected);
            Assert.AreEqual("pulso_tesla", loaded.Current.armR);

            File.WriteAllText(path, File.ReadAllText(path).Replace("pulso_tesla", "parte_removida"));
            Assert.AreEqual("punho_hidraulico", GarageSave.Load(path, content).Current.armL); // back to starter
        }
        finally { File.Delete(path); }
    }
}

public class NewCardTests
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
        E.armor = 0;
    }

    Card Give(string id, Slot src)
    {
        var c = new Card(content.Card(id), src);
        P.hand.Add(c);
        return c;
    }

    Battle WithBuild(RobotDef build)
    {
        b = new Battle(content, 1, build);
        P.hand.Clear();
        P.energy = 10;
        E.armor = 0;
        return b;
    }

    [Test]
    public void BastionBonus_EnemyStartsWith6Armor_FerrovelhoScrapGivesArmor()
    {
        var fresh = new Battle(content, 2);
        Assert.AreEqual(6, fresh.enemy.armor);
        P.armor = 0;
        b.Scrap(P, Give("soco", Slot.BracoEsq));
        Assert.AreEqual(2, P.armor);
    }

    [Test]
    public void KagemoriBonus_FirstCardEachTurnCostsOneLess()
    {
        WithBuild(new RobotDef { frame = "vira_lata", head = "sensor_k7", torso = "chassi_aero", armL = "lamina_plasma", armR = "pulso_tesla", legs = "pernas_digitigradas", core = "bateria_reciclada" });
        var c1 = Give("choque", Slot.BracoDir);
        var c2 = Give("choque", Slot.BracoDir);
        Assert.AreEqual(0, P.CostOf(c1));
        b.Play(P, c1);
        Assert.AreEqual(1, P.CostOf(c2));
    }

    [Test]
    public void NyxBonus_StatusLastsLonger()
    {
        WithBuild(new RobotDef { frame = "vira_lata", head = "coroa_nyx", torso = "torax_refrigerado", armL = "injetor", armR = "lanca_chamas", legs = "hover", core = "bateria_reciclada" });
        b.Play(P, Give("acido", Slot.BracoEsq));
        Assert.AreEqual(3, E.parts[Slot.Torso].corrosion);
    }

    [Test]
    public void Choque_Overload_LastsOneTurn()
    {
        b.Play(P, Give("choque", Slot.BracoDir));
        b.Play(P, Give("choque", Slot.BracoDir));
        Assert.AreEqual(1, E.overload); // doesn't stack
        b.enemyIntent.Clear();
        b.EndPlayerTurn();
        Assert.AreEqual(4 - 1, E.energy); // Reator K 4, minus Sobrecarga
    }

    [Test]
    public void Intrusao_CancelsIntent_Analise_Peeks()
    {
        Assert.IsNotEmpty(b.enemyIntent);
        b.Play(P, Give("intrusao", Slot.Cabeca));
        Assert.IsEmpty(b.enemyIntent);
        b.Play(P, Give("analise", Slot.Cabeca));
        Assert.IsTrue(P.peek);
        Assert.AreEqual(4, b.EnemyNextHand.Count);
    }

    [Test]
    public void PontoFraco_AddsThreeToEachAttackOnThatPart()
    {
        b.Play(P, Give("ponto_fraco", Slot.Cabeca), Slot.Torso);
        b.Play(P, Give("serrar", Slot.BracoDir)); // 3+3 then 3
        Assert.AreEqual(20 - 9, E.parts[Slot.Torso].hp);
    }

    [Test]
    public void ArmorCards_MuralhaDefletirAparar()
    {
        int hand = P.hand.Count;
        b.Play(P, Give("muralha", Slot.Torso));
        b.Play(P, Give("defletir", Slot.Torso));
        b.Play(P, Give("aparar", Slot.BracoEsq));
        Assert.AreEqual(12 + 4 + 6, P.armor);
        Assert.AreEqual(hand + 1, P.hand.Count);
    }

    [Test]
    public void Dissipar_RemovesHeat_Inferno_AddsSelfHeat_Labareda_HeatsTarget()
    {
        b.Play(P, Give("inferno", Slot.BracoDir));
        Assert.AreEqual(2, P.heat);
        b.Play(P, Give("dissipar", Slot.Torso));
        Assert.AreEqual(0, P.heat);
        b.Play(P, Give("labareda", Slot.BracoDir));
        Assert.AreEqual(2, E.heat);
    }

    [Test]
    public void Rajada_HitsThreeTimes_ArmorAbsorbsPerHit()
    {
        E.armor = 5;
        b.Play(P, Give("rajada", Slot.BracoEsq)); // 4,4,4 vs 5 armor
        Assert.AreEqual(20 - 7, E.parts[Slot.Torso].hp);
    }

    [Test]
    public void Investida_DealsDamageEqualToOwnArmor()
    {
        P.armor = 9;
        b.Play(P, Give("investida", Slot.BracoEsq));
        Assert.AreEqual(20 - 9, E.parts[Slot.Torso].hp);
        Assert.AreEqual(9, P.armor); // armor is not spent
    }

    [Test]
    public void CortePreciso_And_Finta_IgnoreArmor()
    {
        E.armor = 20;
        b.Play(P, Give("corte_preciso", Slot.BracoDir));
        Assert.AreEqual(20 - 9, E.parts[Slot.Torso].hp);
        b.Play(P, Give("finta", Slot.Frame));
        b.Play(P, Give("soco", Slot.BracoEsq));
        Assert.AreEqual(20 - 15, E.parts[Slot.Torso].hp);
        Assert.AreEqual(20, E.armor);
    }

    [Test]
    public void Arco_HitsTwoParts()
    {
        b.Play(P, Give("arco", Slot.BracoDir));
        Assert.AreEqual(20 - 6, E.parts[Slot.Torso].hp);
        Assert.AreEqual(1, E.parts.Values.Count(p => p.slot != Slot.Torso && p.hp == p.Max - 6));
    }

    [Test]
    public void Toxico_DoublesCorrosion()
    {
        b.Play(P, Give("acido", Slot.BracoEsq));
        b.Play(P, Give("toxico", Slot.BracoEsq));
        Assert.AreEqual(4, E.parts[Slot.Torso].corrosion);
    }

    [Test]
    public void Salto_DodgesAndDrawsWhenChained_ChuteDeslizarEsmagar()
    {
        b.Play(P, Give("soco", Slot.BracoEsq));
        int hand = P.hand.Count;
        b.Play(P, Give("salto", Slot.Pernas));
        Assert.AreEqual(6, P.dodge);
        Assert.AreEqual(hand + 1, P.hand.Count);
        b.Play(P, Give("chute", Slot.Pernas));
        b.Play(P, Give("esmagar", Slot.Pernas));
        Assert.AreEqual(20 - 6 - 5 - 7, E.parts[Slot.Torso].hp);
        b.Play(P, Give("deslizar", Slot.Pernas));
        Assert.AreEqual(3, P.armor);
    }

    [Test]
    public void Duelista_FirstChainEachTurnGivesEnergy()
    {
        WithBuild(new RobotDef { frame = "duelista", head = "otica_sucata", torso = "chassi_aero", armL = "punho_hidraulico", armR = "serra_circular", legs = "rodas_entrega", core = "bateria_reciclada" });
        b.Play(P, Give("soco", Slot.BracoEsq));
        b.Play(P, Give("serrar", Slot.BracoDir));
        b.Play(P, Give("soco", Slot.BracoEsq));
        Assert.AreEqual(10 - 3 + 1, P.energy);
    }

    [Test]
    public void ReatorK_GivesFourEnergyAndOneHeatPerTurn()
    {
        var fresh = new Battle(content, 3, new RobotDef { frame = "bastiao", head = "otica_sucata", torso = "carcaca_remendada", armL = "punho_hidraulico", armR = "serra_circular", legs = "rodas_entrega", core = "reator_k" });
        Assert.AreEqual(4, fresh.player.energy);
        Assert.AreEqual(1, fresh.player.heat);
    }
}
