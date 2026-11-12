using System;
using System.Collections.Generic;
using System.Linq;

namespace CardBattler.Combat
{
    public enum Slot { Cabeca, Torso, BracoEsq, BracoDir, Pernas, Frame, Chassi }
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
        public int hp, corrosion, weak; // weak: Ponto Fraco bonus this turn
        public int Max => def.integrity;
        public bool Broken => hp <= 0;
        public Part(PartDef def, Slot slot) { this.def = def; this.slot = slot; hp = def.integrity; }
    }

    public class Robot
    {
        public const int HeatLimit = 10, HeatDissipation = 2, ImpactBonus = 4;
        public const int ElementImpact = 2, ElementMod = 1; // Cinético bonus per attack; resist/weak per hit
        public const int RainBonus = 2;
        public readonly string id, name;
        public readonly FrameDef frame;
        public readonly PartDef core;
        public readonly MakerDef[] makers; // active manufacturer bonuses
        public readonly int weight;
        public readonly Dictionary<Slot, Part> parts = new Dictionary<Slot, Part>();
        public readonly List<Card> deck = new List<Card>(), hand = new List<Card>(), discard = new List<Card>();
        public readonly int maxCardsPerTurn, chassisMax, bonusEnergy;
        public readonly string tier;
        public readonly HashSet<string> strategies;
        public readonly PhaseDef[] phases;
        public int phasesDone, phaseEnergy, phaseCards;
        public int chassis, armor, energy, heat, overload, dodge;
        public bool aimPending, scrappedThisTurn, pierceNext, chainEnergyUsed, peek;
        public int cardsPlayedThisTurn;
        public Slot? lastPlayed;
        public Card forced; // boss signature card that must lead the next intent

        public Robot(Content c, RobotDef def, string id)
        {
            this.id = id;
            name = def.name;
            frame = c.Frame(def.frame);
            core = c.Part(def.core);
            makers = c.ActiveMakers(def);
            weight = c.Weight(def);
            chassisMax = chassis = frame.chassis + def.bonusChassis;
            bonusEnergy = def.bonusEnergy;
            tier = def.tier;
            strategies = new HashSet<string>(def.strategies ?? new string[0]);
            phases = (def.phases ?? new PhaseDef[0]).OrderByDescending(p => p.at).ToArray();
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

        // Defeat = chassis at 0. The chassis can only be targeted once some part is destroyed.
        public bool Dead => chassis <= 0;
        public int Life => chassis;
        public int LifeMax => chassisMax;
        public bool ChassisOpen => parts.Values.Any(p => p.Broken);
        // Base cost decides targeting: 2+ (or Mirar, or a "choose" card) picks the part, otherwise it's random.
        public bool Chooses(CardDef d) => d.cost >= 2 || d.choose || aimPending;
        public int BaseEnergy => core.energy + bonusEnergy + phaseEnergy;
        public int MaxCards => maxCardsPerTurn + phaseCards;
        public bool Has(string strategy) => strategies.Contains(strategy);
        public bool IsBroken(Slot s) => parts.TryGetValue(s, out var p) && p.Broken;
        public bool HasBonus(string bonus) => makers.Any(m => m.bonus == bonus);
        public int BonusValue(string bonus) => makers.FirstOrDefault(m => m.bonus == bonus)?.value ?? 0;
        public int CostOf(Card c) => Math.Max(0, c.def.cost + (IsBroken(c.source) ? 1 : 0)
            - (HasBonus("firstCardDiscount") && cardsPlayedThisTurn == 0 ? 1 : 0));

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
        public string element; // attack: element of the source part (for hit effects)
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

        public readonly List<Card> enemyScraps = new List<Card>(); // planned scraps, part of the visible intent
        internal Random Rng => rng;

        // basicEnemyAI: ignore the enemy's strategies (simulator uses it to measure their impact).
        public readonly TerrainDef terrain; // null = neutral arena

        // Whether the arena rule applies to this robot (Hover legs ignore terrain).
        public bool TerrainHits(Robot r, string rule) => terrain != null && terrain.rule == rule && !r.parts[Slot.Pernas].def.ignoresTerrain;

        // basicEnemyAI: ignore the enemy's strategies (simulator uses it to measure their impact). terrainId: arena rule (null = none).
        public Battle(Content content, int seed, RobotDef playerBuild = null, string enemyId = "enemy", bool basicEnemyAI = false, string terrainId = null)
        {
            this.content = content;
            terrain = content.Terrain(terrainId);
            this.seed = seed;
            rng = new Random(seed);
            if (playerBuild != null)
            {
                var errors = content.Validate(playerBuild);
                if (errors.Count > 0) throw new ArgumentException("Build inválida: " + string.Join(" ", errors));
            }
            player = new Robot(content, playerBuild ?? content.Robot("player"), "player");
            enemy = new Robot(content, content.Robot(enemyId), "enemy");
            if (basicEnemyAI) enemy.strategies.Clear();
            foreach (var r in new[] { player, enemy })
                Emit(r, "battle_start", $"{r.name}: frame {r.frame.name}, núcleo {r.core.name}, partes " +
                    string.Join(", ", r.parts.Values.Select(p => $"{SlotName(p.slot)}={p.def.name}({p.Max})")) + $". Seed {seed}.",
                    cards: Names(r.deck), value: seed);
            foreach (var r in new[] { player, enemy })
                Emit(r, "build", $"{r.name}: peso {r.weight}/{r.frame.carry}, bônus de fabricante: {(r.makers.Length == 0 ? "nenhum" : string.Join("; ", r.makers.Select(m => m.id + " (" + m.text + ")")))}.",
                    target: Content.MakerNames(r.makers), cost: r.weight, value: r.frame.carry,
                    cards: new[] { r.frame.name }.Concat(r.parts.Values.Select(p => p.def.name)).Append(r.core.name).ToArray(), ui: false);
            if (terrain != null) Emit(player, "terrain", $"Terreno: {terrain.name} ({terrain.place}). {terrain.text}", target: terrain.id, ui: false);
            Shuffle(player.deck); Shuffle(enemy.deck);
            PlanEnemy();
            StartTurn(player, true);
            foreach (var r in new[] { player, enemy }.Where(r => r.HasBonus("startArmor")))
            {
                int v = r.BonusValue("startArmor");
                r.armor += v;
                Emit(r, "bonus", $"{r.name} começa com {v} de blindagem (Bastion).", target: "Bastion", value: v, ui: false);
            }
        }

        public static string SlotName(Slot s) => s switch
        {
            Slot.Cabeca => "Cabeça", Slot.Torso => "Torso", Slot.BracoEsq => "Braço Esq",
            Slot.BracoDir => "Braço Dir", Slot.Pernas => "Pernas", Slot.Chassi => "Chassi", _ => "Frame"
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
            // Sobrecarga = number of upcoming turns with -1 energy.
            r.energy = Math.Max(0, r.BaseEnergy - (r.overload > 0 ? 1 : 0) - (hot ? 1 : 0));
            if (r.overload > 0) Emit(r, "overload_penalty", $"{r.name}: Sobrecarga, -1 energia ({r.overload - 1} turno(s) restante(s)).", value: 1);
            if (hot) Emit(r, "heat_penalty", $"{r.name}: superaquecido ({r.heat} Calor), -1 energia.", value: 1);
            r.overload = Math.Max(0, r.overload - 1);
            r.scrappedThisTurn = false;
            r.chainEnergyUsed = false;
            r.cardsPlayedThisTurn = 0;
            r.lastPlayed = null;
            Emit(r, "turn_start", $"Turno {turn}: {r.name} gera {r.energy} energia.", value: r.energy, ui: false);
            foreach (var p in r.parts.Values.Where(p => p.corrosion > 0).ToList())
            {
                // Corrosão = turns left; 1 damage per turn ("um dano por turno" in the design doc).
                Emit(r, "corrosion_tick", $"{r.name}: Corrosão causa 1 em {SlotName(p.slot)} ({p.corrosion - 1} turno(s) restante(s)).", target: SlotName(p.slot), value: 1);
                DamagePart(r, p.slot, 1);
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
            r.peek = false;
            foreach (var p in Other(r).parts.Values) p.weak = 0;
            r.heat = Math.Max(0, r.heat - Robot.HeatDissipation);
        }

        public Target NeedsTarget(Robot user, Card c) =>
            c.def.repair > 0 ? Target.OwnPart
            : c.def.weakPoint > 0 || (c.def.TargetsPart && user.Chooses(c.def)) ? Target.EnemyPart : Target.None;

        // A chosen target must be an intact part, or the chassis once it is exposed.
        public static bool ValidTarget(Robot r, Slot? s) =>
            s.HasValue && (s == Slot.Chassi ? r.ChassisOpen : r.parts.TryGetValue(s.Value, out var p) && !p.Broken);

        // Random hits land on an intact part (the chassis only if every part is destroyed).
        public Slot RandomPart(Robot r, Slot? except = null)
        {
            var intact = r.parts.Values.Where(p => !p.Broken && p.slot != except).ToList();
            return intact.Count == 0 ? Slot.Chassi : intact[rng.Next(intact.Count)].slot;
        }

        public bool CanPlay(Robot user, Card c) => result == Result.None && user.hand.Contains(c) && user.energy >= user.CostOf(c);

        public bool Play(Robot user, Card c, Slot? target = null)
        {
            if (!CanPlay(user, c)) return false;
            int cost = user.CostOf(c);
            user.energy -= cost;
            int fullCost = c.def.cost + (user.IsBroken(c.source) ? 1 : 0);
            if (fullCost > cost) Emit(user, "bonus", $"{user.name}: {c.def.name} custa 1 a menos (Kagemori).", card: c.def.name, target: "Kagemori", value: fullCost - cost, ui: false);
            user.hand.Remove(c);
            Emit(user, "play", $"{user.name} joga {c.def.name} ({SlotName(c.source)}, custo {cost}).",
                card: c.def.name, source: SlotName(c.source), cost: cost, target: target.HasValue ? SlotName(target.Value) : null);
            Resolve(user, c, target);
            user.cardsPlayedThisTurn++;
            if (!c.def.exhaust) user.discard.Add(c);
            if (user.forced == c) user.forced = null;
            user.lastPlayed = c.source;
            CheckEnd();
            return true;
        }

        public bool Scrap(Robot user, Card c)
        {
            if (result != Result.None || !user.hand.Remove(c)) return false;
            user.discard.Add(c);
            // Vira-lata: the first scrap of the turn draws a card instead of giving energy (drawing is never free).
            bool swap = user.frame.passive == "scrapDraw" && !user.scrappedThisTurn;
            if (!swap) user.energy++;
            Emit(user, "scrap", $"{user.name} sucateia {c.def.name} ({(swap ? "compra 1" : "+1 energia")}).", card: c.def.name, source: SlotName(c.source), value: swap ? 0 : 1);
            if (user.HasBonus("scrapArmor"))
            {
                int v = user.BonusValue("scrapArmor");
                user.armor += v;
                Emit(user, "bonus", $"{user.name} ganha {v} de blindagem (Ferrovelho).", card: c.def.name, target: "Ferrovelho", value: v, ui: false);
            }
            if (swap) Draw(user, 1);
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
            int statusExtra = user.HasBonus("longerStatus") ? 1 : 0;

            if (chained && user.frame.passive == "chainEnergy" && !user.chainEnergyUsed)
            {
                user.chainEnergyUsed = true;
                user.energy++;
                Emit(user, "chain_energy", $"{user.name}: primeiro Encadear do turno gera 1 energia.", card: d.name, value: 1);
            }
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
            if (d.chainDraw > 0 && chained) Draw(user, d.chainDraw);
            if (d.dodge > 0)
            {
                if (user.IsBroken(Slot.Pernas)) Emit(user, "dodge_cancelled", "Pernas avariadas: esquiva anulada.", card: d.name);
                else
                {
                    // Piso de sucata: wheels skid, the dodge is halved.
                    bool skid = user.parts[Slot.Pernas].def.wheels && TerrainHits(user, "noWheelDodge");
                    user.dodge = Math.Max(user.dodge, skid ? d.dodge / 2 : d.dodge);
                    if (skid) Emit(user, "dodge_skid", $"{terrain.name}: as rodas derrapam, esquiva pela metade.", card: d.name, value: d.dodge / 2);
                    Emit(user, "dodge", $"{user.name} prepara esquiva de {user.dodge}.", card: d.name, value: user.dodge, ui: false);
                }
            }
            if (d.aim)
            {
                user.aimPending = true;
                Emit(user, "aim", $"{user.name} mira.", card: d.name, ui: false);
            }
            if (d.pierceNext)
            {
                user.pierceNext = true;
                Emit(user, "pierce_next", $"{user.name}: o próximo ataque ignora blindagem.", card: d.name, ui: false);
            }
            if (d.selfHeat > 0)
            {
                user.heat += d.selfHeat;
                Emit(user, "heat_applied", $"{user.name} ganha {d.selfHeat} Calor (total {user.heat}).", card: d.name, target: user.id, value: d.selfHeat);
            }
            if (d.clearHeat)
            {
                Emit(user, "heat_cleared", $"{user.name} remove {user.heat} de Calor.", card: d.name, value: user.heat);
                user.heat = 0;
            }
            if (d.peek && user == player)
            {
                user.peek = true;
                Emit(user, "peek", $"{user.name} vê a próxima mão do inimigo: {string.Join(", ", Names(EnemyNextHand))}.", card: d.name, cards: Names(EnemyNextHand), ui: false);
            }
            if (d.cancelIntent && user == player) // the player has no visible intent for the enemy to cancel
            {
                Emit(user, "intent_cancel", $"{user.name} cancela a intenção do inimigo.", card: d.name, cards: Names(enemyIntent));
                enemyIntent.Clear();
                enemyScraps.Clear();
            }
            if (d.weakPoint > 0)
            {
                var slot = target != Slot.Chassi && ValidTarget(opp, target) ? target.Value : RandomPart(opp);
                if (slot == Slot.Chassi) return;
                opp.parts[slot].weak += d.weakPoint;
                Emit(opp, "weak_point", $"{opp.name}: {SlotName(slot)} sofre +{d.weakPoint} de cada ataque neste turno.", card: d.name, target: SlotName(slot), value: d.weakPoint);
            }

            if (!d.TargetsPart) return;
            bool chooses = user.Chooses(d);
            user.aimPending = false; // Mirar is spent on the next attack
            // A chosen target that is no longer valid (part destroyed meanwhile) falls back to the chassis if exposed.
            var main = chooses ? (ValidTarget(opp, target) ? target.Value : opp.ChassisOpen ? Slot.Chassi : RandomPart(opp)) : RandomPart(opp);
            if (d.doubleCorrosion)
            {
                var p = main == Slot.Chassi ? opp.parts.Values.OrderByDescending(x => x.corrosion).First() : opp.parts[main];
                main = p.slot;
                Emit(opp, "corrosion_applied", $"{opp.name}: Corrosão em {SlotName(main)} dobra ({p.corrosion} → {p.corrosion * 2}).", card: d.name, target: SlotName(main), value: p.corrosion);
                p.corrosion *= 2;
                return;
            }
            int dmg = d.DamageFor(user.armor);
            bool pierce = d.pierce || user.pierceNext;
            user.pierceNext = false;
            var slots = new List<Slot> { main };
            if (d.secondPart)
            {
                var other = RandomPart(opp, main);
                if (other != Slot.Chassi) slots.Add(other);
            }
            foreach (var slot in slots)
            {
                bool impact = d.chainImpact && chained;
                Attack(user, opp, c, slot, dmg, Math.Max(1, d.hits), pierce, impact, chained, out bool broke, out int dealt);
                if (broke && bonus && d.breakDraw > 0) Draw(user, d.breakDraw);
                if (dealt > 0) ElementStatus(user, opp, c, slot);
                if (d.corrosion > 0 && slot != Slot.Chassi)
                {
                    opp.parts[slot].corrosion += d.corrosion + statusExtra;
                    if (statusExtra > 0) Emit(user, "bonus", "Nyx: +1 de Corrosão.", card: d.name, target: "Nyx", value: 1, ui: false);
                    Emit(opp, "corrosion_applied", $"{opp.name} recebe {d.corrosion + statusExtra} Corrosão em {SlotName(slot)}.", card: d.name, target: SlotName(slot), value: d.corrosion + statusExtra);
                }
            }
            if (d.targetHeat > 0)
            {
                int heat = HeatOn(opp, d.targetHeat);
                opp.heat += heat;
                Emit(opp, "heat_applied", $"{opp.name} recebe {heat} Calor (total {opp.heat}){(heat > d.targetHeat ? $" ({terrain.name}: dobrado)" : "")}.", card: d.name, value: heat);
            }
            if (d.overload > 0)
            {
                opp.overload = Math.Max(opp.overload, d.overload + statusExtra); // doesn't stack: "1 energia a menos no próximo turno"
                if (statusExtra > 0) Emit(user, "bonus", "Nyx: Sobrecarga dura +1 turno.", card: d.name, target: "Nyx", value: 1, ui: false);
                Emit(opp, "overload_applied", $"{opp.name} recebe Sobrecarga por {d.overload + statusExtra} turno(s).", card: d.name, value: d.overload + statusExtra);
            }
        }

        // ---- Elements: an attack carries the element of the part it comes from ----
        public static string ElementOf(Robot user, Card c) => c.source == Slot.Frame || !user.parts.ContainsKey(c.source) ? null : user.parts[c.source].def.element;

        public static string ElementEffect(string element) => element switch
        {
            "Cinético" => $"Impacto: +{Robot.ElementImpact} contra parte avariada ou chassi exposto",
            "Térmico" => "+1 Calor no alvo",
            "Elétrico" => "Sobrecarga no alvo",
            "Químico" => "+1 Corrosão na parte atingida",
            _ => null,
        };

        // Per-hit change from the target part's resistance/weakness (chassis has none).
        public static int ElementModifier(PartDef target, string element) =>
            target == null || string.IsNullOrEmpty(element) ? 0 : target.resist == element ? -Robot.ElementMod : target.weakTo == element ? Robot.ElementMod : 0;

        // Status that comes with the element when the attack lands. Cards that already apply the same status don't stack it.
        void ElementStatus(Robot user, Robot opp, Card c, Slot slot)
        {
            var d = c.def;
            switch (ElementOf(user, c))
            {
                case "Térmico" when d.targetHeat == 0:
                    int heat = HeatOn(opp, 1);
                    opp.heat += heat;
                    Emit(opp, "heat_applied", $"Térmico: {opp.name} recebe {heat} Calor (total {opp.heat}).", card: d.name, value: heat);
                    break;
                case "Elétrico" when d.overload == 0 && opp.overload == 0:
                    opp.overload = 1;
                    Emit(opp, "overload_applied", $"Elétrico: {opp.name} recebe Sobrecarga por 1 turno.", card: d.name, value: 1);
                    break;
                case "Químico" when d.corrosion == 0 && slot != Slot.Chassi:
                    opp.parts[slot].corrosion += 1;
                    Emit(opp, "corrosion_applied", $"Químico: {opp.name} recebe 1 Corrosão em {SlotName(slot)}.", card: d.name, target: SlotName(slot), value: 1);
                    break;
            }
        }

        // Calor from attacks; doubled on a "heatDouble" arena.
        int HeatOn(Robot target, int amount) => TerrainHits(target, "heatDouble") ? amount * 2 : amount;

        void Attack(Robot user, Robot def, Card c, Slot slot, int dmg, int hits, bool pierce, bool impact, bool chained, out bool broke, out int dealt)
        {
            var d = c.def;
            var part = slot == Slot.Chassi ? null : def.parts[slot];
            bool wasBroken = part?.Broken ?? false;
            var element = ElementOf(user, c);
            int mod = ElementModifier(part?.def, element);
            int kinetic = element == "Cinético" && (part == null || part.Broken) ? Robot.ElementImpact : 0;
            if (element == "Elétrico" && TerrainHits(def, "rainElectric")) kinetic += Robot.RainBonus; // first-hit bonus, same as Cinético
            int dodge = def.IsBroken(Slot.Pernas) ? 0 : def.dodge;
            def.dodge = 0;
            int total = 0, absorbed = 0, dodged = 0;
            for (int h = 0; h < hits; h++)
            {
                // Impacto: bonus against destroyed parts and the exposed chassis. Ponto Fraco: +weak on every hit.
                int amount = Math.Max(0, dmg + (impact && (part == null || part.Broken) ? Robot.ImpactBonus : 0) + (part?.weak ?? 0) + mod + (h == 0 ? kinetic : 0));
                int dg = Math.Min(dodge, amount); dodge -= dg; amount -= dg; dodged += dg;
                if (!pierce) { int a = Math.Min(def.armor, amount); def.armor -= a; amount -= a; absorbed += a; }
                DamagePart(def, slot, amount);
                total += amount;
            }
            dealt = total;
            Emit(user, "attack", $"{d.name}{(impact ? " (Encadear: Impacto)" : chained ? " (Encadear)" : "")}{(kinetic > 0 ? element == "Elétrico" ? $" ({terrain.name}: +{kinetic})" : " (Cinético: Impacto)" : "")}{(mod > 0 ? " (fraqueza)" : mod < 0 ? " (resistência)" : "")}: {def.name} sofre {total} em {SlotName(slot)}" +
                (absorbed > 0 ? $", {absorbed} na blindagem" : "") + (dodged > 0 ? $", {dodged} esquivado" : "") + ".",
                card: d.name, source: SlotName(c.source), target: SlotName(slot), value: total, absorbed: absorbed, dodged: dodged, chained: chained);
            history[history.Count - 1].element = element;
            broke = part != null && !wasBroken && part.Broken;
        }

        // What the enemy will draw for its next intent (Análise).
        public List<Card> EnemyNextHand => enemy.deck.Take(enemy.frame.hand).ToList();

        // Damage beyond a part's remaining integrity spills into the chassis only once it is exposed
        // (some part destroyed, this one included); otherwise it is lost. Random hits never pick the chassis.
        public void DamagePart(Robot r, Slot slot, int amount)
        {
            if (amount <= 0) return;
            if (slot == Slot.Chassi)
            {
                int ch = Math.Min(r.chassis, amount);
                r.chassis -= ch;
                if (ch > 0) Emit(r, "damage", $"{r.name}: chassi perde {ch} ({r.chassis}/{r.chassisMax}).", target: "Chassi", value: ch, ui: false);
                return;
            }
            var p = r.parts[slot];
            bool wasBroken = p.Broken, opened = r.ChassisOpen;
            int a = Math.Min(p.hp, amount); p.hp -= a;
            if (a > 0) Emit(r, "damage", $"{r.name}: {SlotName(slot)} perde {a} ({p.hp}/{p.Max}).", target: SlotName(slot), value: a, ui: false);
            if (!wasBroken && p.Broken)
            {
                Emit(r, "part_broken", $"{r.name}: {SlotName(slot)} avariada!", target: SlotName(slot));
                if (!opened) Emit(r, "chassis_open", $"{r.name}: chassi exposto!", target: "Chassi");
                if (slot == Slot.Pernas && r.dodge > 0) { r.dodge = 0; Emit(r, "dodge_cancelled", "Pernas avariadas: esquiva anulada."); }
            }
            int rest = amount - a;
            if (rest <= 0) return;
            if (r.ChassisOpen) DamagePart(r, Slot.Chassi, rest);
            else Emit(r, "overflow_lost", $"{rest} de dano perdido em {SlotName(slot)}.", target: SlotName(slot), value: rest, ui: false);
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
            Emit(enemy, "intent_execute", $"{enemy.name} executa: {IntentSummary()}.", cards: Names(enemyScraps.Concat(enemyIntent)), ui: false);
            foreach (var c in enemyScraps.ToList()) Scrap(enemy, c);
            foreach (var c in enemyIntent.ToList())
            {
                if (result != Result.None) break;
                if (!CanPlay(enemy, c)) { Emit(enemy, "intent_skip", $"{enemy.name} não consegue usar {c.def.name}.", card: c.def.name); continue; }
                Play(enemy, c, c.def.repair > 0 ? (Slot?)null : c.def.weakPoint > 0 ? enemyWeakTarget : enemyAimTarget);
            }
            if (result != Result.None) return;
            EndTurn(enemy);
            PlanEnemy();
            StartTurn(player, true);
        }

        public Slot enemyWeakTarget = Slot.Torso;

        // Draws the enemy hand, applies boss phases, then EnemyAI decides the whole turn up front so the intent shown is honest.
        void PlanEnemy()
        {
            Draw(enemy, enemy.frame.hand - enemy.hand.Count);
            CheckPhases();
            EnemyAI.Plan(this);
            Emit(enemy, "intent_reveal", $"Intenção: {IntentSummary()}.", cards: Names(enemyScraps.Concat(enemyIntent)), target: SlotName(enemyAimTarget), ui: false);
        }

        public string IntentSummary() => string.Join(", ",
            (enemyScraps.Count > 0 ? new[] { $"sucateia {string.Join(", ", Names(enemyScraps))}" } : new string[0]).Concat(enemyIntent.Select(IntentText)));

        // Boss hook: each phase triggers once when the chassis drops to its threshold.
        void CheckPhases()
        {
            while (enemy.phasesDone < enemy.phases.Length && enemy.Life <= enemy.phases[enemy.phasesDone].at * enemy.LifeMax)
            {
                var ph = enemy.phases[enemy.phasesDone++];
                enemy.phaseEnergy += ph.energy;
                enemy.phaseCards += ph.cards;
                enemy.armor += ph.armor;
                Emit(enemy, "boss_phase", $"{enemy.name}: {ph.text}", value: enemy.phasesDone);
                if (!string.IsNullOrEmpty(ph.signature))
                {
                    var sig = new Card(content.Card(ph.signature), Slot.Frame);
                    enemy.hand.Insert(0, sig);
                    enemy.forced = sig;
                }
            }
        }

        public string IntentText(Card c)
        {
            var d = c.def;
            bool aimed = enemyIntent.Any(x => x.def.aim) && d.TargetsPart && enemyIntent.First(x => x.def.TargetsPart) == c;
            string where = d.cost >= 2 || d.choose || aimed ? SlotName(enemyAimTarget) : "parte aleatória";
            if (d.armorDamage) return $"{d.name}: {d.DamageFor(enemy.armor)} em {where}";
            if (d.damage > 0) return $"{d.name}: {d.damage}{(d.hits > 1 ? "x" + d.hits : "")} em {where}{(d.secondPart ? " + outra parte" : "")}";
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
