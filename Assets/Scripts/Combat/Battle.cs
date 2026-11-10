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
        public readonly string id, name;
        public readonly FrameDef frame;
        public readonly PartDef core;
        public readonly Dictionary<Slot, Part> parts = new Dictionary<Slot, Part>();
        public readonly List<Card> deck = new List<Card>(), hand = new List<Card>(), discard = new List<Card>();
        public readonly int maxCardsPerTurn;
        public int chassis, armor, energy, heat, overload, dodge;
        public bool aimPending, scrappedThisTurn;
        public Slot? lastPlayed;

        public Robot(Content c, RobotDef def, string id)
        {
            this.id = id;
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

        public RobotState Snapshot() => new RobotState
        {
            energy = energy, armor = armor, heat = heat, overload = overload, dodge = dodge, aimPending = aimPending,
            chassis = chassis, head = parts[Slot.Cabeca].hp, torso = parts[Slot.Torso].hp, armL = parts[Slot.BracoEsq].hp,
            armR = parts[Slot.BracoDir].hp, legs = parts[Slot.Pernas].hp,
            hand = hand.Count, deck = deck.Count, discard = discard.Count
        };
    }

    // One line of the battle log. Fields that don't apply to an event stay empty/0.
    [Serializable]
    public class RobotState
    {
        public int energy, armor, heat, overload, dodge, chassis, head, torso, armL, armR, legs, hand, deck, discard;
        public bool aimPending;
    }

    [Serializable]
    public class BattleEvent
    {
        public int seq, turn;
        public string actor, evt, card, source, target, msg;
        public int cost, value, absorbed, dodged;
        public bool chained;
        public string[] cards;
        public RobotState player, enemy;
    }

    public class Battle
    {
        public readonly Content content;
        public readonly Robot player, enemy;
        public readonly int seed;
        public readonly List<string> log = new List<string>();
        public readonly List<BattleEvent> history = new List<BattleEvent>();
        public event Action<BattleEvent> Logged;
        public readonly List<Card> enemyIntent = new List<Card>();
        public Slot enemyAimTarget = Slot.Torso;
        public Result result;
        public int turn;
        readonly Random rng;

        public Battle(Content content, int seed, string playerId = "player", string enemyId = "enemy")
        {
            this.content = content;
            this.seed = seed;
            rng = new Random(seed);
            player = new Robot(content, content.Robot(playerId), "player");
            enemy = new Robot(content, content.Robot(enemyId), "enemy");
            foreach (var r in new[] { player, enemy })
                Emit(r, "battle_start", $"{r.name}: frame {r.frame.name}, núcleo {r.core.name}, partes " +
                    string.Join(", ", r.parts.Values.Select(p => $"{SlotName(p.slot)}={p.def.name}({p.Max})")) + $". Seed {seed}.",
                    cards: Names(r.deck), value: seed);
            Shuffle(player.deck); Shuffle(enemy.deck);
            PlanEnemy();
            StartTurn(player, true);
        }

        public static string SlotName(Slot s) => s switch
        {
            Slot.Cabeca => "Cabeça", Slot.Torso => "Torso", Slot.BracoEsq => "Braço Esq",
            Slot.BracoDir => "Braço Dir", Slot.Pernas => "Pernas", _ => "Frame"
        };

        static string[] Names(IEnumerable<Card> cards) => cards.Select(c => c.def.name).ToArray();
        Robot Other(Robot r) => r == player ? enemy : player;

        void Shuffle(List<Card> l)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (l[i], l[j]) = (l[j], l[i]); }
        }

        public void Draw(Robot r, int n)
        {
            var drawn = new List<Card>();
            for (int i = 0; i < n; i++)
            {
                if (r.deck.Count == 0 && r.discard.Count > 0)
                {
                    r.deck.AddRange(r.discard); r.discard.Clear(); Shuffle(r.deck);
                    Emit(r, "reshuffle", $"{r.name} embaralha o descarte no deck.");
                }
                if (r.deck.Count == 0) break;
                drawn.Add(r.deck[0]); r.hand.Add(r.deck[0]); r.deck.RemoveAt(0);
            }
            if (drawn.Count > 0) Emit(r, "draw", $"{r.name} compra {string.Join(", ", Names(drawn))}.", cards: Names(drawn), value: drawn.Count, ui: false);
        }

        // Armor lasts until its owner's next turn (Bastião passive: halves instead).
        void StartTurn(Robot r, bool draw)
        {
            if (r == player) turn++;
            int oldArmor = r.armor;
            r.armor = r.frame.passive == "armorHalves" ? r.armor / 2 : 0;
            if (oldArmor > 0) Emit(r, "armor_expire", $"{r.name}: blindagem {oldArmor} → {r.armor}.", value: oldArmor - r.armor, ui: false);
            r.heat += r.core.heatPerTurn;
            if (r.core.heatPerTurn > 0) Emit(r, "heat_tick", $"{r.name}: núcleo gera {r.core.heatPerTurn} Calor (total {r.heat}).", value: r.core.heatPerTurn, ui: false);
            bool hot = r.heat >= Robot.HeatLimit;
            r.energy = Math.Max(0, r.core.energy - r.overload - (hot ? 1 : 0));
            if (r.overload > 0) Emit(r, "overload_penalty", $"{r.name}: Sobrecarga, -{r.overload} energia.", value: r.overload);
            if (hot) Emit(r, "heat_penalty", $"{r.name}: superaquecido ({r.heat} Calor), -1 energia.", value: 1);
            r.overload = 0;
            r.scrappedThisTurn = false;
            r.lastPlayed = null;
            Emit(r, "turn_start", $"Turno {turn}: {r.name} gera {r.energy} energia.", value: r.energy, ui: false);
            foreach (var p in r.parts.Values.Where(p => p.corrosion > 0).ToList())
            {
                Emit(r, "corrosion_tick", $"{r.name}: Corrosão causa {p.corrosion} em {SlotName(p.slot)}.", target: SlotName(p.slot), value: p.corrosion);
                DamagePart(r, p.slot, p.corrosion);
                p.corrosion--;
            }
            CheckEnd();
            if (draw)
            {
                Draw(r, r.frame.hand - r.hand.Count);
                Emit(r, "hand", $"{r.name} mão: {string.Join(", ", Names(r.hand))}.", cards: Names(r.hand), ui: false);
            }
        }

        void EndTurn(Robot r)
        {
            Emit(r, "turn_end", $"{r.name} encerra o turno com {r.energy} energia sobrando, descarta {r.hand.Count} carta(s).",
                cards: Names(r.hand), value: r.energy, ui: false);
            r.discard.AddRange(r.hand); r.hand.Clear();
            r.heat = Math.Max(0, r.heat - Robot.HeatDissipation);
        }

        public Target NeedsTarget(Robot user, Card c) =>
            c.def.repair > 0 ? Target.OwnPart : c.def.damage > 0 && user.aimPending ? Target.EnemyPart : Target.None;

        public bool CanPlay(Robot user, Card c) => result == Result.None && user.hand.Contains(c) && user.energy >= user.CostOf(c);

        public bool Play(Robot user, Card c, Slot? target = null)
        {
            if (!CanPlay(user, c)) return false;
            int cost = user.CostOf(c);
            user.energy -= cost;
            user.hand.Remove(c);
            Emit(user, "play", $"{user.name} joga {c.def.name} ({SlotName(c.source)}, custo {cost}).",
                card: c.def.name, source: SlotName(c.source), cost: cost, target: target.HasValue ? SlotName(target.Value) : null);
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
            Emit(user, "scrap", $"{user.name} sucateia {c.def.name} (+1 energia).", card: c.def.name, source: SlotName(c.source), value: 1);
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

            if (d.armor > 0)
            {
                user.armor += d.armor;
                Emit(user, "armor", $"{user.name} ganha {d.armor} de blindagem.", card: d.name, value: d.armor, ui: false);
            }
            if (d.repair > 0)
            {
                var p = target.HasValue && target != Slot.Frame ? user.parts[target.Value]
                    : user.parts.Values.OrderByDescending(x => x.Max - x.hp).First();
                int before = p.hp;
                p.hp = Math.Min(p.Max, p.hp + d.repair);
                Emit(user, "repair", $"{user.name} repara {p.hp - before} em {SlotName(p.slot)}.", card: d.name, target: SlotName(p.slot), value: p.hp - before);
            }
            if (d.draw > 0) Draw(user, d.draw);
            if (d.dodge > 0)
            {
                if (user.IsBroken(Slot.Pernas)) Emit(user, "dodge_cancelled", "Pernas avariadas: esquiva anulada.", card: d.name);
                else
                {
                    user.dodge = Math.Max(user.dodge, d.dodge);
                    Emit(user, "dodge", $"{user.name} prepara esquiva de {user.dodge}.", card: d.name, value: user.dodge, ui: false);
                }
            }
            if (d.aim)
            {
                user.aimPending = true;
                Emit(user, "aim", $"{user.name} mira.", card: d.name, ui: false);
            }
            if (d.selfHeat > 0)
            {
                user.heat += d.selfHeat;
                Emit(user, "heat_applied", $"{user.name} ganha {d.selfHeat} Calor (total {user.heat}).", card: d.name, target: user.id, value: d.selfHeat);
            }

            if (d.damage > 0)
            {
                var slot = Slot.Torso;
                if (user.aimPending) { if (target.HasValue && target != Slot.Frame) slot = target.Value; user.aimPending = false; }
                bool impact = d.chainImpact && chained;
                Attack(user, opp, c, slot, Math.Max(1, d.hits), impact, chained, out bool broke);
                if (broke && bonus && d.breakDraw > 0) Draw(user, d.breakDraw);
                if (d.targetHeat > 0)
                {
                    opp.heat += d.targetHeat;
                    Emit(opp, "heat_applied", $"{opp.name} recebe {d.targetHeat} Calor (total {opp.heat}).", card: d.name, value: d.targetHeat);
                }
                if (d.overload > 0)
                {
                    opp.overload += d.overload;
                    Emit(opp, "overload_applied", $"{opp.name} recebe {d.overload} Sobrecarga.", card: d.name, value: d.overload);
                }
                if (d.corrosion > 0)
                {
                    opp.parts[slot].corrosion += d.corrosion;
                    Emit(opp, "corrosion_applied", $"{opp.name} recebe {d.corrosion} Corrosão em {SlotName(slot)}.", card: d.name, target: SlotName(slot), value: d.corrosion);
                }
            }
        }

        void Attack(Robot user, Robot def, Card c, Slot slot, int hits, bool impact, bool chained, out bool broke)
        {
            var d = c.def;
            bool wasBroken = def.parts[slot].Broken;
            int dodge = def.IsBroken(Slot.Pernas) ? 0 : def.dodge;
            def.dodge = 0;
            int total = 0, absorbed = 0, dodged = 0;
            for (int h = 0; h < hits; h++)
            {
                int amount = d.damage + (impact && def.parts[slot].Broken ? Robot.ImpactBonus : 0);
                int dg = Math.Min(dodge, amount); dodge -= dg; amount -= dg; dodged += dg;
                if (!d.pierce) { int a = Math.Min(def.armor, amount); def.armor -= a; amount -= a; absorbed += a; }
                DamagePart(def, slot, amount);
                total += amount;
            }
            Emit(user, "attack", $"{d.name}{(impact ? " (Encadear: Impacto)" : chained ? " (Encadear)" : "")}: {def.name} sofre {total} em {SlotName(slot)}" +
                (absorbed > 0 ? $", {absorbed} na blindagem" : "") + (dodged > 0 ? $", {dodged} esquivado" : "") + ".",
                card: d.name, source: SlotName(c.source), target: SlotName(slot), value: total, absorbed: absorbed, dodged: dodged, chained: chained);
            broke = !wasBroken && def.parts[slot].Broken;
        }

        // Excess damage on a limb/head spills into the torso; torso excess goes to the chassis.
        public void DamagePart(Robot r, Slot slot, int amount)
        {
            if (amount <= 0) return;
            var p = r.parts[slot];
            bool wasBroken = p.Broken;
            int a = Math.Min(p.hp, amount); p.hp -= a; amount -= a;
            if (a > 0) Emit(r, "damage", $"{r.name}: {SlotName(slot)} perde {a} ({p.hp}/{p.Max}).", target: SlotName(slot), value: a, ui: false);
            if (!wasBroken && p.Broken)
            {
                Emit(r, "part_broken", $"{r.name}: {SlotName(slot)} avariada!", target: SlotName(slot));
                if (slot == Slot.Pernas && r.dodge > 0) { r.dodge = 0; Emit(r, "dodge_cancelled", "Pernas avariadas: esquiva anulada."); }
            }
            if (amount <= 0) return;
            if (slot != Slot.Torso) { DamagePart(r, Slot.Torso, amount); return; }
            int ch = Math.Min(r.chassis, amount);
            r.chassis -= ch;
            if (ch > 0) Emit(r, "damage", $"{r.name}: chassi perde {ch} ({r.chassis}/{r.frame.chassis}).", target: "Chassi", value: ch, ui: false);
        }

        void CheckEnd()
        {
            if (result != Result.None) return;
            if (enemy.Dead) result = Result.Win;
            else if (player.Dead) result = Result.Lose;
            else return;
            var parts = string.Join("; ", new[] { player, enemy }.Select(r =>
                $"{r.name}: chassi {r.chassis}, " + string.Join(", ", r.parts.Values.Select(p => $"{SlotName(p.slot)} {p.hp}/{p.Max}"))));
            Emit(result == Result.Win ? player : enemy, "battle_end",
                $"{(result == Result.Win ? "Vitória" : "Derrota")} no turno {turn}. {parts}", value: turn);
        }

        public void EndPlayerTurn()
        {
            if (result != Result.None) return;
            EndTurn(player);
            StartTurn(enemy, false); // hand was already drawn when the intent was revealed
            Emit(enemy, "intent_execute", $"{enemy.name} executa: {string.Join(", ", enemyIntent.Select(IntentText))}.", cards: Names(enemyIntent), ui: false);
            foreach (var c in enemyIntent.ToList())
            {
                if (result != Result.None) break;
                if (!CanPlay(enemy, c)) { Emit(enemy, "intent_skip", $"{enemy.name} não consegue usar {c.def.name}.", card: c.def.name); continue; }
                Play(enemy, c, c.def.repair > 0 ? (Slot?)null : enemyAimTarget);
            }
            if (result != Result.None) return;
            EndTurn(enemy);
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
            Emit(enemy, "intent_reveal", $"Intenção: {string.Join(", ", enemyIntent.Select(IntentText))}.",
                cards: Names(enemyIntent), target: SlotName(enemyAimTarget), ui: false);
        }

        public string IntentText(Card c)
        {
            var d = c.def;
            bool aimed = enemyIntent.Any(x => x.def.aim) && d.damage > 0 && enemyIntent.First(x => x.def.damage > 0) == c;
            if (d.damage > 0) return $"{d.name}: {d.damage}{(d.hits > 1 ? "x" + d.hits : "")} em {SlotName(aimed ? enemyAimTarget : Slot.Torso)}";
            if (d.aim) return $"{d.name} → {SlotName(enemyAimTarget)}";
            return $"{d.name}: {d.text}";
        }

        void Emit(Robot actor, string evt, string msg, string card = null, string source = null, string target = null,
            int cost = 0, int value = 0, int absorbed = 0, int dodged = 0, bool chained = false, string[] cards = null, bool ui = true)
        {
            var e = new BattleEvent
            {
                seq = history.Count, turn = turn, actor = actor.id, evt = evt, msg = msg, card = card, source = source, target = target,
                cost = cost, value = value, absorbed = absorbed, dodged = dodged, chained = chained, cards = cards ?? new string[0],
                player = player?.Snapshot(), enemy = enemy?.Snapshot()
            };
            history.Add(e);
            Logged?.Invoke(e);
            if (!ui) return;
            log.Add(msg);
            if (log.Count > 40) log.RemoveAt(0);
        }
    }
}
