using System.Collections.Generic;
using System.Linq;

namespace CardBattler.Combat
{
    // Numbers shown on the garage stats sheet ("Ficha do robô"). Pure, so it can be tested and diffed.
    public class BuildStats
    {
        public int chassis, totalIntegrity, energy, heatPerTurn, hand, weight, carry;
        public List<(Slot slot, string part, int integrity)> parts = new List<(Slot, string, int)>();
        public string[] makers, elements, statuses;
        public string passive;
        public int deckSize, attacks, defense, utility, choose, random;
        public float avgCost;

        public static BuildStats Of(Content c, RobotDef b)
        {
            var frame = c.Frame(b.frame);
            var core = c.Part(b.core);
            var slots = new[] { (Slot.Cabeca, b.head), (Slot.Torso, b.torso), (Slot.BracoEsq, b.armL), (Slot.BracoDir, b.armR), (Slot.Pernas, b.legs) };
            var deck = c.Deck(b);
            var s = new BuildStats
            {
                chassis = frame.chassis,
                energy = core.energy,
                heatPerTurn = core.heatPerTurn,
                hand = frame.hand,
                weight = c.Weight(b),
                carry = frame.carry,
                passive = frame.passiveText,
                makers = c.ActiveMakers(b).Select(m => m.id).ToArray(),
                elements = b.PartIds.Select(id => c.Part(id).element).Where(e => !string.IsNullOrEmpty(e)).Distinct().ToArray(),
                deckSize = deck.Count,
                avgCost = deck.Count == 0 ? 0 : (float)deck.Average(d => d.cost),
            };
            foreach (var (slot, id) in slots) s.parts.Add((slot, c.Part(id).name, c.Part(id).integrity));
            s.totalIntegrity = s.parts.Sum(p => p.integrity);
            var statuses = new List<string>();
            if (deck.Any(d => d.targetHeat > 0)) statuses.Add("Calor");
            if (deck.Any(d => d.overload > 0)) statuses.Add("Sobrecarga");
            if (deck.Any(d => d.corrosion > 0 || d.doubleCorrosion)) statuses.Add("Corrosão");
            s.statuses = statuses.ToArray();
            foreach (var d in deck)
            {
                if (d.TargetsPart && !d.doubleCorrosion)
                {
                    s.attacks++;
                    if (d.cost >= 2 || d.choose) s.choose++; else s.random++;
                }
                else if (d.armor > 0 || d.dodge > 0 || d.repair > 0) s.defense++;
                else s.utility++;
            }
            return s;
        }
    }
}
