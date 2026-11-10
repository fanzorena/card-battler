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
        public bool aim, chainImpact, pierce, pierceNext, armorDamage, secondPart, doubleCorrosion, cancelIntent, peek, clearHeat;
        public bool TargetsPart => damage > 0 || armorDamage || doubleCorrosion;
    }

    [Serializable]
    public class PartDef
    {
        public string id, name, slot, maker, element;
        public int integrity, weight, energy, heatPerTurn;
        public bool ignoresTerrain; // shortcut: no terrain rules yet, flag is data only
        public string[] cards = new string[0];
    }

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
    }

    // Also used as a garage build: frame + one part id per slot.
    [Serializable]
    public class RobotDef
    {
        public string id, name, frame, head, torso, armL, armR, legs, core;
        public int maxCardsPerTurn;

        public RobotDef Clone() => (RobotDef)MemberwiseClone();
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

        public CardDef Card(string id) => cards.FirstOrDefault(c => c.id == id) ?? throw new ArgumentException("Carta desconhecida: " + id);
        public PartDef Part(string id) => parts.FirstOrDefault(p => p.id == id) ?? throw new ArgumentException("Parte desconhecida: " + id);
        public FrameDef Frame(string id) => frames.FirstOrDefault(f => f.id == id) ?? throw new ArgumentException("Frame desconhecido: " + id);
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

        public static Content Load() => JsonUtility.FromJson<Content>(Resources.Load<TextAsset>("content").text);
    }
}
