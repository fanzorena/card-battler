using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CardBattler.Combat
{
    // Data lives in Assets/Resources/content.json; add cards/parts/frames/robots there.
    [Serializable]
    public class CardDef
    {
        public string id, name, text;
        public int cost, damage, hits, armor, draw, repair, dodge, breakDraw, chainDraw, weakPoint;
        public int targetHeat, selfHeat, overload, corrosion;
        public bool aim, chainImpact, pierce, pierceNext, armorDamage, secondPart, doubleCorrosion, cancelIntent, peek, clearHeat, exhaust, choose;
        public int armorPct; // armorDamage: % of your armor dealt as damage (0 = 50%)
        public bool TargetsPart => damage > 0 || armorDamage || doubleCorrosion;
        public int DamageFor(int userArmor) => armorDamage ? userArmor * (armorPct > 0 ? armorPct : 50) / 100 : damage;
    }

    [Serializable]
    public class PartDef
    {
        public string id, name, slot, maker, element;
        public string resist, weakTo; // element names: -1 / +1 damage per hit taken
        public int integrity, weight, energy, heatPerTurn;
        public bool ignoresTerrain; // legs: terrain rules don't apply to this robot (Hover)
        public bool wheels;         // legs: affected by "noWheelDodge" terrain
        public string[] cards = new string[0];
    }

    // Arena rule. rule: "noWheelDodge", "rainElectric" or "heatDouble". Robots on Hover legs ignore it.
    [Serializable]
    public class TerrainDef { public string id, name, place, text, rule; }

    [Serializable]
    public class FrameDef
    {
        public string id, name, passive, passiveText, signature;
        public int chassis, hand, carry;
    }

    [Serializable]
    public class MakerDef
    {
        public string id, bonus, text;
        public int value; // armor amount for scrapArmor/startArmor
    }

    // Boss hook: when the enemy's torso+chassis drops to `at` (fraction of max), it gets these buffs once.
    [Serializable]
    public class PhaseDef
    {
        public float at;
        public int energy, cards, armor;
        public string signature, text; // signature: card id added to the hand and played first in that turn
    }

    // One explanation shown by the "?" icons and keyword tooltips (single source for every screen).
    [Serializable]
    public class GlossaryEntry
    {
        public string id, title, text;
    }

    // Pre-fight intel on an enemy (stand-in for exploration). goodBuilds/badBuilds are preset ids the
    // simulator checks against real results, so the text stays true after balance changes.
    [Serializable]
    public class DossierDef
    {
        public string[] rumors = new string[0], worksWell = new string[0], avoid = new string[0];
        public string[] goodBuilds = new string[0], badBuilds = new string[0];
        public bool Exists => rumors != null && rumors.Length > 0;
    }

    // Also used as a garage build: frame + one part id per slot. Enemy-only fields tune the AI.
    [Serializable]
    public class RobotDef
    {
        public string id, name, frame, head, torso, armL, armR, legs, core;
        public int maxCardsPerTurn;
        public string tier;                 // Fácil, Moderado, Difícil, Chefe (enemies only)
        public string terrain;              // home arena terrain id (Treino livre / simulator)
        public string[] strategies = new string[0]; // sucateiro, combista, cirurgiao, defensivo, status, adaptativo
        public int bonusChassis, bonusEnergy;       // fine-tuning only
        public PhaseDef[] phases = new PhaseDef[0];
        public DossierDef dossier = new DossierDef();

        public RobotDef Clone() => (RobotDef)MemberwiseClone();
        public static readonly string[] Tiers = { "Fácil", "Moderado", "Difícil", "Chefe" };
        public string[] PartIds => new[] { head, torso, armL, armR, legs, core };
    }

    [Serializable]
    public class Content
    {
        public const int MakerBonusParts = 3;
        public CardDef[] cards;
        public PartDef[] parts;
        public FrameDef[] frames;
        public MakerDef[] makers;
        public RobotDef[] robots;
        public RobotDef[] presets; // player builds used by the simulator
        public GlossaryEntry[] glossary = new GlossaryEntry[0];
        public JobDef[] jobs = new JobDef[0];
        public TerrainDef[] terrains = new TerrainDef[0];
        public TerrainDef Terrain(string id) => string.IsNullOrEmpty(id) ? null : terrains.FirstOrDefault(t => t.id == id) ?? throw new ArgumentException("Terreno desconhecido: " + id);
        public StageDef[] pit = new StageDef[0];
        public StageDef[] circuit = new StageDef[0];       // Circuito Regional ladder (Distrito Neon)
        public ShopItem[] shop = new ShopItem[0];
        public BlueprintDef[] blueprints = new BlueprintDef[0];
        public BlueprintDef Blueprint(string id) => blueprints.FirstOrDefault(b => b.id == id) ?? throw new ArgumentException("Planta desconhecida: " + id);          // Fosso ranking ladder, last = champion
        public DialogueDef[] dialogues = new DialogueDef[0];
        public ClueDef[] clues = new ClueDef[0];
        public DialogueDef Dialogue(string id) => dialogues.FirstOrDefault(d => d.id == id) ?? throw new ArgumentException("Diálogo desconhecido: " + id);
        public ClueDef Clue(string id) => clues.FirstOrDefault(d => d.id == id) ?? throw new ArgumentException("Pista desconhecida: " + id);
        public GlossaryEntry Glossary(string id) => glossary.FirstOrDefault(g => g.id == id);

        public CardDef Card(string id) => cards.FirstOrDefault(c => c.id == id) ?? throw new ArgumentException("Carta desconhecida: " + id);
        public PartDef Part(string id) => parts.FirstOrDefault(p => p.id == id) ?? throw new ArgumentException("Parte desconhecida: " + id);
        public FrameDef Frame(string id) => frames.FirstOrDefault(f => f.id == id) ?? throw new ArgumentException("Frame desconhecido: " + id);
        public JobDef Job(string id) => jobs.FirstOrDefault(j => j.id == id) ?? throw new ArgumentException("Trabalho desconhecido: " + id);
        public RobotDef Robot(string id) => robots.FirstOrDefault(r => r.id == id) ?? throw new ArgumentException("Robô desconhecido: " + id);
        public PartDef[] PartsFor(string slot) => parts.Where(p => p.slot == slot).ToArray();

        public int Weight(RobotDef b) => b.PartIds.Sum(id => Part(id).weight);

        // Every manufacturer with 3+ equipped parts (core included); a 3+3 build gets both bonuses.
        public MakerDef[] ActiveMakers(RobotDef b) =>
            b.PartIds.Select(id => Part(id).maker).Where(m => !string.IsNullOrEmpty(m))
                .GroupBy(m => m).Where(g => g.Count() >= MakerBonusParts)
                .Select(g => makers.FirstOrDefault(m => m.id == g.Key)).Where(m => m != null).ToArray();

        public static string MakerNames(MakerDef[] m) => m.Length == 0 ? "nenhum" : string.Join(" + ", m.Select(x => x.id));

        // Empty list = build can fight.
        public List<string> Validate(RobotDef b)
        {
            var errors = new List<string>();
            var slots = new[] { ("Cabeca", b.head), ("Torso", b.torso), ("Braco", b.armL), ("Braco", b.armR), ("Pernas", b.legs), ("Nucleo", b.core) };
            foreach (var (slot, id) in slots)
            {
                var p = parts.FirstOrDefault(x => x.id == id);
                if (p == null) errors.Add($"Slot {slot} vazio ou parte desconhecida.");
                else if (p.slot != slot) errors.Add($"{p.name} não encaixa em {slot}.");
            }
            if (frames.All(f => f.id != b.frame)) errors.Add("Frame desconhecido.");
            if (errors.Count > 0) return errors;
            int w = Weight(b), carry = Frame(b.frame).carry;
            if (w > carry) errors.Add($"Peso {w} passa da carga {carry} do frame.");
            return errors;
        }

        // Signature card first, then the part packs in slot order.
        public List<CardDef> Deck(RobotDef b) =>
            new[] { Card(Frame(b.frame).signature) }.Concat(new[] { b.head, b.torso, b.armL, b.armR, b.legs }.SelectMany(id => Part(id).cards.Select(Card))).ToList();

        static readonly Dictionary<string, string> StrategyText = new Dictionary<string, string>
        {
            ["sucateiro"] = "sucateia cartas para jogar mais por turno",
            ["combista"] = "encadeia golpes de braços e pernas",
            ["cirurgiao"] = "mira na sua parte mais valiosa",
            ["defensivo"] = "se protege com blindagem",
            ["status"] = "aplica Calor, Sobrecarga e Corrosão",
            ["adaptativo"] = "fecha a guarda depois de apanhar e ataca quando você está fraco",
        };

        // Plain-language description of an enemy's behavior for the dossier.
        public string Describe(RobotDef enemy)
        {
            var parts = (enemy.strategies ?? new string[0]).Select(s => StrategyText.TryGetValue(s, out var t) ? t : s).ToList();
            if (parts.Count == 0) parts.Add("joga no improviso");
            if (enemy.phases != null && enemy.phases.Length > 0) parts.Add($"muda de fase {enemy.phases.Length}x quando a vida cai");
            return string.Join("; ", parts);
        }

        // Manufacturer with most parts on a build (core included).
        public string MainMaker(RobotDef b) => b.PartIds.Select(id => Part(id).maker).Where(m => !string.IsNullOrEmpty(m))
            .GroupBy(m => m).OrderByDescending(g => g.Count()).First().Key;

        public static Content Load() => JsonUtility.FromJson<Content>(Resources.Load<TextAsset>("content").text);
    }
}
