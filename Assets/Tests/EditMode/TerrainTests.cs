using System.Linq;
using CardBattler.Combat;
using NUnit.Framework;

// Arena rules: Piso de sucata (wheels dodge half), Telhado na chuva (+2 Elétrico), Campo de supressão (Calor doubled). Hover ignores terrain.
public class TerrainTests
{
    Content content;
    Battle b;
    Robot P => b.player;
    Robot E => b.enemy;

    void Setup(string terrain, string playerLegs = null, string armL = null, string enemy = "enemy")
    {
        content = Content.Load();
        var build = content.Robot("player").Clone();
        if (playerLegs != null) build.legs = playerLegs;
        if (armL != null) build.armL = armL;
        b = new Battle(content, 1, build, enemy, false, terrain);
        P.hand.Clear();
        P.energy = 10;
        E.armor = 0;
        E.dodge = 0;
    }

    Card Give(string id, Slot src)
    {
        var c = new Card(content.Card(id), src);
        P.hand.Add(c);
        return c;
    }

    [Test]
    public void Content_TerrainsAndHomeArenasAreValid()
    {
        var c = Content.Load();
        CollectionAssert.AreEquivalent(new[] { "noWheelDodge", "rainElectric", "heatDouble" }, c.terrains.Select(t => t.rule));
        foreach (var r in c.robots.Where(r => !string.IsNullOrEmpty(r.tier))) Assert.IsNotNull(c.Terrain(r.terrain), r.id);
        Assert.IsTrue(c.Part("hover").ignoresTerrain);
        Assert.IsTrue(c.Part("rodas_entrega").wheels);
        Assert.IsNull(c.Terrain(null));
        Assert.Throws<System.ArgumentException>(() => c.Terrain("nada"));
    }

    [Test]
    public void Sucata_WheelsDodgeHalf_OtherLegsFull()
    {
        int full = Content.Load().Card("esquivar").dodge;
        Setup("fosso"); // starter legs are wheels: they skid
        b.Play(P, Give("esquivar", Slot.Pernas));
        Assert.AreEqual(full / 2, P.dodge);

        Setup(null);
        b.Play(P, Give("esquivar", Slot.Pernas));
        Assert.AreEqual(full, P.dodge, "sem terreno a esquiva é inteira");

        Setup("fosso", playerLegs: "pernas_digitigradas"); // not wheels
        b.Play(P, Give("esquivar", Slot.Pernas));
        Assert.AreEqual(full, P.dodge);
    }

    [Test]
    public void Chuva_ElectricAttacksHitHarder_UnlessTargetHovers()
    {
        int Hit(string terrain, string enemy)
        {
            Setup(terrain, armL: "pulso_tesla", enemy: enemy);
            b.Play(P, Give("mirar", Slot.Cabeca));
            int hp = E.parts[Slot.BracoEsq].hp;
            b.Play(P, Give("choque", Slot.BracoEsq), Slot.BracoEsq);
            return hp - E.parts[Slot.BracoEsq].hp;
        }
        Assert.AreEqual(Hit(null, "enemy_sucata") + Robot.RainBonus, Hit("neon_chuva", "enemy_sucata"));
        Assert.AreEqual(Hit(null, "enemy_nyx"), Hit("neon_chuva", "enemy_nyx"), "o Rato de Laboratório anda de Hover");
    }

    [Test]
    public void Supressao_DoublesHeatFromAttacks()
    {
        Setup("torre_kagemori", armL: "lamina_plasma", enemy: "enemy_sucata");
        b.Play(P, Give("corte_preciso", Slot.BracoEsq), Slot.BracoEsq); // Térmico element: +1 Calor, doubled
        Assert.AreEqual(2, E.heat);

        Setup(null, armL: "lamina_plasma", enemy: "enemy_sucata");
        b.Play(P, Give("corte_preciso", Slot.BracoEsq), Slot.BracoEsq);
        Assert.AreEqual(1, E.heat);
    }

    [Test]
    public void CareerFightsAreInTheFosso()
    {
        var c = Content.Load();
        var k = Career.New(c);
        Assert.AreEqual("fosso", k.StartBattle(c, 1, "enemy").terrain.id);
        Assert.AreEqual("fosso", Simulator.TerrainFor(c, "enemy", ""));
        Assert.IsNull(Simulator.TerrainFor(c, "enemy", "none"));
        Assert.AreEqual("neon_chuva", Simulator.TerrainFor(c, "enemy", "neon_chuva"));
    }
}
