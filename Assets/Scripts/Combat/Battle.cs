using System;
using System.Collections.Generic;
using System.Linq;

namespace CardBattler.Combat
{
    public enum Slot { Cabeca, Torso, BracoEsq, BracoDir, Pernas, Frame }
    public enum Target { None, OwnPart, EnemyPart }
    public enum Result { None, Win, Lose }

    public class Card
    {
        public readonly CardDef def;
        public readonly Slot source;
        public Card(CardDef def, Slot source) { this.def = def; this.source = source; }
    }

    public class Part
    {
        public readonly PartDef def;
        public readonly Slot slot;
        public int hp, corrosion;
        public int Max => def.integrity;
        public bool Broken => hp <= 0;
        public Part(PartDef def, Slot slot) { this.def = def; this.slot = slot; hp = def.integrity; }
    }

    public class Robot
    {
        public const int HeatLimit = 10, HeatDissipation = 2, ImpactBonus = 4;
        public readonly string name;
        public readonly FrameDef frame;
        public readonly PartDef core;
        public readonly Dictionary<Slot, Part> parts = new Dictionary<Slot, Part>();
        public readonly List<Card> deck = new List<Card>(), hand = new List<Card>(), discard = new List<Card>();
        public readonly int maxCardsPerTurn;
        public int chassis, armor, energy, heat, overload, dodge;
        public bool aimPending, scrappedThisTurn;
        public Slot? lastPlayed;

        public Robot(Content c, RobotDef def)
        {
            name = def.name;
            frame = c.Frame(def.frame);
            core = c.Part(def.core);
            chassis = frame.chassis;
            maxCardsPerTurn = def.maxCardsPerTurn > 0 ? def.maxCardsPerTurn : 99;
            Add(c, Slot.Cabeca, def.head); Add(c, Slot.Torso, def.torso);
            Add(c, Slot.BracoEsq, def.armL); Add(c, Slot.BracoDir, def.armR); Add(c, Slot.Pernas, def.legs);
            deck.Add(new Card(c.Card(frame.signature), Slot.Frame));
        }

        void Add(Content c, Slot slot, string partId)
        {
            var p = new Part(c.Part(partId), slot);
            parts[slot] = p;
            foreach (var id in p.def.cards) deck.Add(new Card(c.Card(id), slot));
        }

        public bool Dead => parts[Slot.Torso].hp <= 0 && chassis <= 0;
        public bool IsBroken(Slot s) => s != Slot.Frame && parts[s].Broken;
        public int CostOf(Card c) => c.def.cost + (IsBroken(c.source) ? 1 : 0);
    }

    public class Battle
    {
        public readonly Content content;
        public readonly Robot player, enemy;
        public readonly List<string> log = new List<string>();
        public readonly List<Card> enemyIntent = new List<Card>();
        public Slot enemyAimTarget = Slot.Torso;
        public Result result;
        public int turn;
        readonly Random rng;

        public Battle(Content content, int seed, string playerId = "player", string enemyId = "enemy")
        {
            this.content = content;
            rng = new Random(seed);
            player = new Robot(content, content.Robot(playerId));
            enemy = new Robot(content, content.Robot(enemyId));
            Shuffle(player.deck); Shuffle(enemy.deck);
            PlanEnemy();
            StartTurn(player, true);
        }

        public static string SlotName(Slot s) => s switch
        {
            Slot.Cabeca => "Cabeça", Slot.Torso => "Torso", Slot.BracoEsq => "Braço Esq",
            Slot.BracoDir => "Braço Dir", Slot.Pernas => "Pernas", _ => "Frame"
        };

        Robot Other(Robot r) => r == player ? enemy : player;

        void Shuffle(List<Card> l)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); }
        }

        public void Draw(Robot r, int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (r.deck.Count == 0) { r.deck.AddRange(r.discard); r.discard.Clear(); Shuffle(r.deck); }
                if (r.deck.Count == 0) return;
                r.hand.Add(r.deck[0]); r.deck.RemoveAt(0);
            }
        }

        // Armor lasts until its owner's next turn (Bastião passive: halves instead).
        void StartTurn(Robot r, bool draw)
        {
            if (r == player) turn++;
            r.armor = r.frame.passive == "armorHalves" ? r.armor / 2 : 0;
            r.heat += r.core.heatPerTurn;
            r.energy = Math.Max(0, r.core.energy - r.overload - (r.heat >= Robot.HeatLimit ? 1 : 0));
            if (r.overload > 0) Log($"{r.name}: Sobrecarga, -{r.overload} energia.");
            if (r.heat >= Robot.HeatLimit) Log($"{r.name}: superaquecido, -1 energia.");
            r.overload = 0;
            r.scrappedThisTurn = false;
            r.lastPlayed = null;
            foreach (var p in r.parts.Values.Where(p => p.corrosion > 0).ToList())
            {
                Log($"{r.name}: Corrosão causa {p.corrosion} em {SlotName(p.slot)}.");
                DamagePart(r, p.slot, p.corrosion);
                p.corrosion--;
            }
            CheckEnd();
            if (draw) Draw(r, r.frame.hand - r.hand.Count);
        }

        void EndTurn(Robot r)
        {
            r.discard.AddRange(r.hand); r.hand.Clear();
            r.heat = Math.Max(0, r.heat - Robot.HeatDissipation);
        }

        public Target NeedsTarget(Robot user, Card c) =>
            c.def.repair > 0 ? Target.OwnPart : c.def.damage > 0 && user.aimPending ? Target.EnemyPart : Target.None;

        public bool CanPlay(Robot user, Card c) => result == Result.None && user.hand.Contains(c) && user.energy >= user.CostOf(c);

        public bool Play(Robot user, Card c, Slot? target = null)
        {
            if (!CanPlay(user, c)) return false;
            user.energy -= user.CostOf(c);
            user.hand.Remove(c);
            Log($"{user.name} joga {c.def.name}.");
            Resolve(user, c, target);
            user.discard.Add(c);
            user.lastPlayed = c.source;
            CheckEnd();
            return true;
        }

        public bool Scrap(Robot user, Card c)
        {
            if (result != Result.None || !user.hand.Remove(c)) return false;
            user.discard.Add(c);
            user.energy++;
            Log($"{user.name} sucateia {c.def.name} (+1 energia).");
            if (user.frame.passive == "scrapDraw" && !user.scrappedThisTurn) Draw(user, 1);
            user.scrappedThisTurn = true;
            return true;
        }

        static bool IsLimb(Slot s) => s == Slot.BracoEsq || s == Slot.BracoDir || s == Slot.Pernas;

        void Resolve(Robot user, Card c, Slot? target)
        {
            var d = c.def;
            var opp = Other(user);
            bool bonus = !user.IsBroken(c.source); // broken part: no bonus effects
            bool chained = bonus && user.lastPlayed is Slot last && last != c.source && IsLimb(last) && IsLimb(c.source);

            if (d.armor > 0) user.armor += d.armor;
            if (d.repair > 0)
            {
                var p = target.HasValue && target != Slot.Frame ? user.parts[target.Value]
                    : user.parts.Values.OrderByDescending(x => x.Max - x.hp).First();
                p.hp = Math.Min(p.Max, p.hp + d.repair);
                Log($"{user.name} repara {SlotName(p.slot)}.");
            }
            if (d.draw > 0) Draw(user, d.draw);
            if (d.dodge > 0)
            {
                if (user.IsBroken(Slot.Pernas)) Log("Pernas avariadas: esquiva anulada.");
                else user.dodge = Math.Max(user.dodge, d.dodge);
            }
            if (d.aim) user.aimPending = true;
            user.heat += d.selfHeat;

            if (d.damage > 0)
            {
                var slot = Slot.Torso;
                if (user.aimPending) { if (target.HasValue && target != Slot.Frame) slot = target.Value; user.aimPending = false; }
                bool impact = d.chainImpact && chained;
                if (impact) Log("Encadear: Impacto!");
                bool broke = Attack(user, opp, slot, d.damage, Math.Max(1, d.hits), d.pierce, impact);
                if (broke && bonus && d.breakDraw > 0) Draw(user, d.breakDraw);
                opp.heat += d.targetHeat;
                opp.overload += d.overload;
                opp.parts[slot].corrosion += d.corrosion;
            }
        }

        // Returns true if this attack broke the targeted part.
        bool Attack(Robot user, Robot def, Slot slot, int dmg, int hits, bool pierce, bool impact)
        {
            bool wasBroken = def.parts[slot].Broken;
            int dodge = def.IsBroken(Slot.Pernas) ? 0 : def.dodge;
            def.dodge = 0;
            int total = 0;
            for (int h = 0; h < hits; h++)
            {
                int amount = dmg + (impact && def.parts[slot].Broken ? Robot.ImpactBonus : 0);
                int dodged = Math.Min(dodge, amount); dodge -= dodged; amount -= dodged;
                if (!pierce) { int a = Math.Min(def.armor, amount); def.armor -= a; amount -= a; }
                DamagePart(def, slot, amount);
                total += amount;
            }
            Log($"{def.name} sofre {total} em {SlotName(slot)}.");
            bool broke = !wasBroken && def.parts[slot].Broken;
            if (broke) Log($"{def.name}: {SlotName(slot)} avariada!");
            return broke;
        }

        // Excess damage on a limb/head spills into the torso; torso excess goes to the chassis.
        public void DamagePart(Robot r, Slot slot, int amount)
        {
            if (amount <= 0) return;
            var p = r.parts[slot];
            int a = Math.Min(p.hp, amount); p.hp -= a; amount -= a;
            if (slot == Slot.Pernas && p.Broken) r.dodge = 0;
            if (amount <= 0) return;
            if (slot != Slot.Torso) { DamagePart(r, Slot.Torso, amount); return; }
            r.chassis = Math.Max(0, r.chassis - amount);
        }

        void CheckEnd()
        {
            if (result != Result.None) return;
            if (enemy.Dead) { result = Result.Win; Log("Vitória!"); }
            else if (player.Dead) { result = Result.Lose; Log("Derrota..."); }
        }

        public void EndPlayerTurn()
        {
            if (result != Result.None) return;
            EndTurn(player);
            StartTurn(enemy, false); // hand was already drawn when the intent was revealed
            foreach (var c in enemyIntent.ToList())
            {
                if (result != Result.None) break;
                if (!CanPlay(enemy, c)) { Log($"{enemy.name} não consegue usar {c.def.name}."); continue; }
                Play(enemy, c, c.def.repair > 0 ? (Slot?)null : enemyAimTarget);
            }
            EndTurn(enemy);
            if (result != Result.None) return;
            PlanEnemy();
            StartTurn(player, true);
        }

        // Simple intent AI: draw a hand, pick random affordable cards (aim first) up to maxCardsPerTurn.
        void PlanEnemy()
        {
            enemyIntent.Clear();
            Draw(enemy, enemy.frame.hand - enemy.hand.Count);
            int budget = Math.Max(0, enemy.core.energy - enemy.overload - (enemy.heat + enemy.core.heatPerTurn >= Robot.HeatLimit ? 1 : 0));
            foreach (var c in enemy.hand.OrderBy(_ => rng.Next()))
            {
                if (enemyIntent.Count >= enemy.maxCardsPerTurn) break;
                int cost = enemy.CostOf(c);
                if (cost > budget) continue;
                enemyIntent.Add(c); budget -= cost;
            }
            enemyIntent.Sort((a, b) => (b.def.aim ? 1 : 0) - (a.def.aim ? 1 : 0));
            var alive = player.parts.Values.Where(p => !p.Broken && p.slot != Slot.Torso).ToList();
            enemyAimTarget = alive.Count > 0 ? alive[rng.Next(alive.Count)].slot : Slot.Torso;
        }

        public string IntentText(Card c)
        {
            var d = c.def;
            bool aimed = enemyIntent.Any(x => x.def.aim) && d.damage > 0 && enemyIntent.First(x => x.def.damage > 0) == c;
            if (d.damage > 0) return $"{d.name}: {d.damage}{(d.hits > 1 ? "x" + d.hits : "")} em {SlotName(aimed ? enemyAimTarget : Slot.Torso)}";
            if (d.aim) return $"{d.name} → {SlotName(enemyAimTarget)}";
            return $"{d.name}: {d.text}";
        }

        void Log(string s)
        {
            log.Add(s);
            if (log.Count > 40) log.RemoveAt(0);
        }
    }
}
