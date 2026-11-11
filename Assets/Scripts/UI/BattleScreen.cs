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
        RectTransform hand, intents, playerPanel, enemyPanel, overlayLayer;
        Text textBox, energyText, deckText, discardText, hint;
        Button endTurn, cancel, quit;
        readonly Dictionary<Card, CardView> views = new Dictionary<Card, CardView>();
        readonly Dictionary<Card, Text> scrapLabels = new Dictionary<Card, Text>();
        readonly List<string> lines = new List<string>();
        Card pending;
        bool busy, quitArmed;
        int seen; // history events already shown

        const float RobotScale = 0.85f;
        static readonly Vector2 EnemyPos = new Vector2(1450, 330), PlayerPos = new Vector2(430, 565);
        static readonly Vector2 DeckPos = new Vector2(110, 1015), DiscardPos = new Vector2(1500, 995), EnergyPos = new Vector2(120, 870);

        public BattleScreen(GameUI app, RectTransform root, Battle battle) { this.app = app; this.root = root; b = battle; }

        public static Slot? SlotFrom(string name) =>
            Enum.GetValues(typeof(Slot)).Cast<Slot>().Where(s => Battle.SlotName(s) == name).Select(s => (Slot?)s).FirstOrDefault();

        // Intent chips for the enemy's planned turn (pure: used by the UI and tests).
        public static List<(CardKind kind, string text)> IntentChips(Battle b)
        {
            var list = new List<(CardKind, string)>();
            if (b.enemyScraps.Count > 0) list.Add((CardKind.Other, $"SUCATA ×{b.enemyScraps.Count}"));
            bool aimed = false;
            foreach (var c in b.enemyIntent)
            {
                var d = c.def;
                var k = Kinds.Of(d);
                if (d.aim) aimed = true;
                if (k == CardKind.Attack)
                {
                    bool chooses = d.cost >= 2 || d.choose || aimed;
                    list.Add((k, $"{Kinds.Value(d, b.enemy.armor)} → {(chooses ? Battle.SlotName(b.enemyAimTarget) : "?")}"));
                    aimed = false; // Mirar is spent by the next attack
                }
                else list.Add((k, k == CardKind.Armor ? $"+{Kinds.Value(d)}" : d.name));
            }
            return list;
        }

        public void Build()
        {
            enemyPanel = Theme.Node(root, "EnemyPanel").Place(40, 30, 640, 330);
            playerPanel = Theme.Node(root, "PlayerPanel").Place(1240, 545, 640, 330);

            enemyBot = RobotView.Create(root, true, true);
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
            textBox = Theme.Label(tb.transform, "", 18, Theme.Ink, TextAnchor.LowerLeft);
            textBox.rectTransform.Fill(14);
            textBox.verticalOverflow = VerticalWrapMode.Truncate; // full history is in the battle log files

            hand = Theme.Node(root, "Hand").Place(0, 760, 1920, 320);

            var deckChip = Theme.Chip(root, "", Theme.TextDim, Theme.Body);
            ((RectTransform)deckChip.transform.parent).Center(DeckPos.x, DeckPos.y, 150, 76);
            deckText = deckChip;
            var discardChip = Theme.Chip(root, "", Theme.TextDim, Theme.Body);
            ((RectTransform)discardChip.transform.parent).Center(DiscardPos.x, DiscardPos.y, 150, 76);
            discardText = discardChip;

            var orb = Theme.Box(root, "Energy", Theme.Mix(Theme.Bg, Theme.Energy, 0.15f), Theme.Circle);
            orb.rectTransform.Center(EnergyPos.x, EnergyPos.y, 120, 120);
            Theme.Box(orb.transform, "Ring", Theme.Energy, Theme.Ring).rectTransform.Fill();
            energyText = Theme.Label(orb.transform, "", 52, Theme.Energy, TextAnchor.MiddleCenter, FontStyle.Bold);
            energyText.rectTransform.Fill();
            Theme.Label(root, "ENERGIA", Theme.Small, Theme.Energy, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Center(EnergyPos.x, EnergyPos.y - 76, 160, 26);

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
            Say($"Luta contra {b.enemy.name} ({b.enemy.tier}).");
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
            foreach (Transform t in parent) UnityEngine.Object.Destroy(t.gameObject);
            var bg = Theme.Box(parent, "Bg", Theme.Alpha(Theme.Panel, 0.92f));
            bg.rectTransform.Fill();
            bg.gameObject.AddComponent<Outline>().effectColor = Theme.Alpha(Theme.Line, 1);
            string tier = enemy ? $"  <color=#{Theme.HexOf(GarageScreen.TierColor(r.tier))}>{r.tier?.ToUpperInvariant()}</color>" : "";
            Theme.Label(parent, $"<b>{r.name}</b>{tier}", 26).rectTransform.Place(18, 10, 604, 34);
            var makers = r.makers.Length == 0 ? "" : " · bônus " + Content.MakerNames(r.makers);
            Theme.Label(parent, $"{r.frame.name}{makers}", 17, Theme.TextDim).rectTransform.Place(18, 44, 604, 24);

            var (cf, cl) = Theme.Bar(parent, r.ChassisOpen ? Theme.Exposed : Theme.Hex("7C86A6"), Theme.Body);
            ((RectTransform)cf.transform.parent).Place(18, 74, 604, 38);
            cf.fillAmount = (float)r.chassis / Mathf.Max(1, r.chassisMax);
            cl.text = $"CHASSI {r.chassis}/{r.chassisMax} · {(r.ChassisOpen ? "EXPOSTO" : "PROTEGIDO")}";

            float y = 120;
            foreach (var p in r.parts.Values)
            {
                var color = p.Broken ? Theme.Hex("4A4E5C") : Theme.Maker(p.def.maker);
                var (f, l) = Theme.Bar(parent, Theme.Mix(Theme.Panel, color, p.Broken ? 1 : 0.5f), 17);
                ((RectTransform)f.transform.parent).Place(18, y, 604, 34);
                f.fillAmount = (float)Mathf.Max(0, p.hp) / p.Max;
                l.text = $"{Battle.SlotName(p.slot).ToUpperInvariant()}  {p.def.name}  {(p.Broken ? "— AVARIADA" : $"{p.hp}/{p.Max}")}";
                y += 40;
            }
        }

        void Intents()
        {
            foreach (Transform t in intents) UnityEngine.Object.Destroy(t.gameObject);
            foreach (var (kind, text) in IntentChips(b))
            {
                var color = Kinds.ColorOf(kind);
                var chip = Theme.Box(intents, "Intent", Theme.Alpha(Theme.Mix(Theme.Bg, color, 0.22f), 0.95f));
                var o = chip.gameObject.AddComponent<Outline>(); o.effectColor = Theme.Alpha(color, 0.9f); o.effectDistance = new Vector2(2, -2);
                Kinds.Icon(chip.transform, kind, 30).Place(8, 8, 30, 30);
                var t = Theme.Label(chip.transform, text, 20, color, TextAnchor.MiddleLeft, FontStyle.Bold);
                t.rectTransform.Place(44, 0, 260, 46);
                chip.rectTransform.sizeDelta = new Vector2(Mathf.Min(300, t.preferredWidth + 60), 46);
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
            Hand(animateDraw);
            if (b.result != Result.None) ShowResult();
        }

        void Hand(bool animateDraw)
        {
            foreach (var gone in views.Keys.Where(c => !b.player.hand.Contains(c)).ToList())
            {
                if (views[gone]) UnityEngine.Object.Destroy(views[gone].gameObject);
                views.Remove(gone);
            }
            var p = b.player;
            int n = p.hand.Count;
            float spacing = Mathf.Min(CardView.W + 16, 1000f / Mathf.Max(1, n));
            float start = 760 - spacing * (n - 1) / 2;
            int i = 0;
            foreach (var c in p.hand)
            {
                bool isNew = !views.TryGetValue(c, out var v);
                if (isNew)
                {
                    var part = c.source == Slot.Frame ? null : p.parts[c.source].def;
                    v = CardView.Create(hand, c.def, part?.maker, c.source == Slot.Frame ? "Frame" : Battle.SlotName(c.source), c);
                    v.Rect.anchorMin = v.Rect.anchorMax = new Vector2(0, 1);
                    v.Rect.pivot = new Vector2(0.5f, 0.5f);
                    v.onClick = Clicked;
                    var scrap = Theme.Button(v.transform, "", () => Scrap(c), Theme.Mix(Theme.Panel, Theme.Hex("C8A27A"), 0.3f), 16);
                    ((RectTransform)scrap.transform).Place(10, CardView.H - 6, CardView.W - 20, 34);
                    scrapLabels[c] = scrap.GetComponentInChildren<Text>();
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
                bool swap = p.frame.passive == "scrapDraw" && !p.scrappedThisTurn;
                scrapLabels[c].text = swap ? "SUCATA: COMPRA 1" : "SUCATA: +1 ENERGIA";
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
            if (!b.Play(b.player, c, target)) { if (v) UnityEngine.Object.Destroy(v.gameObject); Refresh(); return; }
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
            if (v) v.FlyAway(EnergyPos, 0.2f);
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
            if (!quitArmed) { quitArmed = true; Say("<color=#FFC24A>Clique em Desistir de novo para voltar à garagem.</color>"); Fx.Delay(3, () => quitArmed = false); return; }
            app.ShowGarage();
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
                        if (e.actor == "enemy") { Say($"<color=#FF8A8A>{e.msg}</color>"); wait = 0.35f; }
                        else Say(e.msg);
                        break;
                    case "scrap": if (e.actor == "enemy") { Fx.Float(Fx.PosOf(bot.Rect), "SUCATA", Theme.Hex("C8A27A")); Say(e.msg); wait = 0.2f; } break;
                    case "attack":
                        if (e.absorbed > 0) Fx.Float(Fx.PosOf(other.Rect) + new Vector2(0, -120), $"-{e.absorbed} BLINDAGEM", Theme.Armor, 26);
                        if (e.dodged > 0) Fx.Float(Fx.PosOf(other.Rect) + new Vector2(0, -160), "ESQUIVOU", Theme.Hex("9EF0FF"), 26);
                        if (e.chained) Fx.Float(Fx.PosOf(bot.Rect) + new Vector2(0, -200), "ENCADEAR!", Theme.Hex("FFD34D"), 28);
                        Say(e.msg);
                        break;
                    case "damage":
                    {
                        var slot = SlotFrom(e.target) ?? Slot.Chassi;
                        bot.Show(e.actor == "player" ? e.player : e.enemy, e.actor == "player" ? b.player : b.enemy);
                        bot.Hit(slot);
                        Fx.Float(bot.PosOf(slot), "-" + e.value, slot == Slot.Chassi ? Theme.Exposed : Theme.Bad, 40);
                        wait = fast ? 0.08f : 0.18f;
                        break;
                    }
                    case "part_broken":
                    {
                        var slot = SlotFrom(e.target) ?? Slot.Torso;
                        Fx.Burst(bot.PosOf(slot), Theme.Warn, 18);
                        Fx.Float(bot.PosOf(slot) + new Vector2(0, -40), "AVARIADA!", Theme.Warn, 30);
                        Say($"<color=#FFC24A>{e.msg}</color>");
                        wait = 0.25f;
                        break;
                    }
                    case "chassis_open": Fx.Float(bot.PosOf(Slot.Chassi) + new Vector2(0, 50), "CHASSI EXPOSTO", Theme.Exposed, 30); Say($"<color=#FF8A3D>{e.msg}</color>"); break;
                    case "armor": case "bonus":
                        if (e.value > 0 && (e.evt == "armor" || e.target == "Ferrovelho" || e.target == "Bastion")) Fx.Float(Fx.PosOf(bot.Rect) + new Vector2(0, -170), $"+{e.value} BLINDAGEM", Theme.Armor, 26);
                        break;
                    case "repair": Fx.Float(bot.PosOf(SlotFrom(e.target) ?? Slot.Torso), "+" + e.value, Theme.Good, 36); Say(e.msg); break;
                    case "heat_applied": case "overload_applied": case "corrosion_applied": case "weak_point": case "intent_cancel": case "boss_phase":
                        Say(e.evt == "boss_phase" ? $"<color=#FF5A6A><b>{e.msg}</b></color>" : e.msg);
                        if (e.evt == "boss_phase") { Banner("FASE " + (e.value + 1), Theme.Bad); wait = 0.6f; }
                        break;
                    case "battle_end": Say($"<b>{e.msg.Split('.')[0]}.</b>"); break;
                }
                if (wait > 0) yield return new WaitForSeconds(wait);
            }
            yield return new WaitForSeconds(fast ? 0.1f : 0.3f);
            done?.Invoke();
        }

        void Say(string line)
        {
            lines.Add(line);
            while (lines.Count > 5) lines.RemoveAt(0);
            textBox.text = string.Join("\n", lines);
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

        void ShowResult()
        {
            if (overlayLayer.Find("Result")) return;
            bool win = b.result == Result.Win;
            var shade = Theme.Solid(overlayLayer, "Result", Theme.Alpha(Color.black, 0.78f));
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            Theme.Label(shade.transform, win ? "VITÓRIA" : "DERROTA", 110, win ? Theme.Good : Theme.Bad, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Center(960, 400, 1200, 140);
            Theme.Label(shade.transform, $"{b.enemy.name} · turno {b.turn}", Theme.Title, Theme.TextDim, TextAnchor.MiddleCenter).rectTransform.Center(960, 490, 1200, 50);
            var build = app.garage.Current;
            var enemyId = b.content.robots.First(r => r.name == b.enemy.name).id;
            ((RectTransform)Theme.Button(shade.transform, "REVANCHE", () => app.StartBattle(build, enemyId), Theme.PanelHi, 28).transform).Center(820, 600, 260, 76);
            ((RectTransform)Theme.Button(shade.transform, "GARAGEM", app.ShowGarage, Theme.PanelHi, 28).transform).Center(1100, 600, 260, 76);
        }
    }
}
