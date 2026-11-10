using System;
using System.Linq;

namespace CardBattler.Combat
{
    // Greedy player AI for the simulator: plays the card with the best value per energy, scraps to unlock a better card.
    // shortcut: one-step heuristic, no lookahead; upgrade to a search if sim results look too far from human play.
    public static class Bot
    {
        // Random legal play, for comparison: random playable cards until none is affordable, never scraps.
        public static void PlayTurnRandom(Battle b, Random rng)
        {
            var p = b.player;
            for (int guard = 0; guard < 60 && b.result == Result.None; guard++)
            {
                var options = p.hand.Where(c => b.CanPlay(p, c)).ToList();
                if (options.Count == 0) break;
                var c = options[rng.Next(options.Count)];
                var t = b.NeedsTarget(p, c);
                Slot? target = t == Target.None ? (Slot?)null
                    : (t == Target.OwnPart ? p : b.enemy).parts.Keys.ElementAt(rng.Next(5));
                b.Play(p, c, target);
            }
        }

        public static void PlayTurn(Battle b)
        {
            var p = b.player;
            for (int guard = 0; guard < 60 && b.result == Result.None; guard++)
            {
                var best = p.hand.Where(c => b.CanPlay(p, c)).Select(c => (card: c, v: Value(b, c)))
                    .Where(x => x.v > 0).OrderByDescending(x => x.v / (p.CostOf(x.card) + 1f)).FirstOrDefault();
                if (best.card != null) { b.Play(p, best.card, TargetFor(b, best.card)); continue; }

                var wanted = p.hand.Where(c => !b.CanPlay(p, c) && p.CostOf(c) == p.energy + 1).OrderByDescending(c => Value(b, c)).FirstOrDefault();
                var junk = wanted == null ? null : p.hand.Where(c => c != wanted).OrderBy(c => Value(b, c)).FirstOrDefault();
                if (junk != null && Value(b, junk) < Value(b, wanted)) { b.Scrap(p, junk); continue; }
                break;
            }
        }

        // Damage the visible enemy intent will deal this turn (before armor).
        public static int Incoming(Battle b) => b.enemyIntent.Where(c => c.def.TargetsPart && !c.def.doubleCorrosion)
            .Sum(c => c.def.DamageFor(b.enemy.armor) * Math.Max(1, c.def.hits) * (c.def.secondPart ? 2 : 1));

        static bool IsLimb(Slot s) => s == Slot.BracoEsq || s == Slot.BracoDir || s == Slot.Pernas;

        static bool AttackAfter(Battle b, Card c) =>
            b.player.hand.Any(x => x != c && x.def.TargetsPart && b.player.CostOf(x) <= b.player.energy - b.player.CostOf(c));

        public static float Value(Battle b, Card c)
        {
            var d = c.def;
            var p = b.player;
            var e = b.enemy;
            int unblocked = Math.Max(0, Incoming(b) - p.armor - p.dodge);
            float v = 0;
            bool chains = p.lastPlayed is Slot last && last != c.source && IsLimb(last) && IsLimb(c.source);
            if (chains && (d.chainImpact || d.chainDraw > 0 || (p.frame.passive == "chainEnergy" && !p.chainEnergyUsed))) v += 3;
            v += 4 * (d.draw + d.chainDraw);
            if (d.armor > 0)
            {
                // Armor blocks the visible intent; leftovers still matter if they persist (Bastião) or feed Investida.
                int useful = Math.Min(d.armor, unblocked);
                v += useful * 1.2f + (d.armor - useful) * (0.2f + (p.frame.passive == "armorHalves" ? 0.4f : 0)
                    + (p.hand.Any(x => x != c && x.def.armorDamage) ? 0.5f : 0));
            }
            if (d.dodge > 0 && p.dodge == 0 && !p.IsBroken(Slot.Pernas)) v += Math.Min(d.dodge, unblocked) * 1.2f;
            if (d.repair > 0) v += Math.Min(d.repair, p.parts.Values.Max(x => x.Max - x.hp));
            if (d.cancelIntent) v += Incoming(b) * 1.2f;
            if (d.clearHeat && p.heat >= 6) v += p.heat;
            if (d.peek) v += 0.5f;
            if (d.aim && AttackAfter(b, c)) v += 2;
            if (d.pierceNext && !p.pierceNext && e.armor > 0 && AttackAfter(b, c)) v += Math.Min(e.armor, 8);
            if (d.weakPoint > 0) v += d.weakPoint * p.hand.Count(x => x != c && x.def.TargetsPart);
            if (d.selfHeat > 0 && p.heat + d.selfHeat >= Robot.HeatLimit) v -= 3;
            if (d.doubleCorrosion) v += e.parts[Slot.Torso].corrosion;
            else if (d.TargetsPart)
            {
                int raw = d.DamageFor(p.armor) * Math.Max(1, d.hits) * (d.secondPart ? 2 : 1);
                v += d.pierce || p.pierceNext ? raw : Math.Max(raw * 0.3f, raw - e.armor);
                v += d.targetHeat + d.overload * 2 + d.corrosion;
                int landed = d.pierce || p.pierceNext ? raw : raw - e.armor;
                if (landed >= e.parts[Slot.Torso].hp + e.chassis) v += 1000; // lethal
            }
            return v;
        }

        public static Slot? TargetFor(Battle b, Card c)
        {
            var p = b.player;
            switch (b.NeedsTarget(p, c))
            {
                case Target.OwnPart:
                    return p.parts.Values.OrderByDescending(x => x.Max - x.hp).First().slot;
                case Target.EnemyPart:
                    if (c.def.weakPoint > 0) return Slot.Torso;
                    // Break an enemy weapon arm if this hit can, otherwise go for the torso.
                    int dmg = c.def.DamageFor(p.armor) * Math.Max(1, c.def.hits);
                    var arm = b.enemy.parts.Values.Where(x => (x.slot == Slot.BracoEsq || x.slot == Slot.BracoDir) && !x.Broken && x.hp <= dmg)
                        .OrderByDescending(x => x.hp).FirstOrDefault();
                    return arm?.slot ?? Slot.Torso;
                default:
                    return null;
            }
        }
    }
}
