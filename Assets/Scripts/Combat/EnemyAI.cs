using System;
using System.Collections.Generic;
using System.Linq;

namespace CardBattler.Combat
{
    // Plans the enemy's whole turn (scraps, plays in order, targets) when the intent is revealed.
    // Strategies are composable flags from content.json:
    //   sucateiro  - scraps weak cards to afford more/better plays
    //   combista   - orders limb cards so they chain (Encadear), prefers chain cards
    //   cirurgiao  - aims (Mirar/Ponto Fraco) at the player part that hurts most to lose
    //   defensivo  - values armor highly
    //   status     - prefers Calor/Sobrecarga/Corrosão cards
    //   adaptativo - lethal check and blocks when the player has been hitting hard
    // No strategy = random affordable cards (easy enemies).
    public static class EnemyAI
    {
        public static void Plan(Battle b)
        {
            var e = b.enemy;
            var p = b.player;
            b.enemyIntent.Clear();
            b.enemyScraps.Clear();
            var rng = b.Rng;
            bool smart = e.strategies.Count > 0;

            int budget = Math.Max(0, e.BaseEnergy - (e.overload > 0 ? 1 : 0) - (e.heat + e.core.heatPerTurn >= Robot.HeatLimit ? 1 : 0));
            int maxCards = e.MaxCards;
            int threat = PlayerThreat(b);
            // Lethal only through the chassis, which needs an exposed chassis and attacks that choose their target.
            int lethalNeed = p.ChassisOpen ? p.chassis + p.armor + p.dodge : int.MaxValue;
            bool canAim = e.hand.Any(c => c.def.aim);
            bool lethal = e.Has("adaptativo") && e.hand.Where(c => c.def.TargetsPart && (Chooses(c.def) || canAim)).Sum(c => Raw(e, c)) >= lethalNeed;

            var scores = e.hand.ToDictionary(c => c, c => smart ? Score(b, c, threat, lethal) : (float)rng.NextDouble());
            if (e.forced != null) scores[e.forced] = 10000;

            var chosen = new List<Card>();
            var scraps = b.enemyScraps;
            int CostAt(Card c, int index) { e.cardsPlayedThisTurn = index; return e.CostOf(c); } // Kagemori discount on the first card only
            var pool = e.hand.OrderByDescending(c => scores[c]).ToList();
            foreach (var c in pool)
            {
                if (chosen.Count >= maxCards) break;
                if (scraps.Contains(c) || (smart && scores[c] <= 0)) continue;
                int cost = CostAt(c, chosen.Count);
                if (cost > budget && e.Has("sucateiro"))
                {
                    // Scrap the weakest cards not yet used to pay for this better one.
                    // Vira-lata's first scrap draws instead of giving energy, so it needs one extra card.
                    int free = e.frame.passive == "scrapDraw" && scraps.Count == 0 ? 1 : 0;
                    int need = cost - budget + free;
                    var junk = pool.Where(x => x != c && !chosen.Contains(x) && !scraps.Contains(x) && scores[x] < scores[c])
                        .OrderBy(x => scores[x]).Take(need).ToList();
                    if (junk.Count == need) { scraps.AddRange(junk); budget += need - free; }
                }
                if (cost > budget) continue;
                chosen.Add(c); budget -= cost;
            }
            e.cardsPlayedThisTurn = 0;

            b.enemyIntent.AddRange(Order(e, chosen));

            // Target for the attacks that choose (cost 2+, or after Mirar). Cost-1 attacks stay random.
            bool aimed = b.enemyIntent.Any(c => c.def.aim);
            int planned = b.enemyIntent.Where(c => c.def.TargetsPart && (Chooses(c.def) || aimed)).Sum(c => Raw(e, c));
            var intact = p.parts.Values.Where(x => !x.Broken).ToList();
            var weakest = intact.OrderBy(x => x.hp).FirstOrDefault()?.slot ?? Slot.Chassi;
            if (!smart)
            {
                var options = intact.Select(x => x.slot).ToList();
                if (p.ChassisOpen || options.Count == 0) options.Add(Slot.Chassi);
                b.enemyAimTarget = options[rng.Next(options.Count)];
            }
            else if (p.ChassisOpen) b.enemyAimTarget = Slot.Chassi;             // the win condition
            else if (e.Has("cirurgiao")) b.enemyAimTarget = StrategicPart(b, planned); // break the part that hurts most to open the chassis
            else b.enemyAimTarget = weakest;                                      // open the chassis as fast as possible
            b.enemyWeakTarget = b.enemyAimTarget != Slot.Chassi ? b.enemyAimTarget : weakest;
        }

        static bool Chooses(CardDef d) => d.cost >= 2 || d.choose;

        static bool IsLimb(Slot s) => s == Slot.BracoEsq || s == Slot.BracoDir || s == Slot.Pernas;

        public static int Raw(Robot user, Card c) =>
            c.def.DamageFor(user.armor) * Math.Max(1, c.def.hits) * (c.def.secondPart ? 2 : 1);

        // Damage the player dealt in its last turn: what an adaptive enemy expects next.
        public static int PlayerThreat(Battle b)
        {
            int turn = b.turn;
            return b.history.Where(x => x.actor == "player" && x.evt == "attack" && x.turn == turn).Sum(x => x.value + x.absorbed);
        }

        static float Score(Battle b, Card c, int threat, bool lethal)
        {
            var e = b.enemy;
            var p = b.player;
            var d = c.def;
            float v = 0;
            int raw = d.TargetsPart && !d.doubleCorrosion ? Raw(e, c) : 0;
            float aimValue = Chooses(d) ? (p.ChassisOpen ? 1.3f : 1f) : 0.8f; // random hits are worth less
            v += (d.pierce ? raw : Math.Max(raw * 0.4f, raw - p.armor)) * aimValue;
            if (raw > 0) v += Bot.ElementValue(Battle.ElementOf(e, c), d, p) * (e.Has("status") ? 2 : 1);
            v += 1.5f * (d.draw + d.chainDraw);
            v += d.armor * 0.4f;
            v += d.repair > 0 ? Math.Min(d.repair, e.parts.Values.Max(x => x.Max - x.hp)) * 0.6f : 0;
            if (d.dodge > 0 && !e.IsBroken(Slot.Pernas)) v += 2;
            if (d.aim) v += e.hand.Any(x => x != c && x.def.TargetsPart && !Chooses(x.def)) ? (p.ChassisOpen ? 5 : 2) : 0;
            if (d.weakPoint > 0) v += e.hand.Count(x => x != c && x.def.TargetsPart) * 1.5f;
            if (d.selfHeat > 0 && e.heat + d.selfHeat >= Robot.HeatLimit) v -= 3;
            if (d.clearHeat) v += e.heat >= 6 ? e.heat : -1;
            if (d.cancelIntent || d.peek) v -= 1; // no effect for the enemy

            if (e.Has("defensivo")) v += d.armor * (e.Life < e.LifeMax / 2 ? 1.2f : 0.8f) + (d.dodge > 0 ? 3 : 0);
            if (e.Has("status")) v += d.targetHeat * 2 + d.overload * 4 + d.corrosion * 2.5f + (d.doubleCorrosion ? p.parts.Values.Max(x => x.corrosion) * 2 : 0);
            if (e.Has("combista") && IsLimb(c.source) && (d.chainImpact || d.chainDraw > 0 || e.frame.passive == "chainEnergy")) v += 2.5f;
            if (e.Has("cirurgiao") && (d.aim || d.weakPoint > 0)) v += 3;
            if (e.Has("adaptativo"))
            {
                if (lethal) v += raw * 3;
                else if (threat > e.armor + 4) v += Math.Min(d.armor, threat) * 0.9f + (d.dodge > 0 ? 3 : 0);
            }
            return v;
        }

        // Non-limb cards first (Mirar, Ponto Fraco, Finta...), then limbs alternating sources so they chain;
        // the boss signature card always leads.
        static List<Card> Order(Robot e, List<Card> chosen)
        {
            var setup = chosen.Where(c => !IsLimb(c.source)).OrderByDescending(c => c == e.forced).ThenByDescending(c => c.def.aim || c.def.weakPoint > 0 || c.def.pierceNext).ToList();
            var limbs = chosen.Where(c => IsLimb(c.source)).ToList();
            if (!e.Has("combista")) return setup.Concat(limbs).ToList();
            var ordered = new List<Card>();
            Slot? last = null;
            while (limbs.Count > 0)
            {
                // Prefer a card from a different limb; among those, chain-payoff cards later in the sequence.
                var next = limbs.Where(c => c.source != last).OrderBy(c => c.def.chainImpact || c.def.chainDraw > 0 ? 1 : 0).FirstOrDefault() ?? limbs[0];
                ordered.Add(next); limbs.Remove(next); last = next.source;
            }
            return setup.Concat(ordered).ToList();
        }

        // The player part whose loss hurts the player most, favoring parts this turn's damage can break
        // (breaking any part also exposes the chassis).
        public static Slot StrategicPart(Battle b, int plannedDamage)
        {
            var p = b.player;
            float Value(Part part)
            {
                var cards = part.def.cards.Select(id => b.content.Card(id)).ToList();
                float v = cards.Sum(c => c.damage * Math.Max(1, c.hits)) + cards.Count(c => c.dodge > 0) * 4 + cards.Sum(c => c.draw) * 2 + cards.Sum(c => c.armor) * 0.5f;
                if (plannedDamage - p.armor >= part.hp) v *= 2; // can break it this turn
                return v / Math.Max(1, part.hp);
            }
            var best = p.parts.Values.Where(x => !x.Broken).OrderByDescending(Value).FirstOrDefault();
            return best?.slot ?? Slot.Chassi;
        }
    }
}
