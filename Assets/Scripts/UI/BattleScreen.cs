using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattler.UI
{
    // Battle HUD: enemy top-right with intent icons, player bottom-left, info panels, text box, hand, energy.
    // Visuals replay the battle log events so every hit, block and break gets feedback.
    public class BattleScreen
    {
        readonly GameUI app;
        readonly RectTransform root;
        readonly Battle b;
        RobotView playerBot, enemyBot;
        RectTransform hand, intents, playerPanel, enemyPanel, overlayLayer, textRows, scrapZone;
        Text energyText, deckText, discardText, hint, scrapInfo;
        Image scrapGlow;
        Button endTurn, cancel, quit;
        readonly Dictionary<Card, CardView> views = new Dictionary<Card, CardView>();
        readonly List<(string line, string icon)> lines = new List<(string, string)>();
        Card pending;
        bool busy, quitArmed;
        int seen; // history events already shown

        const float RobotScale = 0.85f;
        static readonly Vector2 EnemyPos = new Vector2(1450, 330), PlayerPos = new Vector2(430, 565);
        static readonly Vector2 DeckPos = new Vector2(1370, 1010), DiscardPos = new Vector2(1520, 1010), EnergyPos = new Vector2(120, 780);
        static readonly Vector2 ScrapPos = new Vector2(130, 962), ScrapSize = new Vector2(220, 170);
        const float PlayLine = 730; // dropping a card above this line (overlay y) plays it

        public enum DropKind { Cancel, Scrap, Play, PlayAt, ChooseTarget }

        // What releasing a dragged card means (pure: used by the screen and tests).
        // enemySlot/ownSlot: valid part under the pointer, if any.
        public static (DropKind kind, Slot? slot) Decide(Target needs, bool overScrap, Slot? enemySlot, Slot? ownSlot, float overlayY)
        {
            if (overScrap) return (DropKind.Scrap, null);
            if (needs == Target.EnemyPart && enemySlot.HasValue) return (DropKind.PlayAt, enemySlot);
            if (needs == Target.OwnPart && ownSlot.HasValue) return (DropKind.PlayAt, ownSlot);
            if (overlayY < PlayLine) return needs == Target.None ? (DropKind.Play, null) : (DropKind.ChooseTarget, null);
            return (DropKind.Cancel, null);
        }

        public BattleScreen(GameUI app, RectTransform root, Battle battle) { this.app = app; this.root = root; b = battle; }

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
            enemyPanel = Theme.Node(root, "EnemyPanel").Place(40, 30, 640, 330);
            playerPanel = Theme.Node(root, "PlayerPanel").Place(1240, 545, 640, 330);

            enemyBot = RobotView.Create(root, false, true); // not mirrored: left arm is always on the left, like its icon
            enemyBot.Rect.Center(EnemyPos.x, EnemyPos.y, RobotView.W, RobotView.H);
            enemyBot.transform.localScale = new Vector3(RobotScale, RobotScale, 1);
            enemyBot.Build(b.content, Def(b.enemy));
            playerBot = RobotView.Create(root, false, true);
            playerBot.Rect.Center(PlayerPos.x, PlayerPos.y, RobotView.W, RobotView.H);
            playerBot.transform.localScale = new Vector3(RobotScale, RobotScale, 1);
            playerBot.Build(b.content, Def(b.player));

            intents = Theme.Node(root, "Intents").Place(1040, 26, 820, 50);
            var hl = intents.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10; hl.childAlignment = TextAnchor.MiddleCenter; hl.childControlWidth = false; hl.childControlHeight = false; hl.childForceExpandWidth = false;

            var tb = Theme.Box(root, "TextBox", Theme.Alpha(Theme.Bg, 0.88f));
            tb.rectTransform.Place(700, 480, 520, 250);
            tb.gameObject.AddComponent<Outline>().effectColor = Theme.Alpha(Theme.Line, 1);
            textRows = Theme.Node(tb.transform, "Rows").Fill(12); // last lines, each with the icon of the part involved

            hand = Theme.Node(root, "Hand").Place(0, 760, 1920, 320);

            var deckChip = Theme.Chip(root, "", Theme.TextDim, Theme.Body);
            ((RectTransform)deckChip.transform.parent).Center(DeckPos.x, DeckPos.y, 140, 70);
            deckText = deckChip;
            var discardChip = Theme.Chip(root, "", Theme.TextDim, Theme.Body);
            ((RectTransform)discardChip.transform.parent).Center(DiscardPos.x, DiscardPos.y, 140, 70);
            discardText = discardChip;

            var orb = Theme.Box(root, "Energy", Theme.Mix(Theme.Bg, Theme.Energy, 0.15f), Theme.Circle);
            orb.rectTransform.Center(EnergyPos.x, EnergyPos.y, 110, 110);
            Theme.Box(orb.transform, "Ring", Theme.Energy, Theme.Ring).rectTransform.Fill();
            energyText = Theme.Label(orb.transform, "", 52, Theme.Energy, TextAnchor.MiddleCenter, FontStyle.Bold);
            energyText.rectTransform.Fill();
            Theme.Label(root, "ENERGIA", Theme.Small, Theme.Energy, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Center(EnergyPos.x, EnergyPos.y - 70, 160, 26);

            // Scrap zone: drop a card here to scrap it.
            var zone = Theme.Box(root, "ScrapZone", Theme.Alpha(Theme.Mix(Theme.Bg, Theme.Hex("C8A27A"), 0.18f), 0.95f), raycast: true);
            zone.rectTransform.Center(ScrapPos.x, ScrapPos.y, ScrapSize.x, ScrapSize.y);
            scrapZone = zone.rectTransform;
            scrapGlow = Theme.Box(zone.transform, "Glow", Theme.Alpha(Theme.Hex("C8A27A"), 0.5f), Theme.Frame);
            scrapGlow.rectTransform.Fill(-4);
            var bin = Theme.Node(zone.transform, "Bin").Place(16, 18, 54, 60);
            Theme.Box(bin, "Can", Theme.Hex("C8A27A")).rectTransform.Place(6, 14, 42, 46);
            Theme.Box(bin, "Lid", Theme.Hex("E2C49E")).rectTransform.Place(0, 4, 54, 10);
            for (int k = 0; k < 3; k++) Theme.Solid(bin, "Rib", Theme.Alpha(Theme.Bg, 0.6f)).rectTransform.Place(14 + k * 10, 22, 4, 30);
            Theme.Label(zone.transform, "SUCATA", 26, Theme.Hex("E2C49E"), style: FontStyle.Bold).rectTransform.Place(80, 20, 140, 34);
            Theme.Label(zone.transform, "arraste uma carta", 15, Theme.TextDim).rectTransform.Place(80, 54, 140, 22);
            scrapInfo = Theme.Label(zone.transform, "", 18, Theme.Energy, TextAnchor.UpperLeft, FontStyle.Bold);
            scrapInfo.rectTransform.Place(14, 92, 200, 74);

            endTurn = Theme.Button(root, "ENCERRAR TURNO", EndTurn, Theme.Mix(Theme.PanelHi, Theme.Exposed, 0.5f), 26);
            ((RectTransform)endTurn.transform).Place(1600, 950, 290, 80);
            quit = Theme.Button(root, "Desistir", Quit, Theme.Panel, Theme.Small);
            ((RectTransform)quit.transform).Place(1760, 1036, 130, 38);

            hint = Theme.Label(root, "", 26, Theme.Warn, TextAnchor.MiddleCenter, FontStyle.Bold);
            hint.rectTransform.Center(960, 420, 900, 40);
            hint.gameObject.AddComponent<Outline>().effectColor = Color.black;
            cancel = Theme.Button(root, "Cancelar", CancelTarget, Theme.Panel, Theme.Small);
            ((RectTransform)cancel.transform).Center(960, 462, 160, 40);
            cancel.gameObject.SetActive(false);

            overlayLayer = Theme.Node(root, "Overlay").Fill();

            seen = b.history.Count;
            if (b.terrain != null)
            {
                var chip = Theme.Chip(root, "ARENA: " + b.terrain.name.ToUpperInvariant(), Theme.Exposed, 16);
                ((RectTransform)chip.transform.parent).Center(960, 455, 360, 30);
                chip.transform.parent.name = "Terrain";
                var help = chip.transform.parent.gameObject.AddComponent<HoverHelp>();
                help.text = $"{b.terrain.name} ({b.terrain.place}): {b.terrain.text} Pernas Hover ignoram o terreno.";
                chip.transform.parent.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            }
            Say($"Luta contra {b.enemy.name} ({b.enemy.tier}){(b.terrain == null ? "" : $" · arena: {b.terrain.name}, {b.terrain.text}")}.");
            Refresh(animateDraw: true);
            Banner("SEU TURNO", Theme.Good);
        }

        static RobotDef Def(Robot r) => new RobotDef
        {
            frame = r.frame.id, head = r.parts[Slot.Cabeca].def.id, torso = r.parts[Slot.Torso].def.id, armL = r.parts[Slot.BracoEsq].def.id,
            armR = r.parts[Slot.BracoDir].def.id, legs = r.parts[Slot.Pernas].def.id, core = r.core.id,
        };

        // ---- Panels ----
        void Panel(RectTransform parent, Robot r, bool enemy)
        {
            Theme.ClearChildren(parent);
            var bg = Theme.Box(parent, "Bg", Theme.Alpha(Theme.Panel, 0.92f));
            bg.rectTransform.Fill();
            bg.gameObject.AddComponent<Outline>().effectColor = Theme.Alpha(Theme.Line, 1);
            string tier = enemy ? $"  <color=#{Theme.HexOf(GarageScreen.TierColor(r.tier))}>{r.tier?.ToUpperInvariant()}</color>" : "";
            Theme.Label(parent, $"<b>{r.name}</b>{tier}", 26).rectTransform.Place(18, 10, 604, 34);
            var makers = r.makers.Length == 0 ? "" : " · bônus " + Content.MakerNames(r.makers);
            Theme.Label(parent, $"{r.frame.name}{makers}", 17, Theme.TextDim).rectTransform.Place(18, 44, 604, 24);

            var (cf, cl) = Theme.Bar(parent, r.ChassisOpen ? Theme.Exposed : Theme.Hex("7C86A6"), Theme.Body);
            var chassisBar = (RectTransform)cf.transform.parent;
            chassisBar.Place(18, 74, 604, 38);
            cf.fillAmount = (float)r.chassis / Mathf.Max(1, r.chassisMax);
            cl.text = $"{r.chassis}/{r.chassisMax} · {(r.ChassisOpen ? "EXPOSTO" : "PROTEGIDO")}";
            cl.rectTransform.offsetMin = new Vector2(46, cl.rectTransform.offsetMin.y);
            SlotIcons.Icon(chassisBar, "Chassi", 34, Theme.Ink).Place(3, 2, 34, 34);

            float y = 120;
            foreach (var p in r.parts.Values)
            {
                var color = p.Broken ? Theme.Hex("4A4E5C") : Theme.Maker(p.def.maker);
                var (f, l) = Theme.Bar(parent, Theme.Mix(Theme.Panel, color, p.Broken ? 1 : 0.5f), 17);
                var bar = (RectTransform)f.transform.parent;
                bar.Place(18, y, 604, 34);
                f.fillAmount = (float)Mathf.Max(0, p.hp) / p.Max;
                l.text = $"{p.def.name}  {(p.Broken ? "— AVARIADA" : $"{p.hp}/{p.Max}")}";
                l.rectTransform.offsetMin = new Vector2(44, l.rectTransform.offsetMin.y);
                SlotIcons.Icon(bar, p.slot, 32, p.Broken ? Theme.Bad : Theme.Maker(p.def.maker)).Place(3, 1, 32, 32);
                y += 40;
            }
        }

        void Intents()
        {
            Theme.ClearChildren(intents);
            foreach (var (kind, text, target) in IntentChips(b))
            {
                var color = Kinds.ColorOf(kind);
                var chip = Theme.Box(intents, "Intent", Theme.Alpha(Theme.Mix(Theme.Bg, color, 0.22f), 0.95f));
                var o = chip.gameObject.AddComponent<Outline>(); o.effectColor = Theme.Alpha(color, 0.9f); o.effectDistance = new Vector2(2, -2);
                Kinds.Icon(chip.transform, kind, 30).Place(8, 8, 30, 30);
                var t = Theme.Label(chip.transform, text, 20, color, TextAnchor.MiddleLeft, FontStyle.Bold);
                t.rectTransform.Place(44, 0, 260, 46);
                float w = Mathf.Min(300, t.preferredWidth + 60);
                if (target.HasValue)
                {
                    SlotIcons.Icon(chip.transform, target.Value == Slot.Chassi ? "Chassi" : SlotIcons.Key(target.Value), 38, color).Place(w - 8, 4, 38, 38);
                    w += 40;
                }
                chip.rectTransform.sizeDelta = new Vector2(w, 46);
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
            deckText.text = $"DECK\n{b.player.deck.Count}";
            discardText.text = $"DESCARTE\n{b.player.discard.Count}";
            endTurn.interactable = !busy && b.result == Result.None && pending == null;
            var p = b.player;
            bool swap = p.frame.passive == "scrapDraw" && !p.scrappedThisTurn;
            int scrapArmor = p.BonusValue("scrapArmor");
            scrapInfo.text = (swap ? "COMPRA 1 CARTA\n<size=14>(1ª sucata do Vira-lata)</size>" : "+1 ENERGIA") + (scrapArmor > 0 ? $"\n<color=#{Theme.HexOf(Theme.Armor)}>+{scrapArmor} BLINDAGEM</color>" : "");
            Hand(animateDraw);
            if (b.result != Result.None) ShowResult();
        }

        void Hand(bool animateDraw)
        {
            foreach (var gone in views.Keys.Where(c => !b.player.hand.Contains(c)).ToList())
            {
                if (views[gone]) Theme.Kill(views[gone].gameObject);
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
                    v.Rect.anchorMin = v.Rect.anchorMax = new Vector2(0, 1);
                    v.Rect.pivot = new Vector2(0.5f, 0.5f);
                    v.onClick = Clicked;
                    v.onDragStart = DragStart;
                    v.onDragMove = DragMove;
                    v.onDrop = Drop;
                    views[c] = v;
                }
                var home = new Vector2(start + i * spacing, -140);
                if (isNew && animateDraw)
                {
                    v.Rect.anchoredPosition = new Vector2(DeckPos.x, -(DeckPos.y - 760));
                    v.SetHome(v.Rect.anchoredPosition);
                    var target = home;
                    var view = v;
                    view.animating = true;
                    Fx.Delay(0.06f * i, () => { if (view) Fx.Move(view.Rect, target, 0.3f, () => { if (view) { view.SetHome(target); view.animating = false; } }); });
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
            var target = t == Target.OwnPart ? b.player : b.enemy;
            Func<Slot, bool> valid = t == Target.OwnPart ? (s => s != Slot.Chassi)
                : v.card.def.weakPoint > 0 ? (Func<Slot, bool>)(s => s != Slot.Chassi && Battle.ValidTarget(target, s))
                : s => Battle.ValidTarget(target, s);
            robot.SetTargets(valid, s => { robot.SetTargets(null, null); var c = pending; pending = null; Play(c, s); });
            hint.text = t == Target.OwnPart ? "Escolha uma parte sua para reparar" : b.enemy.ChassisOpen && v.card.def.weakPoint == 0
                ? "Escolha uma parte do inimigo — ou o CHASSI EXPOSTO" : "Escolha uma parte do inimigo";
            cancel.gameObject.SetActive(true);
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
            scrapGlow.color = Theme.Alpha(Theme.Hex("FFD9A0"), 0.9f);
            var t = b.NeedsTarget(b.player, v.card);
            if (t == Target.EnemyPart) enemyBot.SetTargets(ValidFor(v.card, t), null);
            if (t == Target.OwnPart) playerBot.SetTargets(ValidFor(v.card, t), null);
            hint.text = t == Target.None ? "Solte acima da mão para jogar · na SUCATA para sucatear" : "Solte numa parte para jogar · na SUCATA para sucatear";
        }

        void DragMove(CardView v, Vector2 overlayPos, Vector2 screen)
        {
            bool over = RectTransformUtility.RectangleContainsScreenPoint(scrapZone, screen, null);
            scrapZone.localScale = over ? new Vector3(1.06f, 1.06f, 1) : Vector3.one;
        }

        void Drop(CardView v, Vector2 overlayPos, Vector2 screen)
        {
            scrapGlow.color = Theme.Alpha(Theme.Hex("C8A27A"), 0.5f);
            scrapZone.localScale = Vector3.one;
            enemyBot.SetTargets(null, null); playerBot.SetTargets(null, null);
            hint.text = "";
            if (busy || pending != null || b.result != Result.None) { v.ReturnToHand(); return; }
            var t = b.NeedsTarget(b.player, v.card);
            bool overScrap = RectTransformUtility.RectangleContainsScreenPoint(scrapZone, screen, null);
            var valid = ValidFor(v.card, t);
            Slot? enemySlot = enemyBot.SlotAt(screen), ownSlot = playerBot.SlotAt(screen);
            if (enemySlot.HasValue && !valid(enemySlot.Value)) enemySlot = null;
            if (ownSlot.HasValue && !valid(ownSlot.Value)) ownSlot = null;
            var (kind, slot) = Decide(t, overScrap, enemySlot, ownSlot, overlayPos.y);
            bool canPlay = b.CanPlay(b.player, v.card);
            switch (kind)
            {
                case DropKind.Scrap: Scrap(v.card); break;
                case DropKind.PlayAt when canPlay: Play(v.card, slot); break;
                case DropKind.Play when canPlay: Play(v.card, null); break;
                case DropKind.ChooseTarget when canPlay: v.ReturnToHand(); Clicked(v); break;
                default: v.ReturnToHand(); if (kind != DropKind.Cancel) Fx.Shake(v.Rect, 6, 0.15f); break;
            }
        }

        void CancelTarget()
        {
            pending = null;
            enemyBot.SetTargets(null, null); playerBot.SetTargets(null, null);
            hint.text = ""; cancel.gameObject.SetActive(false);
            Refresh();
        }

        void Play(Card c, Slot? target)
        {
            hint.text = ""; cancel.gameObject.SetActive(false);
            var v = views.TryGetValue(c, out var view) ? view : null;
            views.Remove(c);
            if (!b.Play(b.player, c, target)) { if (v) Theme.Kill(v.gameObject); Refresh(); return; }
            var aimAt = c.def.TargetsPart ? enemyBot.PosOf(target ?? Slot.Torso) : c.def.repair > 0 ? playerBot.PosOf(target ?? Slot.Torso) : Fx.PosOf(playerBot.Rect);
            if (v) v.FlyAway(aimAt, 0.4f);
            busy = true;
            Fx.Run(ReplayEvents(fast: true, () => { busy = false; Refresh(); }));
        }

        void Scrap(Card c)
        {
            if (busy || pending != null || b.result != Result.None) return;
            var v = views.TryGetValue(c, out var view) ? view : null;
            views.Remove(c);
            if (!b.Scrap(b.player, c)) return;
            if (v) v.FlyAway(ScrapPos, 0.2f);
            Fx.Float(EnergyPos + new Vector2(0, -60), b.player.frame.passive == "scrapDraw" && b.history.Last(e => e.evt == "scrap").value == 0 ? "+1 CARTA" : "+1", Theme.Energy, 34);
            busy = true;
            Fx.Run(ReplayEvents(fast: true, () => { busy = false; Refresh(animateDraw: true); }));
        }

        void EndTurn()
        {
            if (busy || pending != null || b.result != Result.None) return;
            busy = true;
            foreach (var v in views.Values.Where(x => x)) v.FlyAway(DiscardPos, 0.3f);
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
            if (!quitArmed) { quitArmed = true; Say("<color=#FFC24A>Clique em Desistir de novo para sair da luta.</color>"); Fx.Delay(3, () => quitArmed = false); return; }
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
                    case "scrap": if (e.actor == "enemy") { Fx.Float(Fx.PosOf(bot.Rect), "SUCATA", Theme.Hex("C8A27A")); Say(e.msg); wait = 0.2f; } break;
                    case "attack":
                    {
                        lastElement = e.element;
                        var from = bot.PosOf(SlotFrom(e.source) ?? Slot.Torso);
                        var to = other.PosOf(SlotFrom(e.target) ?? Slot.Chassi);
                        Fx.Lunge(bot.Rect, Mathf.Sign(to.x - from.x) * 46);
                        Fx.Projectile(from, to, HitColor(e.element));
                        if (!Fx.Reduced) yield return new WaitForSeconds(fast ? 0.1f : 0.18f);
                    }
                        if (e.absorbed > 0) Fx.Float(Fx.PosOf(other.Rect) + new Vector2(0, -120), $"-{e.absorbed} BLINDAGEM", Theme.Armor, 26);
                        if (e.dodged > 0) Fx.Float(Fx.PosOf(other.Rect) + new Vector2(0, -160), "ESQUIVOU", Theme.Hex("9EF0FF"), 26);
                        if (e.chained) Fx.Float(Fx.PosOf(bot.Rect) + new Vector2(0, -200), "ENCADEAR!", Theme.Hex("FFD34D"), 28);
                        Say(e.msg, e.target);
                        break;
                    case "damage":
                    {
                        var slot = SlotFrom(e.target) ?? Slot.Chassi;
                        bot.Show(e.actor == "player" ? e.player : e.enemy, e.actor == "player" ? b.player : b.enemy);
                        bot.Hit(slot);
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
                        Say($"<color=#FFC24A>{e.msg}</color>", e.target);
                        wait = 0.25f;
                        break;
                    }
                    case "chassis_open": Fx.Float(bot.PosOf(Slot.Chassi) + new Vector2(0, 50), "CHASSI EXPOSTO", Theme.Exposed, 30); Say($"<color=#FF8A3D>{e.msg}</color>", "Chassi"); break;
                    case "armor": case "bonus":
                        if (e.value > 0 && (e.evt == "armor" || e.target == "Ferrovelho" || e.target == "Bastion")) Fx.Float(Fx.PosOf(bot.Rect) + new Vector2(0, -170), $"+{e.value} BLINDAGEM", Theme.Armor, 26);
                        break;
                    case "repair": Fx.Float(bot.PosOf(SlotFrom(e.target) ?? Slot.Torso), "+" + e.value, Theme.Good, 36); Say(e.msg, e.target); break;
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

        // Text box line: the part's name is replaced by its icon at the start of the line.
        void Say(string line, string slotName = null)
        {
            var slot = string.IsNullOrEmpty(slotName) ? null : SlotFrom(slotName);
            lines.Add((StripSlotNames(line), slot.HasValue ? (slot == Slot.Chassi ? "Chassi" : SlotIcons.Key(slot.Value)) : null));
            while (lines.Count > 5) lines.RemoveAt(0);
            Theme.ClearChildren(textRows);
            float y = 0;
            foreach (var (text, icon) in lines)
            {
                var row = Theme.Label(textRows, text, 18, Theme.Ink);
                row.rectTransform.Place(icon != null ? 38 : 0, y, 456, 44);
                row.verticalOverflow = VerticalWrapMode.Truncate;
                if (icon != null) SlotIcons.Icon(textRows, icon, 30, Theme.Ink).Place(0, y + 2, 30, 30);
                y += Mathf.Min(44, row.preferredHeight + 4);
            }
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
            var bar = Theme.Solid(overlayLayer, "Banner", Theme.Alpha(Theme.Bg, 0.85f));
            bar.rectTransform.Center(960, 420, 1920, 110);
            var t = Theme.Label(bar.transform, text, 56, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Fill();
            var g = bar.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            g.alpha = 0;
            Fx.Fade(g, 1, 0.15f, () => Fx.Delay(0.6f, () => { if (g) Fx.Fade(g, 0, 0.3f, () => UnityEngine.Object.Destroy(bar.gameObject)); }));
        }

        bool ending;

        // Victory/defeat: the loser collapses with sparks, then the result screen fades in.
        void ShowResult()
        {
            if (ending || overlayLayer.Find("Result")) return;
            ending = true;
            if (Fx.Reduced || !Application.isPlaying) { ResultOverlay(); return; }
            var loser = b.result == Result.Win ? enemyBot : playerBot;
            loser.idle = false;
            Fx.Burst(loser.PosOf(Slot.Chassi), Theme.Warn, 26, 340);
            Fx.Collapse(loser.Rect, loser == enemyBot ? -1 : 1);
            Banner(b.result == Result.Win ? "VITÓRIA" : "DERROTA", b.result == Result.Win ? Theme.Good : Theme.Bad);
            Fx.Delay(1.2f, ResultOverlay);
        }

        void ResultOverlay()
        {
            if (!overlayLayer || overlayLayer.Find("Result")) return; // the screen may have changed during the sequence
            bool win = b.result == Result.Win;
            var shade = Theme.Solid(overlayLayer, "Result", Theme.Alpha(Color.black, 0.78f));
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            Theme.Label(shade.transform, win ? "VITÓRIA" : "DERROTA", 110, win ? Theme.Good : Theme.Bad, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Center(960, 400, 1200, 140);
            Theme.Label(shade.transform, $"{b.enemy.name} · turno {b.turn}", Theme.Title, Theme.TextDim, TextAnchor.MiddleCenter).rectTransform.Center(960, 490, 1200, 50);
            if (app.afterBattle != null)
            {
                var after = app.afterBattle;
                var cont = Theme.Button(shade.transform, "CONTINUAR  →", () => after(b), Theme.Mix(Theme.PanelHi, Theme.Exposed, 0.6f), 30);
                cont.name = "Continuar";
                ((RectTransform)cont.transform).Center(960, 600, 360, 80);
                return;
            }
            var build = app.Garage.Current;
            var enemyId = app.lastEnemy;
            ((RectTransform)Theme.Button(shade.transform, "REVANCHE", () => app.StartBattle(build, enemyId), Theme.PanelHi, 26).transform).Center(640, 600, 320, 76);
            ((RectTransform)Theme.Button(shade.transform, "OUTRO ADVERSÁRIO", app.ShowOpponents, Theme.PanelHi, 26).transform).Center(960, 600, 320, 76);
            ((RectTransform)Theme.Button(shade.transform, "VOLTAR À GARAGEM", app.ShowGarage, Theme.PanelHi, 26).transform).Center(1280, 600, 320, 76);
        }
    }
}
