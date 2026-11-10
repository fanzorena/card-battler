using System;
using System.Linq;
using UnityEngine;

namespace CardBattler.Combat
{
    // Data lives in Assets/Resources/content.json; add cards/parts/frames/robots there.
    [Serializable]
    public class CardDef
    {
        public string id, name, text;
        public int cost, damage, hits, armor, draw, repair, dodge, breakDraw;
        public int targetHeat, selfHeat, overload, corrosion;
        public bool aim, chainImpact, pierce;
    }

    [Serializable]
    public class PartDef
    {
        public string id, name, slot;
        public int integrity, energy, heatPerTurn;
        public string[] cards = new string[0];
    }

    [Serializable]
    public class FrameDef
    {
        public string id, name, passive, signature;
        public int chassis, hand, carry;
    }

    [Serializable]
    public class RobotDef
    {
        public string id, name, frame, head, torso, armL, armR, legs, core;
        public int maxCardsPerTurn;
    }

    [Serializable]
    public class Content
    {
        public CardDef[] cards;
        public PartDef[] parts;
        public FrameDef[] frames;
        public RobotDef[] robots;

        public CardDef Card(string id) => cards.FirstOrDefault(c => c.id == id) ?? throw new ArgumentException("Carta desconhecida: " + id);
        public PartDef Part(string id) => parts.FirstOrDefault(p => p.id == id) ?? throw new ArgumentException("Parte desconhecida: " + id);
        public FrameDef Frame(string id) => frames.FirstOrDefault(f => f.id == id) ?? throw new ArgumentException("Frame desconhecido: " + id);
        public RobotDef Robot(string id) => robots.FirstOrDefault(r => r.id == id) ?? throw new ArgumentException("Robô desconhecido: " + id);

        public static Content Load() => JsonUtility.FromJson<Content>(Resources.Load<TextAsset>("content").text);
    }
}
