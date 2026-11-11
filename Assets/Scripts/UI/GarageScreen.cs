using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardBattler.UI
{
    // Garage: only for building robots. Robot in the middle with its slots around it; on the right the robot's
    // stats sheet (with "?" explanations and +/- deltas while hovering an alternative part) or the deck.
    public class GarageScreen
    {
        readonly GameUI app;
        readonly RectTransform root;
        RectTransform sheet;
        static bool showDeck;

        public GarageScreen(GameUI app, RectTransform root) { this.app = app; this.root = root; }

        Content C => app.content;
        GarageSave G => app.garage;

        static readonly (string icon, string slot, Func<RobotDef, string> get, Action<RobotDef, string> set, float x, float y)[] Slots =
        {
            ("Cabeca", "Cabeca", b => b.head, (b, v) => b.head = v, 410, 120),
            (SlotIcons.Nucleo, "Nucleo", b => b.core, (b, v) => b.core = v, 760, 120),
            ("BracoEsq", "Braco", b => b.armL, (b, v) => b.armL = v, 60, 300),
            ("BracoDir", "Braco", b => b.armR, (b, v) => b.armR = v, 760, 300),
            ("Torso", "Torso", b => b.torso, (b, v) => b.torso = v, 60, 480),
            ("Pernas", "Pernas", b => b.legs, (b, v) => b.legs = v, 760, 480),
        };

        // Why the build can't go on to the opponent selection (null = it can).
        public static string ContinueBlocker(Content c, RobotDef b)
        {
            var errors = c.Validate(b);
            return errors.Count == 0 ? null : string.Join("\n", errors);
        }

        void Changed() { G.Save(GameUI.SavePath); Rebuild(); }

        void Rebuild()
        {
            Theme.ClearChildren(root);
            Tooltip.Hide();
            Build();
        }

        static string Cycle(string[] ids, string current, int dir)
        {
            int i = Array.IndexOf(ids, current);
            return ids[((i < 0 ? 0 : i + dir) % ids.Length + ids.Length) % ids.Length];
        }

        public void Build()
        {
            var b = G.Current;
            var frame = C.Frame(b.frame);

            Theme.Label(root, "GARAGEM", 44, Theme.Ink, style: FontStyle.Bold).rectTransform.Place(60, 30, 400, 60);
            Theme.Label(root, "Monte o robô", Theme.Small, Theme.TextDim).rectTransform.Place(62, 80, 500, 30);
            for (int i = 0; i < GarageSave.SlotCount; i++)
            {
                int k = i;
                bool sel = G.selected == i;
                var btn = Theme.Button(root, G.slots[i].name, () => { G.selected = k; Changed(); }, sel ? Theme.Mix(Theme.PanelHi, Theme.Warn, 0.35f) : Theme.Panel);
                ((RectTransform)btn.transform).Place(420 + i * 180, 34, 170, 50);
            }

            // Robot area
            Theme.Box(root, "Area", Theme.Alpha(Theme.Panel, 0.6f)).rectTransform.Place(40, 110, 1040, 880);
            var robot = RobotView.Create(root, false, false);
            robot.Rect.Center(560, 555, RobotView.W, RobotView.H);
            robot.transform.localScale = new Vector3(1.15f, 1.15f, 1);
            robot.Build(C, b);

            var frames = C.frames.Select(f => f.id).ToArray();
            SlotWidget(60, 120, "Frame", frame.name, null, $"Chassi {frame.chassis} · Mão {frame.hand} · Carga {frame.carry}", frame.passiveText,
                () => { b.frame = Cycle(frames, b.frame, -1); Changed(); }, () => { b.frame = Cycle(frames, b.frame, 1); Changed(); },
                dir => With(b, x => x.frame = Cycle(frames, b.frame, dir)));

            foreach (var (icon, slot, get, set, x, y) in Slots)
            {
                var ids = C.PartsFor(slot).Select(p => p.id).ToArray();
                var part = C.Part(get(b));
                string stats = slot == "Nucleo"
                    ? $"{part.energy} energia/turno{(part.heatPerTurn > 0 ? $" · +{part.heatPerTurn} Calor/turno" : "")} · Peso {part.weight}"
                    : $"Integridade {part.integrity} · Peso {part.weight}{(string.IsNullOrEmpty(part.element) ? "" : " · " + part.element)}";
                string cards = string.Join(", ", part.cards.GroupBy(id => id).Select(g => $"{g.Count()}x {C.Card(g.Key).name}"));
                SlotWidget(x, y, icon, part.name, part.maker, stats, cards,
                    () => { set(b, Cycle(ids, get(b), -1)); Changed(); }, () => { set(b, Cycle(ids, get(b), 1)); Changed(); },
                    dir => With(b, r => set(r, Cycle(ids, get(b), dir))));
            }

            // Weight bar + what blocks the build
            int weight = C.Weight(b);
            var (fill, wl) = Theme.Bar(root, weight > frame.carry ? Theme.Bad : Theme.Good, Theme.Body);
            var wbar = (RectTransform)fill.transform.parent;
            wbar.Place(60, 820, 960, 40);
            fill.fillAmount = Mathf.Clamp01((float)weight / frame.carry);
            wl.text = $"PESO {weight} / {frame.carry}" + (weight > frame.carry ? "  — acima da carga do frame" : "");
            Theme.HelpIcon(root, "peso", 30).Place(1030, 825, 30, 30);
            var blocker = ContinueBlocker(C, b);
            if (blocker != null) Theme.Label(root, blocker, Theme.Body, Theme.Bad).rectTransform.Place(60, 876, 1000, 90);

            // Right column: stats sheet or deck
            var deck = C.Deck(b);
            var tabSheet = Theme.Button(root, "FICHA DO ROBÔ", () => { showDeck = false; Rebuild(); }, showDeck ? Theme.Panel : Theme.PanelHi);
            ((RectTransform)tabSheet.transform).Place(1120, 110, 300, 54);
            var tabDeck = Theme.Button(root, $"DECK ({deck.Count})", () => { showDeck = true; Rebuild(); }, showDeck ? Theme.PanelHi : Theme.Panel);
            ((RectTransform)tabDeck.transform).Place(1430, 110, 240, 54);
            var content = Theme.Node(root, "TabContent").Place(1120, 176, 760, 770);
            if (showDeck) DeckGrid(content, deck);
            else { sheet = content; DrawSheet(BuildStats.Of(C, b), null); }

            // Continue to the opponent selection
            var go = Theme.Button(root, "CONTINUAR  →", app.ShowOpponents, Theme.Mix(Theme.PanelHi, Theme.Exposed, 0.6f), 32);
            go.name = "Continuar";
            ((RectTransform)go.transform).Place(1520, 970, 360, 84);
            go.interactable = blocker == null;
            if (blocker != null) Theme.Label(root, "Ajuste o robô para continuar", Theme.Small, Theme.Bad, TextAnchor.MiddleRight).rectTransform.Place(1120, 990, 390, 40);

            var log = Theme.Button(root, (BattleLogger.MirrorToConsole ? "[x]" : "[ ]") + " Log no console", () => { BattleLogger.MirrorToConsole = !BattleLogger.MirrorToConsole; Rebuild(); }, Theme.Panel, Theme.Small);
            ((RectTransform)log.transform).Place(60, 1010, 260, 44);
        }

        // Copy of the build with one change (for the hover preview).
        static RobotDef With(RobotDef b, Action<RobotDef> change) { var c = b.Clone(); change(c); return c; }

        public static Color TierColor(string tier) => tier switch
        {
            "Fácil" => Theme.Good, "Moderado" => Theme.Warn, "Difícil" => Theme.Exposed, "Chefe" => Theme.Bad, _ => Theme.TextDim,
        };

        void SlotWidget(float x, float y, string iconKey, string name, string maker, string stats, string cards, Action prev, Action next, Func<int, RobotDef> preview)
        {
            var accent = maker == null ? Theme.TextDim : Theme.Maker(maker);
            var box = Theme.Box(root, "Slot:" + iconKey, Theme.Panel);
            box.rectTransform.Place(x, y, 300, 170);
            var o = box.gameObject.AddComponent<Outline>(); o.effectColor = Theme.Alpha(accent, 0.5f); o.effectDistance = new Vector2(2, -2);
            Theme.Solid(box.transform, "Stripe", accent).rectTransform.Place(0, 10, 5, 150);
            SlotIcons.Icon(box.transform, iconKey, 52, maker == null ? Theme.Warn : accent).Place(12, 8, 52, 52);
            var n = Theme.Label(box.transform, name, 24, Theme.Ink, style: FontStyle.Bold);
            n.rectTransform.Place(72, 6, 156, 32);
            n.resizeTextForBestFit = true; n.verticalOverflow = VerticalWrapMode.Truncate; n.resizeTextMinSize = 15; n.resizeTextMaxSize = 24;
            if (maker != null)
            {
                var chip = Theme.Chip(box.transform, maker.ToUpperInvariant(), accent, 14);
                ((RectTransform)chip.transform.parent).Place(72, 40, 124, 22);
            }
            Theme.Label(box.transform, stats, 16, Theme.Ink).rectTransform.Place(16, 70, 276, 40);
            Theme.Label(box.transform, cards, 15, Theme.TextDim).rectTransform.Place(16, 112, 276, 54);
            var l = Theme.Button(box.transform, "<", prev, Theme.PanelHi);
            ((RectTransform)l.transform).Place(232, 8, 30, 32);
            var r = Theme.Button(box.transform, ">", next, Theme.PanelHi);
            ((RectTransform)r.transform).Place(264, 8, 30, 32);
            // Hovering an arrow previews the part it would switch to on the stats sheet.
            foreach (var (btn, dir) in new[] { (l, -1), (r, 1) })
            {
                var h = btn.gameObject.AddComponent<HoverAction>();
                h.enter = () => { if (sheet) DrawSheet(BuildStats.Of(C, G.Current), BuildStats.Of(C, preview(dir))); };
                h.exit = () => { if (sheet) DrawSheet(BuildStats.Of(C, G.Current), null); };
            }
        }

        // ---- Stats sheet ----
        // Each row: glossary id (the "?" text), label, value, number for deltas, whether higher is better, optional slot icon.
        public static List<(string help, string label, string value, float? number, bool higherIsBetter, Slot? icon)> SheetRows(BuildStats s)
        {
            string F(float v) => v.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR"));
            var rows = new List<(string, string, string, float?, bool, Slot?)>
            {
                ("chassi", "Chassi", s.chassis.ToString(), s.chassis, true, null),
                ("integridade", "Integridade total", s.totalIntegrity.ToString(), s.totalIntegrity, true, null),
            };
            foreach (var p in s.parts) rows.Add(("integridade_parte", p.part, p.integrity.ToString(), p.integrity, true, p.slot));
            rows.Add(("energia", "Energia por turno", s.energy.ToString(), s.energy, true, null));
            rows.Add(("calor_turno", "Calor por turno", s.heatPerTurn.ToString(), s.heatPerTurn, false, null));
            rows.Add(("mao", "Tamanho da mão", s.hand.ToString(), s.hand, true, null));
            rows.Add(("peso", "Peso / Carga", $"{s.weight} / {s.carry}", s.carry - s.weight, true, null));
            rows.Add(("bonus", "Bônus de fabricante", s.makers.Length == 0 ? "nenhum" : string.Join(" + ", s.makers), null, true, null));
            rows.Add(("passiva", "Passiva do frame", s.passive, null, true, null));
            var elems = s.elements.Concat(s.statuses.Select(x => "aplica " + x)).ToList();
            rows.Add(("elementos", "Elementos e status", elems.Count == 0 ? "nenhum" : string.Join(", ", elems), null, true, null));
            rows.Add(("deck", "Deck", $"{s.deckSize} cartas: {s.attacks} ataque · {s.defense} defesa · {s.utility} utilidade", s.attacks, true, null));
            rows.Add(("custo_medio", "Custo médio", F(s.avgCost), s.avgCost, false, null));
            rows.Add(("alvo", "Alvo dos ataques", $"{s.choose} escolhem · {s.random} aleatórios", s.choose, true, null));
            return rows;
        }

        void DrawSheet(BuildStats s, BuildStats preview)
        {
            Theme.ClearChildren(sheet);
            var bg = Theme.Box(sheet, "Sheet", Theme.Panel);
            bg.rectTransform.Fill();
            var rows = SheetRows(s);
            var prev = preview == null ? null : SheetRows(preview);
            float y = 14;
            for (int i = 0; i < rows.Count; i++)
            {
                var (help, label, value, number, higherBetter, icon) = rows[i];
                bool wide = help == "passiva";
                float h = wide ? 58 : 38;
                float x = 14;
                Theme.HelpIcon(sheet, help, 26).Place(x, y + 4, 26, 26);
                x += 34;
                if (icon.HasValue) { SlotIcons.Icon(sheet, icon.Value, 30, Theme.Ink).Place(x, y + 2, 30, 30); x += 38; }
                var shownLabel = prev != null && icon.HasValue ? prev[i].label : label;
                Theme.Label(sheet, shownLabel, 19, icon.HasValue ? Theme.Ink : Theme.TextDim, TextAnchor.MiddleLeft).rectTransform.Place(x, y, 250 - (x - 48), 34);
                var shownValue = prev != null ? prev[i].value : value;
                var v = Theme.Label(sheet, shownValue, wide ? 16 : 20, Theme.Ink, wide ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, wide ? FontStyle.Normal : FontStyle.Bold);
                v.rectTransform.Place(300, y + (wide ? 4 : 0), wide ? 440 : 330, h - 4);
                if (prev != null && number.HasValue && prev[i].number.HasValue && Math.Abs(prev[i].number.Value - number.Value) > 0.001f)
                {
                    float d = prev[i].number.Value - number.Value;
                    bool good = higherBetter ? d > 0 : d < 0;
                    string txt = (d > 0 ? "+" : "") + (Math.Abs(d % 1) > 0.001f ? d.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) : ((int)d).ToString());
                    var chip = Theme.Chip(sheet, txt, good ? Theme.Good : Theme.Bad, 18);
                    ((RectTransform)chip.transform.parent).Place(650, y + 3, 90, 30);
                }
                else if (prev != null && !number.HasValue && prev[i].value != value)
                {
                    var chip = Theme.Chip(sheet, "muda", Theme.Warn, 16);
                    ((RectTransform)chip.transform.parent).Place(650, y + 3, 90, 30);
                }
                y += h;
            }
            Theme.Label(sheet, preview == null ? "Passe o mouse em < ou > de um slot para comparar a peça." : "Comparando com a peça do botão sob o mouse.",
                15, preview == null ? Theme.TextDim : Theme.Warn, TextAnchor.MiddleLeft).rectTransform.Place(14, y + 6, 720, 26);
        }

        void DeckGrid(RectTransform parent, List<CardDef> deck)
        {
            const float s = 0.58f;
            int col = 0, row = 0;
            var srcs = new[] { (G.Current.head, "Cabeca"), (G.Current.torso, "Torso"), (G.Current.armL, "BracoEsq"), (G.Current.armR, "BracoDir"), (G.Current.legs, "Pernas") };
            foreach (var d in deck)
            {
                var src = srcs.FirstOrDefault(p => C.Part(p.Item1).cards.Contains(d.id));
                string maker = src.Item1 != null ? C.Part(src.Item1).maker : null;
                var card = CardView.Create(parent, d, maker, src.Item2 ?? "Frame");
                card.interactive = false;
                card.Rect.anchorMin = card.Rect.anchorMax = new Vector2(0, 1);
                card.Rect.pivot = new Vector2(0, 1);
                card.transform.localScale = new Vector3(s, s, 1);
                card.Rect.anchoredPosition = new Vector2(col * (CardView.W * s + 10), -row * (CardView.H * s + 10));
                card.SetState(true, d.cost, Kinds.TargetBadge(d, d.cost >= 2 || d.choose), false, false);
                if (++col == 6) { col = 0; row++; }
            }
        }
    }

    // Enter/exit callbacks for any UI element (used for hover previews).
    public class HoverAction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action enter, exit;
        public void OnPointerEnter(PointerEventData e) => enter?.Invoke();
        public void OnPointerExit(PointerEventData e) => exit?.Invoke();
    }
}
