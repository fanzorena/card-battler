using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace CardBattler.UI
{
    // Battle HUD: enemy top-right with intent icons, player bottom-left, info panels, combat log, hand, energy.
    // Visuals replay the battle log events so every hit, block and break gets feedback.
    public class BattleScreen
    {
        readonly GameUI app;
        readonly VisualElement root;
        readonly Battle b;
        RobotView playerBot, enemyBot;
        VisualElement hand, intents, playerPanel, enemyPanel, overlayLayer, scrapZone, logRows;
        ScrollView log;
        Label energyText, deckText, discardText, hint, scrapInfo;
        Button endTurn, cancel, quit;
        readonly Dictionary<Card, CardView> views = new Dictionary<Card, CardView>();
        Card pending;
        bool busy, quitArmed;
        int seen; // history events already shown

        const float RobotScale = 0.85f;
        static readonly Vector2 EnemyPos = new Vector2(1450, 330), PlayerPos = new Vector2(430, 565);
        static readonly Vector2 DeckPos = new Vector2(1370, 1010), DiscardPos = new Vector2(1520, 1010), EnergyPos = new Vector2(120, 780);
        static readonly Vector2 ScrapPos = new Vector2(130, 962), ScrapSize = new Vector2(220, 170);
        const float HandTop = 760;
        const float PlayLine = 730; // dropping a card above this line (stage y) plays it

        public enum DropKind { Cancel, Scrap, Play, PlayAt, ChooseTarget }

        // What releasing a dragged card means (pure: used by the screen and tests).
        // enemySlot/ownSlot: valid part under the pointer, if any.
        public static (DropKind kind, Slot? slot) Decide(Target needs, bool overScrap, Slot? enemySlot, Slot? ownSlot, float stageY)
        {
            if (overScrap) return (DropKind.Scrap, null);
            if (needs == Target.EnemyPart && enemySlot.HasValue) return (DropKind.PlayAt, enemySlot);
            if (needs == Target.OwnPart && ownSlot.HasValue) return (DropKind.PlayAt, ownSlot);
            if (stageY < PlayLine) return needs == Target.None ? (DropKind.Play, null) : (DropKind.ChooseTarget, null);
            return (DropKind.Cancel, null);
        }

        public BattleScreen(GameUI app, VisualElement root, Battle battle) { this.app = app; this.root = root; b = battle; }

        public static Slot? SlotFrom(string name) =>
            Enum.GetValues(typeof(Slot)).Cast<Slot>().Where(s => Battle.SlotName(s) == name).Select(s => (Slot?)s).FirstOrDefault();

        // Intent chips for the enemy's planned turn (pure: used by the UI and tests).
        public static List<(CardKind kind, string text, Slot? target)> IntentChips(Battle b)
        {
            var list = new List<(CardKind, string, Slot?)>();
            if (b.enemyScraps.Count > 0) list.Add((CardKind.Other, $"SUCATA ×{b.enemyScraps.Count}", null));
            bool aimed = false;
            foreach (var c in b.enemyIntent)
            {
                var d = c.def;
                var k = Kinds.Of(d);
                if (d.aim) aimed = true;
                if (k == CardKind.Attack)
                {
                    bool chooses = d.cost >= 2 || d.choose || aimed;
                    list.Add((k, $"{Kinds.Value(d, b.enemy.armor)} →{(chooses ? "" : " ?")}", chooses ? b.enemyAimTarget : (Slot?)null));
                    aimed = false; // Mirar is spent by the next attack
                }
                else list.Add((k, k == CardKind.Armor ? $"+{Kinds.Value(d)}" : d.name, null));
            }
            return list;
        }

        public void Build()
        {
            root.AddToClassList("battle");
            if (b.terrain != null) root.AddToClassList("terrain-" + b.terrain.id);
            Theme.El(root, "Floor", "battle-floor").Place(0, 640, 1920, 440);

            enemyPanel = Theme.El(root, "EnemyPanel").Place(40, 30, 640, 330);
            playerPanel = Theme.El(root, "PlayerPanel").Place(1240, 545, 640, 330);

            // not mirrored: the left arm is always on the left, like its icon
            enemyBot = RobotView.Create(root, false, true).At(EnemyPos.x, EnemyPos.y, RobotScale);
            enemyBot.Build(b.content, Def(b.enemy));
            playerBot = RobotView.Create(root, false, true).At(PlayerPos.x, PlayerPos.y, RobotScale);
            playerBot.Build(b.content, Def(b.player));

            Theme.Label(root, "INTENÇÃO DO INIMIGO", 15, Theme.TextDim, TextAnchor.MiddleRight, FontStyle.Bold).Place(1040, 4, 820, 22);
            intents = Theme.Row(root, "Intents", 10).Place(1040, 26, 840, 50);
            intents.style.justifyContent = Justify.FlexEnd;

            // Combat log: newest line at the bottom, scrolls back through the whole fight.
            var tb = Theme.Plate(root, "TextBox", "panel-glass").Place(700, 470, 520, 260);
            Theme.Label(tb, "REGISTRO", 14, Theme.TextDim, style: FontStyle.Bold).At(16, 8);
            log = Theme.Scroll(tb, "Log").Place(10, 30, 500, 222);
            logRows = log.contentContainer;

            hand = Theme.El(root, "Hand").Place(0, HandTop, 1920, 320);

            deckText = Pile(DeckPos, "DECK", "Cartas que você ainda vai comprar. Acabou? O descarte é embaralhado de volta.");
            discardText = Pile(DiscardPos, "DESCARTE", "Cartas jogadas, sucateadas e as que sobraram na mão no fim do turno.");

            var orb = Theme.Box(root, "Energy", Theme.Mix(Theme.Bg, Theme.Energy, 0.15f), Shape.Circle).Center(EnergyPos.x, EnergyPos.y, 116, 116);
            orb.AddToClassList("energy-orb");
            Theme.Border(orb, Theme.Energy, 4);
            energyText = Theme.Title(orb, "", 50, Theme.Energy, TextAnchor.MiddleCenter);
            energyText.Fill();
            Theme.Hover(orb, Theme.Explain("energia"));
            Theme.Title(root, "ENERGIA", Theme.Small, Theme.Energy, TextAnchor.MiddleCenter).Center(EnergyPos.x, EnergyPos.y - 76, 180, 26);

            // Scrap zone: drop a card here to scrap it.
            scrapZone = Theme.El(root, "ScrapZone", "scrap-zone").Center(ScrapPos.x, ScrapPos.y, ScrapSize.x, ScrapSize.y);
            scrapZone.pickingMode = PickingMode.Position;
            Theme.Icon(scrapZone, "trashcan", 52, Theme.Hex("E2C49E")).At(14, 16);
            Theme.Title(scrapZone, "SUCATA", 24, Theme.Hex("E2C49E")).At(76, 18);
            Theme.Label(scrapZone, "arraste uma carta", 15, Theme.TextDim).At(78, 50);
            scrapInfo = Theme.Label(scrapZone, "", Theme.Small, Theme.Energy, TextAnchor.UpperLeft, FontStyle.Bold).Place(14, 90, 196, 74);
            Theme.Hover(scrapZone, Theme.Explain("sucata"));

            endTurn = Theme.Primary(root, "ENCERRAR TURNO", EndTurn, 26);
            endTurn.name = "EncerrarTurno";
            endTurn.Place(1600, 944, 300, 86);
            Theme.Hover(endTurn, "O inimigo faz o que mostrou na intenção; as cartas que sobraram vão para o descarte.");
            quit = Theme.Button(root, "Desistir", Quit, null, Theme.Small, "btn-small");
            quit.Place(1760, 1036, 140, 40);

            hint = Theme.Title(root, "", 24, Theme.Warn, TextAnchor.MiddleCenter).Classes("outlined").Center(960, 420, 1000, 40);
            cancel = Theme.Button(root, "Cancelar", CancelTarget, null, Theme.Small, "btn-small").Center(960, 466, 170, 44);
            cancel.Show(false);

            overlayLayer = Theme.El(root, "Overlay").Fill();

            seen = b.history.Count;
            if (b.terrain != null)
            {
                var chip = Theme.Chip(root, "ARENA: " + b.terrain.name.ToUpperInvariant(), Theme.Exposed, 16);
                chip.parent.name = "Terrain";
                chip.parent.Center(960, 440, 380, 34);
                Theme.Hover(chip.parent, $"{b.terrain.name} ({b.terrain.place}): {b.terrain.text} Pernas Hover ignoram o terreno.");
            }
            Say($"Luta contra {b.enemy.name} ({b.enemy.tier}){(b.terrain == null ? "" : $" · arena: {b.terrain.name}, {b.terrain.text}")}.");
            Refresh(animateDraw: true);
            Banner("SEU TURNO", Theme.Good);
        }

        Label Pile(Vector2 pos, string title, string help)
        {
            var box = Theme.El(root, "Pile:" + title, "pile").Center(pos.x, pos.y, 140, 76);
            Theme.Icon(box, "cards", 30, Theme.TextDim).At(10, 8);
            Theme.Label(box, title, 14, Theme.TextDim, style: FontStyle.Bold).At(46, 10);
            var n = Theme.Title(box, "", 30, Theme.Ink).At(46, 30);
            Theme.Hover(box, help);
            return n;
        }

        static RobotDef Def(Robot r) => new RobotDef
        {
            frame = r.frame.id, head = r.parts[Slot.Cabeca].def.id, torso = r.parts[Slot.Torso].def.id, armL = r.parts[Slot.BracoEsq].def.id,
            armR = r.parts[Slot.BracoDir].def.id, legs = r.parts[Slot.Pernas].def.id, core = r.core.id,
        };

        // ---- Panels ----
        void Panel(VisualElement parent, Robot r, bool enemy)
        {
            parent.Clear();
            var bg = Theme.Plate(parent, "Bg").Fill();
            string tier = enemy ? $"  <color=#{Theme.HexOf(GarageScreen.TierColor(r.tier))}>{r.tier?.ToUpperInvariant()}</color>" : "";
            Theme.Label(bg, $"<b>{r.name}</b>{tier}", 26).Place(18, 8, 604, 34).OneLine();
            var makers = r.makers.Length == 0 ? "" : " · bônus " + Content.MakerNames(r.makers);
            Theme.Label(bg, $"{r.frame.name}{makers}", 17, Theme.TextDim).Place(18, 42, 604, 24).OneLine();

            var (cf, cl) = Theme.Bar(bg, r.ChassisOpen ? Theme.Exposed : Theme.Hex("7C86A6"), Theme.Body);
            cf.parent.Place(18, 72, 604, 40);
            cf.parent.EnableInClassList("exposed", r.ChassisOpen);
            Theme.SetFill(cf, (float)r.chassis / Mathf.Max(1, r.chassisMax));
            cl.text = $"{r.chassis}/{r.chassisMax} · {(r.ChassisOpen ? "EXPOSTO" : "PROTEGIDO")}";
            cl.style.paddingLeft = 48;
            SlotIcons.Icon(cf.parent, "Chassi", 34, Theme.Ink).Place(3, 3, 34, 34);
            Theme.Hover(cf.parent, Theme.Explain("chassi"));

            float y = 118;
            foreach (var p in r.parts.Values)
            {
                var color = p.Broken ? Theme.Hex("4A4E5C") : Theme.Maker(p.def.maker);
                var (f, l) = Theme.Bar(bg, Theme.Mix(Theme.Panel, color, p.Broken ? 1 : 0.55f), 17);
                f.parent.Place(18, y, 604, 34);
                f.parent.EnableInClassList("broken", p.Broken);
                Theme.SetFill(f, (float)Mathf.Max(0, p.hp) / p.Max);
                l.text = $"{p.def.name}  {(p.Broken ? "— AVARIADA" : $"{p.hp}/{p.Max}")}";
                l.style.paddingLeft = 46;
                SlotIcons.Icon(f.parent, p.slot, 30, p.Broken ? Theme.Bad : Theme.Maker(p.def.maker)).Place(3, 2, 30, 30);
                y += 40;
            }
        }

        void Intents()
        {
            intents.Clear();
            foreach (var (kind, text, target) in IntentChips(b))
            {
                var color = Kinds.ColorOf(kind);
                var chip = Theme.Row(intents, "Intent", 6);
                chip.AddToClassList("intent");
                chip.style.backgroundColor = Theme.Alpha(Theme.Mix(Theme.Bg, color, 0.22f), 0.95f);
                Theme.Border(chip, Theme.Alpha(color, 0.9f), 2);
                Kinds.Icon(chip, kind, 30);
                Theme.Title(chip, text, 20, color, TextAnchor.MiddleLeft).style.whiteSpace = WhiteSpace.NoWrap;
                if (target.HasValue) SlotIcons.Icon(chip, target.Value == Slot.Chassi ? "Chassi" : SlotIcons.Key(target.Value), 38, color);
                Theme.Hover(chip, kind == CardKind.Attack ? (target.HasValue ? "Ataque mirado na parte mostrada." : "Ataque numa parte aleatória (?).") : "Ação planejada pelo inimigo para o próximo turno.");
            }
        }

        // ---- Refresh ----
        void Refresh(bool animateDraw = false)
        {
            Panel(enemyPanel, b.enemy, true);
            Panel(playerPanel, b.player, false);
            enemyBot.Show(b.enemy);
            playerBot.Show(b.player);
            Intents();
            energyText.text = b.player.energy.ToString();
            deckText.text = b.player.deck.Count.ToString();
            discardText.text = b.player.discard.Count.ToString();
            endTurn.SetEnabled(!busy && b.result == Result.None && pending == null);
            // nudge the end-turn button when nothing in hand can be played
            endTurn.EnableInClassList("nudge", !busy && pending == null && b.result == Result.None && !b.player.hand.Any(c => b.CanPlay(b.player, c)));
            var p = b.player;
            bool swap = p.frame.passive == "scrapDraw" && !p.scrappedThisTurn;
            int scrapArmor = p.BonusValue("scrapArmor");
            scrapInfo.text = (swap ? "COMPRA 1 CARTA\n<size=15>(1ª sucata do Vira-lata)</size>" : "+1 ENERGIA") + (scrapArmor > 0 ? $"\n<color=#{Theme.HexOf(Theme.Armor)}>+{scrapArmor} BLINDAGEM</color>" : "");
            Hand(animateDraw);
            if (b.result != Result.None) ShowResult();
        }

        void Hand(bool animateDraw)
        {
            foreach (var gone in views.Keys.Where(c => !b.player.hand.Contains(c)).ToList())
            {
                views[gone].Root.RemoveFromHierarchy();
                views.Remove(gone);
            }
            var p = b.player;
            int n = p.hand.Count;
            float spacing = Mathf.Min(CardView.W + 16, 1000f / Mathf.Max(1, n));
            float start = 790 - spacing * (n - 1) / 2;
            int i = 0;
            foreach (var c in p.hand)
            {
                bool isNew = !views.TryGetValue(c, out var v);
                if (isNew)
                {
                    var part = c.source == Slot.Frame ? null : p.parts[c.source].def;
                    v = CardView.Create(hand, c.def, part?.maker, SlotIcons.Key(c.source), c, part?.element);
                    v.onClick = Clicked;
                    v.onDragStart = DragStart;
                    v.onDragMove = DragMove;
                    v.onDrop = Drop;
                    views[c] = v;
                }
                // slight fan: cards at the edges sit lower and tilt outwards
                float k = n <= 1 ? 0 : (i - (n - 1) / 2f) / ((n - 1) / 2f);
                var home = new Vector2(start + i * spacing, 160 + Mathf.Abs(k) * 14);
                v.Root.style.rotate = new Rotate(k * 4);
                if (isNew && animateDraw && Application.isPlaying && !Fx.Reduced)
                {
                    v.SetHome(new Vector2(DeckPos.x, DeckPos.y - HandTop));
                    var target = home;
                    var view = v;
                    view.animating = true;
                    view.Root.style.opacity = 0;
                    Fx.Delay(0.06f * i, () =>
                    {
                        view.Root.style.opacity = 1;
                        Sfx.Play(s => s.card, 0.35f, UnityEngine.Random.Range(0.95f, 1.1f));
                        Fx.Move(view.Root, target - new Vector2(CardView.W / 2, CardView.H / 2), 0.3f, () => { view.SetHome(target); view.animating = false; });
                    });
                }
                else if (!v.animating) v.SetHome(home);
                bool chains = p.lastPlayed is Slot last && last != c.source && IsLimb(last) && IsLimb(c.source)
                              && (c.def.chainImpact || c.def.chainDraw > 0 || (p.frame.passive == "chainEnergy" && !p.chainEnergyUsed));
                v.SetState(!busy && pending == null && b.CanPlay(p, c), p.CostOf(c), Kinds.TargetBadge(c.def, p.Chooses(c.def)), chains, p.IsBroken(c.source));
                i++;
            }
        }

        static bool IsLimb(Slot s) => s == Slot.BracoEsq || s == Slot.BracoDir || s == Slot.Pernas;

        // ---- Player actions ----
        void Clicked(CardView v)
        {
            if (busy || pending != null) return;
            var t = b.NeedsTarget(b.player, v.card);
            if (t == Target.None) { Play(v.card, null); return; }
            pending = v.card;
            var robot = t == Target.OwnPart ? playerBot : enemyBot;
            robot.SetTargets(ValidFor(v.card, t), s => { robot.SetTargets(null, null); var c = pending; pending = null; Play(c, s); });
            hint.text = t == Target.OwnPart ? "Escolha uma parte sua para reparar" : b.enemy.ChassisOpen && v.card.def.weakPoint == 0
                ? "Escolha uma parte do inimigo — ou o CHASSI EXPOSTO" : "Escolha uma parte do inimigo";
            cancel.Show(true);
            Refresh();
        }

        // ---- Drag & drop ----
        Func<Slot, bool> ValidFor(Card c, Target t) =>
            t == Target.OwnPart ? (s => s != Slot.Chassi)
            : c.def.weakPoint > 0 ? (Func<Slot, bool>)(s => s != Slot.Chassi && Battle.ValidTarget(b.enemy, s))
            : s => Battle.ValidTarget(b.enemy, s);

        void DragStart(CardView v)
        {
            if (busy || pending != null) return;
            scrapZone.AddToClassList("armed");
            var t = b.NeedsTarget(b.player, v.card);
            if (t == Target.EnemyPart) enemyBot.SetTargets(ValidFor(v.card, t), null);
            if (t == Target.OwnPart) playerBot.SetTargets(ValidFor(v.card, t), null);
            hint.text = t == Target.None ? "Solte acima da mão para jogar · na SUCATA para sucatear" : "Solte numa parte para jogar · na SUCATA para sucatear";
        }

        void DragMove(CardView v, Vector2 stagePos, Vector2 panelPos) =>
            scrapZone.EnableInClassList("hot", scrapZone.worldBound.Contains(panelPos));

        void Drop(CardView v, Vector2 stagePos, Vector2 panelPos)
        {
            scrapZone.RemoveFromClassList("armed");
            scrapZone.RemoveFromClassList("hot");
            enemyBot.SetTargets(null, null); playerBot.SetTargets(null, null);
            hint.text = "";
            if (busy || pending != null || b.result != Result.None) { v.ReturnToHand(); return; }
            var t = b.NeedsTarget(b.player, v.card);
            bool overScrap = scrapZone.worldBound.Contains(panelPos);
            var valid = ValidFor(v.card, t);
            Slot? enemySlot = enemyBot.SlotAt(panelPos), ownSlot = playerBot.SlotAt(panelPos);
            if (enemySlot.HasValue && !valid(enemySlot.Value)) enemySlot = null;
            if (ownSlot.HasValue && !valid(ownSlot.Value)) ownSlot = null;
            var (kind, slot) = Decide(t, overScrap, enemySlot, ownSlot, stagePos.y);
            bool canPlay = b.CanPlay(b.player, v.card);
            switch (kind)
            {
                case DropKind.Scrap: Scrap(v.card); break;
                case DropKind.PlayAt when canPlay: Play(v.card, slot); break;
                case DropKind.Play when canPlay: Play(v.card, null); break;
                case DropKind.ChooseTarget when canPlay: v.ReturnToHand(); Clicked(v); break;
                default:
                    v.ReturnToHand();
                    if (kind != DropKind.Cancel) { Fx.Shake(v.Root, 6, 0.15f); Sfx.Play(s => s.error, 0.5f); }
                    break;
            }
        }

        void CancelTarget()
        {
            pending = null;
            enemyBot.SetTargets(null, null); playerBot.SetTargets(null, null);
            hint.text = ""; cancel.Show(false);
            Refresh();
        }

        void Play(Card c, Slot? target)
        {
            hint.text = ""; cancel.Show(false);
            var v = views.TryGetValue(c, out var view) ? view : null;
            views.Remove(c);
            if (!b.Play(b.player, c, target)) { v?.Root.RemoveFromHierarchy(); Refresh(); return; }
            Sfx.Play(s => s.card, 0.6f);
            var aimAt = c.def.TargetsPart ? enemyBot.PosOf(target ?? Slot.Torso) : c.def.repair > 0 ? playerBot.PosOf(target ?? Slot.Torso) : Fx.PosOf(playerBot.Root);
            v?.FlyAway(aimAt, 0.4f);
            busy = true;
            Fx.Run(ReplayEvents(fast: true, () => { busy = false; Refresh(); }));
        }

        void Scrap(Card c)
        {
            if (busy || pending != null || b.result != Result.None) return;
            var v = views.TryGetValue(c, out var view) ? view : null;
            views.Remove(c);
            if (!b.Scrap(b.player, c)) return;
            Sfx.Play(s => s.scrap, 0.7f);
            v?.FlyAway(ScrapPos, 0.2f);
            Fx.Float(EnergyPos + new Vector2(0, -60), b.player.frame.passive == "scrapDraw" && b.history.Last(e => e.evt == "scrap").value == 0 ? "+1 CARTA" : "+1", Theme.Energy, 34);
            busy = true;
            Fx.Run(ReplayEvents(fast: true, () => { busy = false; Refresh(animateDraw: true); }));
        }

        void EndTurn()
        {
            if (busy || pending != null || b.result != Result.None) return;
            busy = true;
            foreach (var v in views.Values) v.FlyAway(DiscardPos, 0.3f);
            views.Clear();
            Banner("TURNO DO INIMIGO", Theme.Bad);
            b.EndPlayerTurn();
            Fx.Run(ReplayEvents(fast: false, () =>
            {
                busy = false;
                Refresh(animateDraw: true);
                if (b.result == Result.None) Banner("SEU TURNO", Theme.Good);
            }));
        }

        void Quit()
        {
            if (!quitArmed)
            {
                quitArmed = true;
                Theme.SetLabel(quit, "Confirmar?");
                quit.AddToClassList("btn-danger");
                Say("<color=#FFC24A>Clique em Desistir de novo para sair da luta.</color>");
                Fx.Delay(3, () => { quitArmed = false; Theme.SetLabel(quit, "Desistir"); quit.RemoveFromClassList("btn-danger"); });
                return;
            }
            if (app.afterBattle != null) app.afterBattle(b); // a job fight given up counts as a loss
            else app.ShowOpponents();
        }

        // ---- Feedback from log events ----
        IEnumerator ReplayEvents(bool fast, Action done)
        {
            var events = b.history.Skip(seen).ToList();
            seen = b.history.Count;
            if (fast) yield return new WaitForSeconds(0.2f);
            foreach (var e in events)
            {
                var bot = e.actor == "player" ? playerBot : enemyBot;
                var other = e.actor == "player" ? enemyBot : playerBot;
                float wait = 0;
                switch (e.evt)
                {
                    case "play":
                        if (e.actor == "enemy") { Say($"<color=#FF8A8A>{e.msg}</color>", e.source); wait = 0.35f; }
                        else Say(e.msg, e.source);
                        break;
                    case "scrap": if (e.actor == "enemy") { Fx.Float(Fx.PosOf(bot.Root), "SUCATA", Theme.Hex("C8A27A")); Say(e.msg); wait = 0.2f; } break;
                    case "attack":
                    {
                        lastElement = e.element;
                        var from = bot.PosOf(SlotFrom(e.source) ?? Slot.Torso);
                        var to = other.PosOf(SlotFrom(e.target) ?? Slot.Chassi);
                        Fx.Lunge(bot.Root, Mathf.Sign(to.x - from.x) * 46);
                        Fx.Projectile(from, to, HitColor(e.element));
                        Sfx.Play(s => s.hit, 0.6f, UnityEngine.Random.Range(0.9f, 1.1f));
                        if (!Fx.Reduced) yield return new WaitForSeconds(fast ? 0.1f : 0.18f);
                    }
                        if (e.absorbed > 0) { Fx.Float(Fx.PosOf(other.Root) + new Vector2(0, -120), $"-{e.absorbed} BLINDAGEM", Theme.Armor, 26); Sfx.Play(s => s.armor, 0.5f); }
                        if (e.dodged > 0) Fx.Float(Fx.PosOf(other.Root) + new Vector2(0, -160), "ESQUIVOU", Theme.Hex("9EF0FF"), 26);
                        if (e.chained) Fx.Float(Fx.PosOf(bot.Root) + new Vector2(0, -200), "ENCADEAR!", Theme.Hex("FFD34D"), 28);
                        Say(e.msg, e.target);
                        break;
                    case "damage":
                    {
                        var slot = SlotFrom(e.target) ?? Slot.Chassi;
                        bot.Show(e.actor == "player" ? e.player : e.enemy, e.actor == "player" ? b.player : b.enemy);
                        bot.Hit(slot);
                        Sfx.Play(s => slot == Slot.Chassi ? s.heavyHit : s.hit, 0.7f);
                        if (lastElement != null) Fx.Burst(bot.PosOf(slot), HitColor(lastElement), 8, 180);
                        Fx.Float(bot.PosOf(slot), "-" + e.value, slot == Slot.Chassi ? Theme.Exposed : Theme.Bad, 40, slot == Slot.Chassi ? "Chassi" : SlotIcons.Key(slot));
                        wait = fast ? 0.08f : 0.18f;
                        break;
                    }
                    case "part_broken":
                    {
                        var slot = SlotFrom(e.target) ?? Slot.Torso;
                        Fx.Burst(bot.PosOf(slot), Theme.Warn, 18);
                        Fx.Float(bot.PosOf(slot) + new Vector2(0, -40), "AVARIADA!", Theme.Warn, 30);
                        Sfx.Play(s => s.broken, 0.8f);
                        Say($"<color=#FFC24A>{e.msg}</color>", e.target);
                        wait = 0.25f;
                        break;
                    }
                    case "chassis_open": Fx.Float(bot.PosOf(Slot.Chassi) + new Vector2(0, 50), "CHASSI EXPOSTO", Theme.Exposed, 30); Say($"<color=#FF8A3D>{e.msg}</color>", "Chassi"); break;
                    case "armor": case "bonus":
                        if (e.value > 0 && (e.evt == "armor" || e.target == "Ferrovelho" || e.target == "Bastion")) { Fx.Float(Fx.PosOf(bot.Root) + new Vector2(0, -170), $"+{e.value} BLINDAGEM", Theme.Armor, 26); Sfx.Play(s => s.armor, 0.5f); }
                        break;
                    case "repair": Fx.Float(bot.PosOf(SlotFrom(e.target) ?? Slot.Torso), "+" + e.value, Theme.Good, 36); Sfx.Play(s => s.repair, 0.6f); Say(e.msg, e.target); break;
                    case "heat_applied": Status(bot, Slot.Torso, $"+{e.value} CALOR", "FF6A3D"); Say(e.msg, e.target); break;
                    case "overload_applied": Status(bot, Slot.Cabeca, "SOBRECARGA", "FFE14D"); Say(e.msg, e.target); break;
                    case "corrosion_applied": Status(bot, SlotFrom(e.target) ?? Slot.Torso, $"+{e.value} CORROSÃO", "7CFF6B"); Say(e.msg, e.target); break;
                    case "corrosion_tick": Fx.Burst(bot.PosOf(SlotFrom(e.target) ?? Slot.Torso), Theme.Hex("7CFF6B"), 6, 120); break;
                    case "heat_tick": Status(bot, Slot.Torso, $"+{e.value} CALOR", "FF6A3D", 22); break;
                    case "overload_penalty": case "heat_penalty": Status(bot, Slot.Chassi, "-1 ENERGIA", e.evt == "heat_penalty" ? "FF6A3D" : "FFE14D", 24); Say(e.msg); break;
                    case "dodge_skid": Status(bot, Slot.Pernas, "DERRAPOU", "C8A27A", 24); Say(e.msg); break;
                    case "weak_point": case "intent_cancel": case "boss_phase":
                        Say(e.evt == "boss_phase" ? $"<color=#FF5A6A><b>{e.msg}</b></color>" : e.msg, e.target);
                        if (e.evt == "boss_phase") { Banner("FASE " + (e.value + 1), Theme.Bad); wait = 0.6f; }
                        break;
                    case "battle_end": Say($"<b>{e.msg.Split('.')[0]}.</b>"); break;
                }
                if (wait > 0) yield return new WaitForSeconds(wait);
            }
            yield return new WaitForSeconds(fast ? 0.1f : 0.3f);
            done?.Invoke();
        }

        string lastElement; // element of the attack being replayed, for the hit sparks

        static Color HitColor(string element) => string.IsNullOrEmpty(element) ? Theme.Hex("FFE2B0") : Theme.Element(element);

        // Status feedback: a small colored float next to the part, wait-free.
        static void Status(RobotView bot, Slot slot, string text, string hex, int size = 26) =>
            Fx.Float(bot.PosOf(slot) + new Vector2(0, -60), text, Theme.Hex(hex), size);

        // Log line: the part's name is replaced by its icon at the start of the line.
        void Say(string line, string slotName = null)
        {
            var slot = string.IsNullOrEmpty(slotName) ? null : SlotFrom(slotName);
            var row = Theme.Row(logRows, "Line", 8);
            row.AddToClassList("log-line");
            if (slot.HasValue) SlotIcons.Icon(row, slot == Slot.Chassi ? "Chassi" : SlotIcons.Key(slot.Value), 28, Theme.Ink);
            var t = Theme.Label(row, StripSlotNames(line), Theme.Small, Theme.Ink);
            t.style.flexShrink = 1;
            foreach (var old in logRows.Children().Where(r => r != row)) old.AddToClassList("old");
            Fx.Enter(row, 0, 10);
            log.schedule.Execute(() => log.scrollOffset = new Vector2(0, float.MaxValue)).StartingIn(10);
        }

        // "sofre 6 em Braço Dir" -> "sofre 6"; "(Braço Dir, custo 1)" -> "(custo 1)"; "Braço Dir avariada" -> "avariada".
        public static string StripSlotNames(string msg)
        {
            foreach (var s in new[] { Slot.Cabeca, Slot.Torso, Slot.BracoEsq, Slot.BracoDir, Slot.Pernas, Slot.Frame })
            {
                var n = Battle.SlotName(s);
                // only where the name refers to the part (card names like "Cabeça Dura" stay intact)
                msg = msg.Replace($" em {n}", "").Replace($"({n}, ", "(").Replace($": {n} ", ": ");
            }
            return msg;
        }

        void Banner(string text, Color color)
        {
            if (!Application.isPlaying) return;
            Sfx.Play(s => s.turn, 0.5f);
            var bar = Theme.El(overlayLayer, "Banner", "banner").Center(960, 420, 1920, 120);
            Theme.Title(bar, text, 56, color, TextAnchor.MiddleCenter).Classes("outlined").Fill();
            bar.style.opacity = 0;
            Fx.Fade(bar, 1, 0.15f, () => Fx.Delay(0.6f, () => Fx.Fade(bar, 0, 0.3f, () => bar.RemoveFromHierarchy())));
        }

        bool ending;

        // Victory/defeat: the loser collapses with sparks, then the result screen fades in.
        void ShowResult()
        {
            if (ending || overlayLayer.Q("Result") != null) return;
            ending = true;
            if (Fx.Reduced || !Application.isPlaying) { ResultOverlay(); return; }
            var loser = b.result == Result.Win ? enemyBot : playerBot;
            loser.idle = false;
            Fx.Burst(loser.PosOf(Slot.Chassi), Theme.Warn, 26, 340);
            Fx.Collapse(loser.Root, loser == enemyBot ? -1 : 1);
            Sfx.Play(s => b.result == Result.Win ? s.win : s.lose, 0.8f);
            Banner(b.result == Result.Win ? "VITÓRIA" : "DERROTA", b.result == Result.Win ? Theme.Good : Theme.Bad);
            Fx.Delay(1.2f, ResultOverlay);
        }

        void ResultOverlay()
        {
            if (overlayLayer.panel == null && Application.isPlaying || overlayLayer.Q("Result") != null) return; // the screen may have changed during the sequence
            bool win = b.result == Result.Win;
            var shade = Theme.El(overlayLayer, "Result", "shade").Fill();
            shade.pickingMode = PickingMode.Position;
            Theme.Title(shade, win ? "VITÓRIA" : "DERROTA", 110, win ? Theme.Good : Theme.Bad, TextAnchor.MiddleCenter).Classes("outlined").Center(960, 380, 1400, 150);
            Theme.Label(shade, $"{b.enemy.name} · turno {b.turn}", Theme.Title, Theme.TextDim, TextAnchor.MiddleCenter).Center(960, 480, 1200, 50);
            Fx.Enter(shade, 0, 0);
            if (app.afterBattle != null)
            {
                var after = app.afterBattle;
                var cont = Theme.Primary(shade, "CONTINUAR  →", () => after(b), 30);
                cont.name = "Continuar";
                cont.Center(960, 600, 380, 86);
                return;
            }
            var build = app.Garage.Current;
            var enemyId = app.lastEnemy;
            var row = Theme.Row(shade, "Actions", 24).Center(960, 600, 1060, 80);
            row.style.justifyContent = Justify.Center;
            Theme.Primary(row, "REVANCHE", () => app.StartBattle(build, enemyId), 26).Size(320, 80);
            Theme.Button(row, "OUTRO ADVERSÁRIO", app.ShowOpponents, null, 24).Size(320, 80);
            Theme.Button(row, "VOLTAR À GARAGEM", app.ShowGarage, null, 24).Size(320, 80);
        }
    }
}
