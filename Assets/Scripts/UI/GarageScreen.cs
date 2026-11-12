using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace CardBattler.UI
{
    // Garage: only for building robots. Robot in the middle with its slots around it; on the right the robot's
    // stats sheet (with "?" explanations and +/- deltas while hovering an alternative part) or the deck.
    public class GarageScreen
    {
        readonly GameUI app;
        readonly VisualElement root;
        VisualElement sheet;
        static bool showDeck;

        public GarageScreen(GameUI app, VisualElement root) { this.app = app; this.root = root; }

        Content C => app.content;
        GarageSave G => app.Garage;
        Career Car => app.careerMode ? app.career : null; // null in Treino livre (everything unlocked)

        static readonly (string icon, string slot, Func<RobotDef, string> get, Action<RobotDef, string> set, float x, float y)[] Slots =
        {
            ("Cabeca", "Cabeca", b => b.head, (b, v) => b.head = v, 400, 124),
            (SlotIcons.Nucleo, "Nucleo", b => b.core, (b, v) => b.core = v, 740, 124),
            ("BracoEsq", "Braco", b => b.armL, (b, v) => b.armL = v, 60, 316),
            ("BracoDir", "Braco", b => b.armR, (b, v) => b.armR = v, 740, 316),
            ("Torso", "Torso", b => b.torso, (b, v) => b.torso = v, 60, 508),
            ("Pernas", "Pernas", b => b.legs, (b, v) => b.legs = v, 740, 508),
        };

        // Why the build can't go on to the opponent selection (null = it can).
        public static string ContinueBlocker(Content c, RobotDef b, Career career = null)
        {
            var errors = career?.Validate(c, b) ?? c.Validate(b);
            return errors.Count == 0 ? null : string.Join("\n", errors);
        }

        void Changed() { app.SaveGarage(); Rebuild(); }

        void Rebuild()
        {
            root.Clear();
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

            Theme.Title(root, "GARAGEM", 44).At(60, 26);
            Theme.Label(root, app.careerMode ? $"{app.career.credits} créditos · {app.career.scrap} sucata · só peças que você tem" : "TREINO LIVRE · todas as peças liberadas",
                Theme.Small, app.careerMode ? Theme.Warn : Theme.Exposed).At(62, 80);
            var tabs = Theme.Row(root, "Builds", 10).Place(420, 30, 560, 54);
            for (int i = 0; i < GarageSave.SlotCount; i++)
            {
                int k = i;
                var btn = Theme.Button(tabs, G.slots[i].name, () => { G.selected = k; Changed(); }, null, Theme.Body, "btn-tab");
                btn.EnableInClassList("selected", G.selected == i);
                btn.Size(176, 54);
                Theme.Hover(btn, $"Robô {i + 1} de {GarageSave.SlotCount}: você guarda até {GarageSave.SlotCount} montagens.");
            }
            Theme.Button(root, app.careerMode ? "←  VOLTAR" : "←  MENU", app.Back, null, 22).Place(1720, 30, 160, 54);

            // Robot area
            Theme.Plate(root, "Area", "panel-glass").Place(40, 110, 1040, 880);
            Theme.El(root, "Pedestal", "pedestal").Center(560, 836, 520, 90);
            RobotView.Create(root, false, true).At(560, 590, 1.1f).Build(C, b);

            var frames = C.frames.Where(f => Car == null || Car.OwnsFrame(f)).Select(f => f.id).ToArray();
            SlotWidget(60, 124, "Frame", frame.name, null, $"Chassi {frame.chassis} · Mão {frame.hand} · Carga {frame.carry}", frame.passiveText,
                () => { b.frame = Cycle(frames, b.frame, -1); Changed(); }, () => { b.frame = Cycle(frames, b.frame, 1); Changed(); },
                dir => With(b, x => x.frame = Cycle(frames, b.frame, dir)), frames.Length);

            foreach (var (icon, slot, get, set, x, y) in Slots)
            {
                var ids = C.PartsFor(slot).Where(p => Car == null || Car.Owns(p)).Select(p => p.id).ToArray();
                var part = C.Part(get(b));
                string stats = slot == "Nucleo"
                    ? $"{part.energy} energia/turno{(part.heatPerTurn > 0 ? $" · +{part.heatPerTurn} Calor/turno" : "")} · Peso {part.weight}"
                    : $"Integridade {(Car != null && Car.HpOf(C, part.id) < part.integrity ? $"<color=#{Theme.HexOf(Theme.Bad)}>{Car.HpOf(C, part.id)}</color>/" : "")}{part.integrity} · Peso {part.weight}{(string.IsNullOrEmpty(part.element) ? "" : " · " + part.element)}{(string.IsNullOrEmpty(part.resist) ? "" : " · resiste " + part.resist)}{(string.IsNullOrEmpty(part.weakTo) ? "" : " · fraca a " + part.weakTo)}";
                string cards = string.Join(", ", part.cards.GroupBy(id => id).Select(g => $"{g.Count()}x {C.Card(g.Key).name}"));
                SlotWidget(x, y, icon, part.name, part.maker, stats, cards,
                    () => { set(b, Cycle(ids, get(b), -1)); Changed(); }, () => { set(b, Cycle(ids, get(b), 1)); Changed(); },
                    dir => With(b, r => set(r, Cycle(ids, get(b), dir))), ids.Length);
            }

            // Weight bar + what blocks the build
            int weight = C.Weight(b);
            var (fill, wl) = Theme.Bar(root, weight > frame.carry ? Theme.Bad : Theme.Good, Theme.Body);
            fill.parent.Place(60, 890, 960, 40);
            Theme.SetFill(fill, (float)weight / frame.carry);
            wl.text = $"PESO {weight} / {frame.carry}" + (weight > frame.carry ? "  — acima da carga do frame" : "");
            Theme.HelpIcon(root, "peso", 32).Place(1030, 894, 32, 32);
            var blocker = ContinueBlocker(C, b, Car);
            if (blocker != null) Theme.Label(root, blocker, Theme.Body, Theme.Bad).Place(60, 936, 1000, 50).Fit(Theme.Small);

            // Right column: stats sheet or deck
            var deck = C.Deck(b);
            var tabRow = Theme.Row(root, "Tabs", 10).Place(1120, 110, 760, 56);
            var tabSheet = Theme.Button(tabRow, "FICHA DO ROBÔ", () => { showDeck = false; Rebuild(); }, null, Theme.Body, "btn-tab").Size(300, 56);
            tabSheet.EnableInClassList("selected", !showDeck);
            var tabDeck = Theme.Button(tabRow, $"DECK ({deck.Count})", () => { showDeck = true; Rebuild(); }, null, Theme.Body, "btn-tab").Size(240, 56);
            tabDeck.EnableInClassList("selected", showDeck);
            var panel = Theme.Plate(root, "TabContent").Place(1120, 176, 760, 770);
            if (showDeck) DeckGrid(Theme.Scroll(panel).Fill(12), deck);
            else { sheet = panel; DrawSheet(BuildStats.Of(C, b), null); }

            // Continue to the opponent selection
            var go = Theme.Primary(root, "CONTINUAR  →", app.careerMode ? () => app.ShowCareer(app.careerHub) : app.ShowOpponents, 32);
            go.name = "Continuar";
            go.Place(1520, 966, 360, 88);
            go.SetEnabled(blocker == null);
            if (blocker != null) Theme.Label(root, "Ajuste o robô para continuar", Theme.Small, Theme.Bad, TextAnchor.MiddleRight).Place(1120, 990, 390, 40);

            // Wheels skid on the Fosso scrap floor: point at the legs slot when the player owns other legs.
            if (Car != null && C.Part(b.legs).wheels && Car.owned.Any(id => C.Part(id) is { slot: "Pernas", wheels: false }))
                Theme.Label(root, "Dica: no piso de sucata do Fosso as rodas esquivam só metade. Troque as pernas (slot de baixo à direita).", Theme.Small, Theme.Exposed, TextAnchor.MiddleLeft)
                    .Place(340, 1004, 760, 50).Fit(15);

            var log = Theme.Button(root, (BattleLogger.MirrorToConsole ? "[x]" : "[ ]") + " Log no console", () => { BattleLogger.MirrorToConsole = !BattleLogger.MirrorToConsole; Rebuild(); }, null, Theme.Small, "btn-small");
            log.Place(60, 1004, 260, 50);
        }

        // Copy of the build with one change (for the hover preview).
        static RobotDef With(RobotDef b, Action<RobotDef> change) { var c = b.Clone(); change(c); return c; }

        public static Color TierColor(string tier) => tier switch
        {
            "Fácil" => Theme.Good, "Moderado" => Theme.Warn, "Difícil" => Theme.Exposed, "Chefe" => Theme.Bad, _ => Theme.TextDim,
        };

        void SlotWidget(float x, float y, string iconKey, string name, string maker, string stats, string cards, Action prev, Action next, Func<int, RobotDef> preview, int options)
        {
            var accent = maker == null ? Theme.TextDim : Theme.Maker(maker);
            var box = Theme.Plate(root, "Slot:" + iconKey, "slot-card").Place(x, y, 300, 176);
            Theme.Border(box, Theme.Alpha(accent, 0.55f), 2);
            Theme.Solid(box, "Stripe", accent).Place(0, 12, 5, 150);
            SlotIcons.Icon(box, iconKey, 52, maker == null ? Theme.Warn : accent).Place(12, 10, 52, 52);
            Theme.Label(box, name, 22, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold).Place(72, 6, 152, 32).OneLine();
            if (maker != null) Theme.Chip(box, maker.ToUpperInvariant(), accent, 13).parent.At(72, 40);
            var info = Theme.Scroll(box, "Info").Place(14, 70, 278, 100);
            Theme.Label(info, stats, 16, Theme.Ink);
            Theme.Label(info, cards, 16, Theme.TextDim).style.marginTop = 4;
            Theme.Hover(info, $"{name}", stats, cards);
            var l = Theme.Button(box, "◀", prev, null, 18, "btn-arrow").Place(228, 8, 32, 34);
            var r = Theme.Button(box, "▶", next, null, 18, "btn-arrow").Place(262, 8, 32, 34);
            l.SetEnabled(options > 1); r.SetEnabled(options > 1);
            // Hovering an arrow previews the part it would switch to on the stats sheet.
            foreach (var (btn, dir) in new[] { (l, -1), (r, 1) })
            {
                btn.RegisterCallback<PointerEnterEvent>(_ => { if (sheet != null) DrawSheet(BuildStats.Of(C, G.Current), BuildStats.Of(C, preview(dir))); });
                btn.RegisterCallback<PointerLeaveEvent>(_ => { if (sheet != null) DrawSheet(BuildStats.Of(C, G.Current), null); });
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
            sheet.Clear();
            var rows = SheetRows(s);
            var prev = preview == null ? null : SheetRows(preview);
            Theme.Label(sheet, preview == null ? "Passe o mouse em ◀ ou ▶ de um slot para comparar a peça." : "Comparando com a peça do botão sob o mouse.",
                Theme.Small, preview == null ? Theme.TextDim : Theme.Warn, TextAnchor.MiddleLeft).Place(20, 10, 720, 30);
            var list = Theme.Scroll(sheet, "Rows").Place(8, 44, 744, 716);
            for (int i = 0; i < rows.Count; i++)
            {
                var (help, label, value, number, higherBetter, icon) = rows[i];
                var row = Theme.Row(list, "Row:" + help, 8);
                row.AddToClassList("sheet-row");
                Theme.HelpIcon(row, help, 26);
                if (icon.HasValue) SlotIcons.Icon(row, icon.Value, 30, Theme.Ink);
                var shownLabel = prev != null && icon.HasValue ? prev[i].label : label;
                Theme.Label(row, shownLabel, 19, icon.HasValue ? Theme.Ink : Theme.TextDim, TextAnchor.MiddleLeft).Size(icon.HasValue ? 214 : 252, 34).OneLine();
                var shownValue = prev != null ? prev[i].value : value;
                var v = Theme.Label(row, shownValue, 20, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                v.style.flexGrow = 1; v.style.flexShrink = 1;
                if (prev != null && number.HasValue && prev[i].number.HasValue && Math.Abs(prev[i].number.Value - number.Value) > 0.001f)
                {
                    float d = prev[i].number.Value - number.Value;
                    bool good = higherBetter ? d > 0 : d < 0;
                    string txt = (d > 0 ? "+" : "") + (Math.Abs(d % 1) > 0.001f ? d.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) : ((int)d).ToString());
                    Theme.Chip(row, txt, good ? Theme.Good : Theme.Bad, 18).parent.AddToClassList("delta");
                }
                else if (prev != null && !number.HasValue && prev[i].value != value)
                    Theme.Chip(row, "muda", Theme.Warn, 16).parent.AddToClassList("delta");
            }
        }

        void DeckGrid(VisualElement parent, List<CardDef> deck)
        {
            const float s = 0.6f;
            var grid = Theme.El(parent, "Grid", "card-grid");
            var srcs = new[] { (G.Current.head, "Cabeca"), (G.Current.torso, "Torso"), (G.Current.armL, "BracoEsq"), (G.Current.armR, "BracoDir"), (G.Current.legs, "Pernas") };
            foreach (var d in deck)
            {
                var src = srcs.FirstOrDefault(p => C.Part(p.Item1).cards.Contains(d.id));
                string maker = src.Item1 != null ? C.Part(src.Item1).maker : null;
                var slot = Theme.El(grid, "CardSlot", "card-slot").Size(CardView.W * s, CardView.H * s);
                var card = CardView.Create(slot, d, maker, src.Item2 ?? "Frame", null, src.Item1 != null ? C.Part(src.Item1).element : null);
                card.interactive = false;
                card.Root.AddToClassList("mini");
                card.Root.style.scale = new Scale(new Vector3(s, s, 1));
                card.SetState(true, d.cost, Kinds.TargetBadge(d, d.cost >= 2 || d.choose), false, false);
            }
        }
    }
}
