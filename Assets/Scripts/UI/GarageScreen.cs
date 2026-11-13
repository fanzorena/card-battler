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

        // Slot cards around the robot: (slot key, x, y).
        static readonly (string key, float x, float y)[] PartSlots =
        {
            ("Cabeca", 400, 124), ("Nucleo", 740, 124), ("BracoEsq", 60, 316), ("BracoDir", 740, 316), ("Torso", 60, 508), ("Pernas", 740, 508),
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
            Theme.Button(root, app.careerMode ? "←  VOLTAR" : "←  MENU", app.Back, null, 22).Place(1670, 30, 210, 54);

            // Robot area
            Theme.Plate(root, "Area", "panel-glass").Place(40, 110, 1040, 880);
            Theme.El(root, "Pedestal", "pedestal").Center(560, 836, 520, 90);
            RobotView.Create(root, false, true).At(560, 600, 0.9f).Build(C, b);

            var frames = C.frames.Where(f => Car == null || Car.OwnsFrame(f)).Select(f => f.id).ToArray();
            SlotWidget(60, 124, "Frame", frame.name, null, $"Chassi {frame.chassis} · Mão {frame.hand} · Carga {frame.carry}", frame.passiveText,
                () => { b.frame = Cycle(frames, b.frame, -1); Changed(); }, () => { b.frame = Cycle(frames, b.frame, 1); Changed(); },
                dir => With(b, x => x.frame = Cycle(frames, b.frame, dir)), frames.Length);

            foreach (var (key, x, y) in PartSlots) PartCard(x, y, key);

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
            var tabSheet = Theme.Button(tabRow, "FICHA DO ROBÔ", () => { showDeck = false; pick = null; Rebuild(); }, null, Theme.Body, "btn-tab").Size(260, 56);
            tabSheet.EnableInClassList("selected", pick == null && !showDeck);
            var tabDeck = Theme.Button(tabRow, $"DECK ({deck.Count})", () => { showDeck = true; pick = null; Rebuild(); }, null, Theme.Body, "btn-tab").Size(200, 56);
            tabDeck.EnableInClassList("selected", pick == null && showDeck);
            var tabParts = Theme.Button(tabRow, "PEÇAS", () => { pick ??= RobotDef.SlotKeys.FirstOrDefault(k => string.IsNullOrEmpty(b.Get(k))) ?? "Cabeca"; Rebuild(); }, null, Theme.Body, "btn-tab").Size(180, 56);
            tabParts.name = "Pecas";
            tabParts.EnableInClassList("selected", pick != null);
            Theme.Hover(tabParts, "Equipar, trocar e desequipar peças. Também abre ao clicar num slot à esquerda.");
            var panel = Theme.Plate(root, "TabContent").Place(1120, 176, 760, 770);
            if (pick != null) Inventory(panel, pick);
            else if (showDeck) DeckGrid(Theme.Scroll(panel).Fill(12), deck);
            else { sheet = panel; DrawSheet(BuildStats.Of(C, b), null); }

            // Continue to the opponent selection
            var go = Theme.Primary(root, "CONTINUAR  →", app.careerMode ? () => app.ShowCareer(app.careerHub) : app.ShowOpponents, 32);
            go.name = "Continuar";
            go.Place(1520, 966, 360, 88);
            go.SetEnabled(blocker == null);
            if (blocker != null) Theme.Label(root, "Ajuste o robô para continuar", Theme.Small, Theme.Bad, TextAnchor.MiddleRight).Place(1120, 990, 390, 40);

            // Wheels skid on the Fosso scrap floor: point at the legs slot when the player owns other legs.
            if (Car != null && !string.IsNullOrEmpty(b.legs) && C.Part(b.legs).wheels && Car.owned.Any(id => C.Part(id) is { slot: "Pernas", wheels: false }))
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
            Theme.Label(box, name, 22, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold).Place(72, 6, 220, 32).OneLine();
            if (maker != null) Theme.Chip(box, maker.ToUpperInvariant(), accent, 13).parent.At(72, 40);
            var info = Theme.Scroll(box, "Info").Place(14, 70, 278, 100);
            Theme.Label(info, stats, 17, Theme.Ink);
            Theme.Label(info, cards, 17, Theme.TextDim).style.marginTop = 4;
            Theme.Hover(info, $"{name}", stats, cards);
            var l = Theme.Button(box, "◀", prev, null, 18, "btn-arrow").Place(222, 36, 34, 32);
            var r = Theme.Button(box, "▶", next, null, 18, "btn-arrow").Place(258, 36, 34, 32);
            l.SetEnabled(options > 1); r.SetEnabled(options > 1);
            // Hovering an arrow previews the part it would switch to on the stats sheet.
            foreach (var (btn, dir) in new[] { (l, -1), (r, 1) })
            {
                btn.RegisterCallback<PointerEnterEvent>(_ => { if (sheet != null) DrawSheet(BuildStats.Of(C, G.Current), BuildStats.Of(C, preview(dir))); });
                btn.RegisterCallback<PointerLeaveEvent>(_ => { if (sheet != null) DrawSheet(BuildStats.Of(C, G.Current), null); });
            }
        }

        // ---- Parts: slot cards on the left, inventory with comparison on the right ----
        static string pick; // slot key whose inventory is open (null = stats sheet / deck)

        static string SlotTitle(string key) => key switch
        {
            "Cabeca" => "CABEÇA", "Torso" => "TORSO", "BracoEsq" => "BRAÇO ESQUERDO", "BracoDir" => "BRAÇO DIREITO", "Pernas" => "PERNAS", "Nucleo" => "NÚCLEO", _ => key,
        };

        string PartStats(PartDef part) => part.slot == "Nucleo"
            ? $"{part.energy} energia/turno{(part.heatPerTurn > 0 ? $" · +{part.heatPerTurn} Calor/turno" : "")} · Peso {part.weight}"
            : $"Integridade {(Car != null && Car.HpOf(C, part.id) < part.integrity ? $"<color=#{Theme.HexOf(Theme.Bad)}>{Car.HpOf(C, part.id)}</color>/" : "")}{part.integrity} · Peso {part.weight}{(string.IsNullOrEmpty(part.element) ? "" : " · " + part.element)}{(string.IsNullOrEmpty(part.resist) ? "" : " · resiste " + part.resist)}{(string.IsNullOrEmpty(part.weakTo) ? "" : " · fraca a " + part.weakTo)}";

        string PartCards(PartDef part) => string.Join(", ", part.cards.GroupBy(id => id).Select(g => $"{g.Count()}x {C.Card(g.Key).name}"));

        // A slot on the left: click it to open its inventory on the right.
        void PartCard(float x, float y, string key)
        {
            var b = G.Current;
            var id = b.Get(key);
            var part = string.IsNullOrEmpty(id) ? null : C.Part(id);
            var accent = part == null ? Theme.TextDim : Theme.Maker(part.maker);
            var box = Theme.Plate(root, "Slot:" + key, "slot-card").Place(x, y, 300, 176).OnClick(() => { pick = pick == key ? null : key; Rebuild(); });
            box.EnableInClassList("picked", pick == key);
            box.EnableInClassList("empty", part == null);
            Theme.Border(box, pick == key ? Theme.Warn : Theme.Alpha(accent, 0.55f), pick == key ? 3 : 2);
            Theme.Solid(box, "Stripe", accent).Place(0, 12, 5, 150);
            SlotIcons.Icon(box, key == "Nucleo" ? SlotIcons.Nucleo : key, 52, part == null ? Theme.TextDim : accent).Place(12, 10, 52, 52);
            if (part == null)
            {
                Theme.Title(box, "VAZIO", 22, Theme.Bad).At(74, 12);
                Theme.Label(box, SlotTitle(key).ToLowerInvariant(), Theme.Small, Theme.TextDim).At(74, 42);
                Theme.Label(box, "Clique para equipar uma peça.", 17, Theme.Warn).Place(14, 84, 276, 60);
                Theme.Hover(box, $"{SlotTitle(key)}: sem peça. O robô só luta com todas as peças.");
                return;
            }
            Theme.Label(box, part.name, 22, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold).Place(72, 6, 220, 32).OneLine();
            Theme.Chip(box, part.maker.ToUpperInvariant(), accent, 13).parent.At(72, 40);
            Theme.Label(box, "TROCAR", 14, Theme.Warn, TextAnchor.MiddleRight, FontStyle.Bold).Place(196, 40, 92, 24);
            var info = Theme.El(box, "Info").Place(14, 72, 278, 98);
            info.style.overflow = Overflow.Hidden;
            Theme.Label(info, PartStats(part), 17, Theme.Ink);
            Theme.Label(info, PartCards(part), 17, Theme.TextDim).style.marginTop = 4;
            Theme.Hover(box, part.name, PartStats(part), PartCards(part), "Clique para trocar ou desequipar.");
        }

        // Copies of a part this robot could take: free copies plus, in career, copies sitting on other robots.
        (int free, string onRobot) Availability(string partId)
        {
            if (Car == null) return (99, null);
            int free = Car.Free(partId);
            var other = G.slots.FirstOrDefault(r => r != G.Current && r.Equipped.Contains(partId));
            return (free, other?.name);
        }

        void Equip(string key, string partId)
        {
            var b = G.Current;
            if (Car != null && Car.Free(partId) <= 0)
            {
                // take it from the other robot that has it
                var other = G.slots.First(r => r != b && r.Equipped.Contains(partId));
                other.Set(SlotOf(other, partId), null);
            }
            b.Set(key, partId);
            Sfx.Play(s => s.repair, 0.5f, 1.2f);
            Changed();
        }

        static string SlotOf(RobotDef r, string partId) => RobotDef.SlotKeys.First(k => r.Get(k) == partId);

        // Number rows that change when the build changes (label, delta text, good?).
        static List<(string label, string delta, bool good)> Deltas(BuildStats now, BuildStats then)
        {
            var a = SheetRows(now); var b = SheetRows(then);
            var list = new List<(string, string, bool)>();
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].icon.HasValue || !a[i].number.HasValue || !b[i].number.HasValue) continue; // per-part integrity rows: the total says it
                float d = b[i].number.Value - a[i].number.Value;
                if (Math.Abs(d) < 0.001f) continue;
                string label = a[i].help == "peso" ? "folga de carga" : a[i].label.ToLowerInvariant();
                string txt = (d > 0 ? "+" : "") + (Math.Abs(d % 1) > 0.001f ? d.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) : ((int)d).ToString());
                list.Add((label, txt, a[i].higherIsBetter ? d > 0 : d < 0));
            }
            return list;
        }

        void Inventory(VisualElement panel, string key)
        {
            var b = G.Current;
            var currentId = b.Get(key);
            var current = string.IsNullOrEmpty(currentId) ? null : C.Part(currentId);
            var head = Theme.El(panel, "Head").Place(20, 14, 720, 92);
            SlotIcons.Icon(head, key == "Nucleo" ? SlotIcons.Nucleo : key, 48, Theme.Warn).At(0, 4);
            Theme.Title(head, SlotTitle(key), 26).At(62, 0);
            Theme.Label(head, current == null ? "Vazio: escolha uma peça abaixo." : $"Equipada: <b>{current.name}</b>", Theme.Small, current == null ? Theme.Bad : Theme.Ink).Place(62, 34, 420, 26).OneLine();
            Theme.Label(head, "Os números mostram o que muda no robô.", 16, Theme.TextDim).Place(62, 62, 440, 24).OneLine();
            if (current != null)
            {
                var off = Theme.Button(head, "DESEQUIPAR", () => { b.Set(key, null); Sfx.Play(s => s.back, 0.5f); Changed(); }, null, 18, "btn-danger");
                off.name = "Desequipar";
                off.Place(520, 8, 200, 50);
                Theme.Hover(off, "Tira a peça do robô e devolve ao estoque. Robô incompleto não luta.");
            }

            var list = Theme.Scroll(panel, "Parts").Place(12, 114, 736, 644);
            var now = BuildStats.Of(C, b);
            var candidates = C.PartsFor(RobotDef.PartSlot(key)).Where(p => Car == null || Car.Owns(p))
                .OrderByDescending(p => p.id == currentId).ThenBy(p => p.name).ToList();
            if (candidates.Count == 0) Theme.Label(list, "Nenhuma peça desse tipo no estoque. O Ferro-velho vende peças.", Theme.Body, Theme.TextDim).Classes("prose");
            int i = 0;
            foreach (var p in candidates)
            {
                bool on = p.id == currentId;
                var (free, onRobot) = Availability(p.id);
                bool available = on || free > 0 || onRobot != null;
                var row = Theme.El(list, "Part:" + p.id, "inventory-row");
                row.EnableInClassList("equipped", on);
                var accent = Theme.Maker(p.maker);
                Theme.Border(row, on ? Theme.Warn : Theme.Alpha(accent, 0.35f), on ? 2 : 1);
                var top = Theme.Row(row, "Top", 10);
                top.style.alignItems = Align.Center;
                SlotIcons.Icon(top, key == "Nucleo" ? SlotIcons.Nucleo : key, 40, accent);
                var nm = Theme.Label(top, p.name, 22, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold).OneLine();
                nm.style.flexShrink = 1;
                Theme.Chip(top, p.maker.ToUpperInvariant(), accent, 13);
                var spacer = Theme.El(top, "Spacer"); spacer.style.flexGrow = 1;
                string where = on ? "EQUIPADA" : Car == null ? "" : free > 0 ? $"{free} no estoque" : onRobot != null ? $"no {onRobot}" : "";
                Theme.Label(top, where, 16, on ? Theme.Warn : free > 0 ? Theme.Good : Theme.TextDim, TextAnchor.MiddleRight, FontStyle.Bold);
                if (!on)
                {
                    var eq = Theme.Button(top, free > 0 || Car == null ? "EQUIPAR" : "PEGAR", () => Equip(key, p.id), null, 18, "btn-good").Size(150, 46);
                    eq.name = "Equipar:" + p.id;
                    eq.SetEnabled(available);
                    if (onRobot != null && free <= 0) Theme.Hover(eq, $"Tira a peça do {onRobot} e coloca neste robô.");
                }
                Theme.Label(row, PartStats(p), 17, Theme.Ink).style.marginTop = 6;
                Theme.Label(row, PartCards(p), 17, Theme.TextDim);
                if (!on)
                {
                    var chips = Theme.El(row, "Deltas", "delta-row");
                    var then = BuildStats.Of(C, With(b, r => r.Set(key, p.id)));
                    var deltas = Deltas(now, then);
                    if (deltas.Count == 0) Theme.Label(chips, "sem mudança nos números", 16, Theme.TextDim);
                    foreach (var (label, delta, good) in deltas) Theme.Chip(chips, $"{delta} {label}", good ? Theme.Good : Theme.Bad, 15);
                    if (C.Weight(With(b, r => r.Set(key, p.id))) > C.Frame(b.frame).carry) Theme.Chip(chips, "PASSA DA CARGA", Theme.Bad, 15);
                }
                Fx.Enter(row, i++);
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
            Theme.Label(sheet, preview == null ? "Clique num slot à esquerda para trocar a peça e comparar. Passe o mouse no ? para ver o que cada número significa." : "Comparando com o frame do botão sob o mouse.",
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
                var src = srcs.FirstOrDefault(p => !string.IsNullOrEmpty(p.Item1) && C.Part(p.Item1).cards.Contains(d.id));
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
