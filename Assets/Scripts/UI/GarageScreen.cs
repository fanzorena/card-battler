using System;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattler.UI
{
    // Garage: robot in the middle with its slots around it, deck as mini cards, opponent dossier as an in-world file.
    public class GarageScreen
    {
        readonly GameUI app;
        readonly RectTransform root;
        static int opponentIndex;
        static bool showDossier = true;

        public GarageScreen(GameUI app, RectTransform root) { this.app = app; this.root = root; }

        Content C => app.content;
        GarageSave G => app.garage;
        RobotDef[] Opponents => C.robots.Where(r => !string.IsNullOrEmpty(r.tier)).OrderBy(r => Array.IndexOf(RobotDef.Tiers, r.tier)).ToArray();
        RobotDef Opponent => Opponents[((opponentIndex % Opponents.Length) + Opponents.Length) % Opponents.Length];

        static readonly (string label, string slot, Func<RobotDef, string> get, Action<RobotDef, string> set, float x, float y)[] Slots =
        {
            // first item: slot icon key (the icon replaces the slot name)
            ("Cabeca", "Cabeca", b => b.head, (b, v) => b.head = v, 410, 120),
            (SlotIcons.Nucleo, "Nucleo", b => b.core, (b, v) => b.core = v, 760, 120),
            ("BracoEsq", "Braco", b => b.armL, (b, v) => b.armL = v, 60, 300),
            ("BracoDir", "Braco", b => b.armR, (b, v) => b.armR = v, 760, 300),
            ("Torso", "Torso", b => b.torso, (b, v) => b.torso = v, 60, 480),
            ("Pernas", "Pernas", b => b.legs, (b, v) => b.legs = v, 760, 480),
        };

        void Changed() { G.Save(GameUI.SavePath); Rebuild(); }

        void Rebuild()
        {
            foreach (Transform t in root) UnityEngine.Object.Destroy(t.gameObject);
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
            Theme.Label(root, "Monte o robô e escolha o adversário", Theme.Small, Theme.TextDim).rectTransform.Place(62, 80, 500, 30);
            for (int i = 0; i < GarageSave.SlotCount; i++)
            {
                int k = i;
                bool sel = G.selected == i;
                var btn = Theme.Button(root, G.slots[i].name, () => { G.selected = k; Changed(); }, sel ? Theme.Mix(Theme.PanelHi, Theme.Warn, 0.35f) : Theme.Panel);
                ((RectTransform)btn.transform).Place(420 + i * 180, 34, 170, 50);
            }

            // Robot area
            var area = Theme.Box(root, "Area", Theme.Alpha(Theme.Panel, 0.6f));
            area.rectTransform.Place(40, 110, 1040, 880);
            var robot = RobotView.Create(root, false, false);
            robot.Rect.Center(560, 555, RobotView.W, RobotView.H);
            robot.transform.localScale = new Vector3(1.15f, 1.15f, 1);
            robot.Build(C, b);

            // Frame selector (top-left slot)
            SlotWidget(60, 120, "Frame", frame.name, null, $"Chassi {frame.chassis} · Mão {frame.hand} · Carga {frame.carry}",
                frame.passiveText, () => { b.frame = Cycle(C.frames.Select(f => f.id).ToArray(), b.frame, -1); Changed(); },
                () => { b.frame = Cycle(C.frames.Select(f => f.id).ToArray(), b.frame, 1); Changed(); });

            foreach (var (label, slot, get, set, x, y) in Slots)
            {
                var ids = C.PartsFor(slot).Select(p => p.id).ToArray();
                var part = C.Part(get(b));
                string stats = slot == "Nucleo"
                    ? $"{part.energy} energia/turno{(part.heatPerTurn > 0 ? $" · +{part.heatPerTurn} Calor/turno" : "")} · Peso {part.weight}"
                    : $"Integridade {part.integrity} · Peso {part.weight}{(string.IsNullOrEmpty(part.element) ? "" : " · " + part.element)}";
                string cards = string.Join(", ", part.cards.GroupBy(id => id).Select(g => $"{g.Count()}x {C.Card(g.Key).name}"));
                SlotWidget(x, y, label, part.name, part.maker, stats, cards,
                    () => { set(b, Cycle(ids, get(b), -1)); Changed(); }, () => { set(b, Cycle(ids, get(b), 1)); Changed(); });
            }

            // Weight bar, bonuses, totals
            int weight = C.Weight(b);
            var (fill, wl) = Theme.Bar(root, weight > frame.carry ? Theme.Bad : Theme.Good, Theme.Body);
            ((RectTransform)fill.transform.parent).Place(60, 820, 1000, 40);
            fill.fillAmount = Mathf.Clamp01((float)weight / frame.carry);
            wl.text = $"PESO {weight} / {frame.carry}" + (weight > frame.carry ? "  — acima da carga do frame" : "");
            var makers = C.ActiveMakers(b);
            float bx = 60;
            if (makers.Length == 0)
                Theme.Label(root, "Sem bônus de fabricante (3 partes do mesmo fabricante ativam)", Theme.Small, Theme.TextDim).rectTransform.Place(60, 874, 1000, 30);
            foreach (var m in makers)
            {
                var chip = Theme.Chip(root, $"{m.id.ToUpperInvariant()}: {m.text}", Theme.Maker(m.id), Theme.Small);
                var rt = (RectTransform)chip.transform.parent;
                float w = chip.preferredWidth + 30;
                rt.Place(bx, 872, w, 36);
                bx += w + 10;
            }
            Theme.Label(root, $"Chassi {frame.chassis}  ·  Integridade das partes {b.PartIds.Sum(id => C.Part(id).integrity)}  ·  Energia {C.Part(b.core).energy}/turno  ·  Mão {frame.hand}",
                Theme.Body, Theme.TextDim).rectTransform.Place(60, 920, 1000, 34);

            // Right column: tabs
            var deck = C.Deck(b);
            var tabDeck = Theme.Button(root, $"DECK ({deck.Count})", () => { showDossier = false; Rebuild(); }, showDossier ? Theme.Panel : Theme.PanelHi);
            ((RectTransform)tabDeck.transform).Place(1120, 110, 240, 54);
            var tabDos = Theme.Button(root, "DOSSIÊ DO ADVERSÁRIO", () => { showDossier = true; Rebuild(); }, showDossier ? Theme.PanelHi : Theme.Panel);
            ((RectTransform)tabDos.transform).Place(1370, 110, 340, 54);
            var content = Theme.Node(root, "TabContent").Place(1120, 176, 760, 680);
            if (showDossier) Dossier(content, Opponent); else DeckGrid(content, deck);

            // Opponent picker + fight
            var op = Opponent;
            var opBox = Theme.Box(root, "Opponent", Theme.Panel);
            opBox.rectTransform.Place(1120, 870, 760, 84);
            Theme.Label(opBox.transform, "ADVERSÁRIO", Theme.Small, Theme.TextDim, style: FontStyle.Bold).rectTransform.Place(20, 8, 300, 24);
            Theme.Label(opBox.transform, $"{op.name}  <color=#{Theme.HexOf(TierColor(op.tier))}>{op.tier.ToUpperInvariant()}</color>", 26, Theme.Ink, style: FontStyle.Bold).rectTransform.Place(20, 34, 600, 40);
            ((RectTransform)Theme.Button(opBox.transform, "<", () => { opponentIndex--; showDossier = true; Rebuild(); }).transform).Place(620, 16, 56, 52);
            ((RectTransform)Theme.Button(opBox.transform, ">", () => { opponentIndex++; showDossier = true; Rebuild(); }).transform).Place(686, 16, 56, 52);

            var errors = C.Validate(b);
            if (errors.Count > 0)
                Theme.Label(root, string.Join("\n", errors), Theme.Body, Theme.Bad).rectTransform.Place(1120, 970, 380, 90);
            var fight = Theme.Button(root, "LUTAR", () => app.StartBattle(b, op.id), Theme.Mix(Theme.PanelHi, Theme.Exposed, 0.6f), 34);
            ((RectTransform)fight.transform).Place(1520, 970, 360, 84);
            fight.interactable = errors.Count == 0;

            var log = Theme.Button(root, (BattleLogger.MirrorToConsole ? "[x]" : "[ ]") + " Log no console", () => { BattleLogger.MirrorToConsole = !BattleLogger.MirrorToConsole; Rebuild(); }, Theme.Panel, Theme.Small);
            ((RectTransform)log.transform).Place(60, 1010, 260, 44);
        }

        public static Color TierColor(string tier) => tier switch
        {
            "Fácil" => Theme.Good, "Moderado" => Theme.Warn, "Difícil" => Theme.Exposed, "Chefe" => Theme.Bad, _ => Theme.TextDim,
        };

        void SlotWidget(float x, float y, string iconKey, string name, string maker, string stats, string cards, Action prev, Action next)
        {
            var accent = maker == null ? Theme.TextDim : Theme.Maker(maker);
            var box = Theme.Box(root, "Slot:" + iconKey, Theme.Panel);
            box.rectTransform.Place(x, y, 300, 170);
            var o = box.gameObject.AddComponent<Outline>(); o.effectColor = Theme.Alpha(accent, 0.5f); o.effectDistance = new Vector2(2, -2);
            Theme.Solid(box.transform, "Stripe", accent).rectTransform.Place(0, 10, 5, 150);
            SlotIcons.Icon(box.transform, iconKey, 52, maker == null ? Theme.Warn : accent).Place(12, 8, 52, 52);
            var n = Theme.Label(box.transform, name, 24, Theme.Ink, style: FontStyle.Bold);
            n.rectTransform.Place(72, 6, 156, 32);
            n.resizeTextForBestFit = true; n.resizeTextMinSize = 15; n.resizeTextMaxSize = 24;
            if (maker != null)
            {
                var chip = Theme.Chip(box.transform, maker.ToUpperInvariant(), accent, 14);
                ((RectTransform)chip.transform.parent).Place(72, 40, 124, 22);
            }
            Theme.Label(box.transform, stats, 16, Theme.Ink).rectTransform.Place(16, 70, 276, 40);
            Theme.Label(box.transform, cards, 15, Theme.TextDim).rectTransform.Place(16, 112, 276, 54);
            ((RectTransform)Theme.Button(box.transform, "<", prev, Theme.PanelHi).transform).Place(232, 8, 30, 32);
            ((RectTransform)Theme.Button(box.transform, ">", next, Theme.PanelHi).transform).Place(264, 8, 30, 32);
        }

        void DeckGrid(RectTransform parent, System.Collections.Generic.List<CardDef> deck)
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

        void Dossier(RectTransform parent, RobotDef op)
        {
            var paper = Theme.Box(parent, "File", Theme.Paper);
            paper.rectTransform.Fill();
            var o = paper.gameObject.AddComponent<Outline>(); o.effectColor = Theme.Alpha(Theme.Hex("6B5A3E"), 0.8f); o.effectDistance = new Vector2(2, -2);
            Theme.Solid(paper.transform, "Header", Theme.Hex("2B251D")).rectTransform.Place(0, 0, 760, 64);
            var title = Theme.Label(paper.transform, "DOSSIÊ // " + op.name.ToUpperInvariant(), 26, Theme.Hex("E8D9B8"), TextAnchor.MiddleLeft, FontStyle.Bold);
            title.rectTransform.Place(24, 0, 560, 64);
            title.horizontalOverflow = HorizontalWrapMode.Wrap; title.resizeTextForBestFit = true; title.resizeTextMinSize = 16; title.resizeTextMaxSize = 26;
            var stamp = Theme.Chip(paper.transform, op.tier.ToUpperInvariant(), TierColor(op.tier), 22);
            var srt = (RectTransform)stamp.transform.parent;
            srt.Place(590, 12, 150, 42); srt.localRotation = Quaternion.Euler(0, 0, -6);

            var d = op.dossier;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<color=#B9A98A>FABRICANTE PRINCIPAL</color>   <color=#{Theme.HexOf(Theme.Maker(C.MainMaker(op)))}><b>{C.MainMaker(op)}</b></color>");
            sb.AppendLine($"<color=#B9A98A>COMO LUTA</color>   {C.Describe(op)}.");
            sb.AppendLine();
            if (d == null || !d.Exists) sb.AppendLine("<i>Ninguém no bar perde tempo falando desse aí. Adversário de treino.</i>");
            else
            {
                sb.AppendLine("<color=#B9A98A>RUMORES</color>");
                foreach (var r in d.rumors) sb.AppendLine($"<i>{r}</i>\n");
                sb.AppendLine($"<color=#{Theme.HexOf(Theme.Good)}><b>FUNCIONA BEM</b></color>");
                foreach (var t in d.worksWell) sb.AppendLine("• " + Theme.Highlight(t));
                sb.AppendLine();
                sb.AppendLine($"<color=#{Theme.HexOf(Theme.Bad)}><b>EVITE</b></color>");
                foreach (var t in d.avoid) sb.AppendLine("• " + Theme.Highlight(t));
            }
            var body = Theme.Label(paper.transform, sb.ToString(), 21, Theme.Hex("E8E1D0"));
            body.rectTransform.Place(28, 84, 704, 580);
            body.lineSpacing = 1.05f;
        }
    }
}
