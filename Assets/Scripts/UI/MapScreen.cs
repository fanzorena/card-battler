using System;
using System.Collections.Generic;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace CardBattler.UI
{
    // The port: a 2D map with points of interest (no free movement, no clock). Clicking a place opens it;
    // the bar opens a linear dialogue with Celeste (and, after the Fosso title, an old bettor).
    public class MapScreen
    {
        readonly GameUI app;
        readonly VisualElement root;
        VisualElement talkLayer;

        public MapScreen(GameUI app, VisualElement root) { this.app = app; this.root = root; }

        Content C => app.content;
        Career K => app.career;

        // id, name, subtitle, position, color. Locked teasers ("ferro", "neon") show their name.
        public static readonly (string id, string name, string sub, float x, float y, string color)[] Porto =
        {
            ("bar", "Bar Fim da Linha", "Celeste arruma lutas", 430, 640, "FFC24A"),
            ("oficina", "Oficina do Cais", "Reparos e garagem", 900, 380, "4AD6FF"),
            ("fosso", "O Fosso", "Ranking até o campeão", 1360, 620, "FF5A4A"),
            ("ferro", "Ferro-velho", "Compra, venda e plantas", 1560, 300, "C8A27A"),
            ("neon", "Trem para o Neon", "Distrito Neon", 200, 300, "B45AFF"),
        };
        public static readonly (string id, string name, string sub, float x, float y, string color)[] Neon =
        {
            ("circuito", "Circuito Regional", "Lutas nos telhados", 1200, 560, "4AD6FF"),
            ("glitch", "Esconderijo da Glitch", "Hacker e informante", 620, 420, "B45AFF"),
            ("porto", "Trem para o porto", "Voltar ao porto", 240, 680, "FFC24A"),
        };

        bool InNeon => K.region == "neon";
        public (string id, string name, string sub, float x, float y, string color)[] Places => InNeon ? Neon : Porto;

        // Distrito Neon places open together with the region (first clue).
        public bool IsOpen(string id) => K.IsOpen(id is "circuito" or "glitch" or "porto" ? "neon" : id);

        public void Build()
        {
            if (InNeon) DrawNeon(); else DrawPort();
            Theme.Title(root, InNeon ? "DISTRITO NEON" : "O PORTO", 44).At(60, 26);
            Theme.Label(root, "Escolha aonde ir", Theme.Small, Theme.TextDim).Place(62, 80, 500, 30);
            Wallet(root, K, 900);
            var clues = Theme.Button(root, $"PISTAS ({K.clues.Length})", Journal, null, 22);
            clues.EnableInClassList("btn-highlight", K.clues.Length > 0);
            clues.name = "Pistas";
            clues.Place(1540, 30, 170, 54);
            Theme.Button(root, "←  MENU", () => app.ShowTitle(), null, 22).Place(1720, 30, 160, 54);

            foreach (var p in Places) Poi(p);

            // Where the story stands, in one line.
            string hint = InNeon ? (K.GlitchReady ? "A Glitch achou alguma coisa. Vá até o esconderijo dela."
                    : !K.Seen("glitch_first") ? "Procure a Glitch. Ela acha qualquer registro."
                    : K.LadderTop(C, "circuit") ? "Campeão do Circuito Regional. A Glitch continua investigando a Lumen Holding."
                    : "Suba no Circuito Regional: duas vitórias e a Glitch consegue o acesso.")
                : K.IsOpen("neon") && !K.Seen("glitch_first") ? "O trem para o Distrito Neon está liberado. A Celeste quer que você procure a Glitch."
                : K.InJob ? $"Trabalho em andamento: luta {Math.Min(K.stage + 1, C.Job(K.job).stages.Length)} de {C.Job(K.job).stages.Length}. Fale com a Celeste no bar."
                : !K.IsOpen("fosso") ? "Fale com a Celeste no Bar Fim da Linha."
                : K.BettorWaiting(C) ? "Um apostador velho quer falar com você no bar."
                : K.Champion(C) ? "Campeão dos Fossos. A Celeste tem mais trabalho no bar."
                : "Suba no ranking do Fosso ou pegue outro trabalho com a Celeste.";
            if (!string.IsNullOrEmpty(K.lastResult)) hint = K.lastResult + "\n" + hint;
            var bar = Theme.Plate(root, "Hint").Place(60, 950, 1300, 104);
            Theme.Icon(bar, "information", 40, Theme.Warn).At(22, 32);
            Theme.Label(bar, hint, 22, Theme.Ink, TextAnchor.MiddleLeft).Place(80, 6, 1196, 92).Fit(Theme.Small);

            talkLayer = Theme.El(root, "Talk").Fill();
        }

        // Night skyline: building silhouettes with lit windows and neon signs (seeded, same every time).
        void Skyline(int seed, float neonChance, int minH, int maxH)
        {
            var rng = new System.Random(seed);
            var neon = new[] { "FF5AD8", "4AD6FF", "FFC24A", "7CFF6B" };
            for (float x = 0; x < 1920;)
            {
                float w = rng.Next(70, 150), h = rng.Next(minH, maxH);
                var bld = Theme.Solid(root, "Building", Theme.Mix(Theme.Hex("0E1018"), Theme.Hex("1C2033"), (float)rng.NextDouble()));
                bld.Place(x, 760 - h, w - 6, h);
                for (float wy = 760 - h + 16; wy < 740; wy += 26)
                    for (float wx = x + 10; wx < x + w - 20; wx += 22)
                        if (rng.NextDouble() < 0.22) Theme.Solid(root, "Window", Theme.Alpha(Theme.Hex(rng.NextDouble() < 0.7 ? "FFC24A" : "4AD6FF"), 0.35f)).Place(wx, wy, 10, 12);
                if (rng.NextDouble() < neonChance)
                {
                    var col = Theme.Hex(neon[rng.Next(neon.Length)]);
                    Theme.Solid(root, "Neon", Theme.Alpha(col, 0.7f)).Place(x + 8, 760 - h + 30, 6, Mathf.Min(120, h - 60));
                    Theme.Box(root, "NeonGlow", Theme.Alpha(col, 0.05f), Shape.Circle).Center(x + 11, 760 - h + 90, 50, 150);
                }
                x += w;
            }
        }

        // Distrito Neon: taller towers, more signs, magenta haze, rain over the rooftops.
        void DrawNeon()
        {
            Skyline(11, 0.6f, 260, 640);
            for (int i = 0; i < 4; i++) Theme.Solid(root, "Haze", Theme.Alpha(Theme.Hex("5A1C6A"), 0.14f)).Place(0, 480 + i * 70, 1920, 70);
            Theme.Solid(root, "Roof", Theme.Hex("161924")).Place(0, 760, 1920, 320);
            Theme.Solid(root, "RoofEdge", Theme.Alpha(Theme.Hex("4AD6FF"), 0.4f)).Place(0, 760, 1920, 4);
            for (int i = 0; i < 8; i++) Theme.Solid(root, "Puddle", Theme.Alpha(Theme.Hex(i % 2 == 0 ? "FF5AD8" : "4AD6FF"), 0.1f)).Place(120 + i * 230, 820 + (i % 3) * 40, 120, 6);
            var rng = new System.Random(3);
            var rain = new List<VisualElement>();
            for (int i = 0; i < 90; i++)
            {
                var drop = Theme.Solid(root, "Rain", Theme.Alpha(Theme.Hex("9EB8FF"), 0.18f));
                drop.Center(rng.Next(0, 1920), rng.Next(100, 1000), 2, rng.Next(18, 40));
                drop.Rotate(-12);
                rain.Add(drop);
            }
            // falling rain (static with reduced motion)
            if (Application.isPlaying && !Fx.Reduced)
                root.schedule.Execute(() =>
                {
                    foreach (var d in rain)
                    {
                        float y = d.style.top.value.value + 18, x = d.style.left.value.value - 4;
                        if (y > 1000) { y = 90; x += 210; }
                        d.style.top = y; d.style.left = x < -40 ? x + 1960 : x;
                    }
                }).Every(33);
            Road(240, 680, 620, 420); Road(620, 420, 1200, 560);
        }

        // Placeholder port art: water, piers, cranes, containers, neon.
        void DrawPort()
        {
            Theme.Box(root, "Moon", Theme.Alpha(Theme.Hex("E8E1D0"), 0.85f), Shape.Circle).Center(1460, 150, 70, 70);
            Theme.Box(root, "MoonGlow", Theme.Alpha(Theme.Hex("E8E1D0"), 0.035f), Shape.Circle).Center(1460, 150, 170, 170);
            Skyline(7, 0.25f, 140, 420);
            var neon = new[] { "FF5AD8", "4AD6FF", "FFC24A", "7CFF6B" };
            for (int i = 0; i < 3; i++) Theme.Solid(root, "Haze", Theme.Alpha(Theme.Hex("2A1C4A"), 0.18f)).Place(0, 560 + i * 70, 1920, 70);

            Theme.Solid(root, "Water", Theme.Hex("0A1A2A")).Place(0, 820, 1920, 260);
            for (int i = 0; i < 18; i++) Theme.Solid(root, "Reflection", Theme.Alpha(Theme.Hex(neon[i % neon.Length]), 0.12f)).Place(90 + i * 104 + (i % 3) * 17, 836 + (i % 4) * 26, 34 - (i % 3) * 8, 3);
            for (int i = 0; i < 14; i++) Theme.Solid(root, "Wave", Theme.Alpha(Theme.Hex("1F4A6A"), 0.5f)).Place(40 + i * 140 + (i % 2) * 40, 860 + (i % 3) * 30, 70, 3);
            Theme.Solid(root, "Quay", Theme.Hex("1A1D28")).Place(0, 760, 1920, 60);
            Theme.Solid(root, "QuayEdge", Theme.Alpha(Theme.Hex("FFC24A"), 0.35f)).Place(0, 816, 1920, 4);
            foreach (var x in new[] { 260f, 1120f }) Theme.Solid(root, "Pier", Theme.Hex("1A1D28")).Place(x, 820, 120, 200);
            // cranes
            foreach (var x in new[] { 700f, 1700f })
            {
                Theme.Solid(root, "Crane", Theme.Hex("2A2F45")).Place(x, 180, 18, 580);
                Theme.Solid(root, "CraneArm", Theme.Hex("2A2F45")).Place(x - 160, 180, 320, 14);
                Theme.Solid(root, "Cable", Theme.Alpha(Theme.Ink, 0.15f)).Place(x + 120, 194, 2, 220);
                Theme.Box(root, "Lamp", Theme.Hex("FF5A4A"), Shape.Circle).Center(x + 9, 172, 12, 12);
            }
            // containers
            var cols = new[] { "6B2E2E", "2E4A6B", "6B5A2E", "2E6B4F", "4A2E6B" };
            for (int i = 0; i < 9; i++)
                Theme.Box(root, "Container", Theme.Alpha(Theme.Hex(cols[i % cols.Length]), 0.55f)).Place(620 + (i % 5) * 92 - (i / 5) * 40, 706 - (i / 5) * 52, 86, 48);
            // dirt roads between places
            Road(430, 640, 900, 380); Road(900, 380, 1360, 620); Road(430, 640, 1360, 620); Road(1360, 620, 1560, 300); Road(200, 300, 430, 640);
        }

        void Road(float x0, float y0, float x1, float y1)
        {
            var r = Theme.Solid(root, "Road", Theme.Alpha(Theme.Hex("3A3F55"), 0.6f));
            float dx = x1 - x0, dy = y1 - y0;
            r.Center((x0 + x1) / 2, (y0 + y1) / 2, Mathf.Sqrt(dx * dx + dy * dy), 6);
            r.Rotate(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
        }

        void Poi((string id, string name, string sub, float x, float y, string color) p)
        {
            bool open = IsOpen(p.id);
            bool teaser = (p.id == "ferro" || p.id == "neon") && !open; // shown by name even while locked
            var color = open ? Theme.Hex(p.color) : Theme.TextDim;
            var id = p.id;
            var node = Theme.El(root, "Place:" + p.id, "poi").Center(p.x, p.y, 300, 220).OnClick(() => Open(id));
            node.EnableInClassList("locked", !open);
            Theme.Box(node, "Glow", Theme.Alpha(color, open ? 0.18f : 0.06f), Shape.Circle).Classes("poi-glow").Center(150, 70, 190, 190);
            var pin = Theme.Box(node, "Pin", Theme.Mix(Theme.Bg, color, 0.25f), Shape.Circle).Classes("poi-pin").Center(150, 64, 128, 128);
            pin.style.overflow = Overflow.Hidden;
            if (open || teaser) Scene(pin, p.id, open);
            else Theme.Title(pin, "?", 44, color, TextAnchor.MiddleCenter).Fill();
            Theme.Box(pin, "Ring", color, Shape.Ring).Fill();
            var plate = Theme.Plate(node, "Plate", "poi-plate").Center(150, 162, 300, 78);
            Theme.Label(plate, open || teaser ? p.name.ToUpperInvariant() : "???", 21, open ? Theme.Ink : Theme.TextDim, TextAnchor.UpperCenter, FontStyle.Bold).Place(6, 8, 288, 30).OneLine();
            Theme.Label(plate, open ? p.sub : teaser ? "TRANCADO · em breve" : "Trancado", 17, open ? color : Theme.TextDim, TextAnchor.UpperCenter).Place(6, 42, 288, 26).OneLine();
            if (p.id == "bar" && (K.BettorWaiting(C) || !K.InJob && K.offer.Length == 0))
            {
                var alert = Theme.Chip(node, "!", Theme.Warn, 22).parent.Center(205, 22, 36, 36);
                alert.AddToClassList("alert");
                Fx.Pulse(alert, 0.45f, 1, 1.2f);
            }
            Theme.Hover(node, open ? $"{p.name}: {p.sub}" : teaser ? $"{p.name}: ainda trancado." : "Lugar ainda desconhecido.");
        }

        // Credits and scrap with Kenney icons, right-aligned before the header buttons.
        public static void Wallet(VisualElement root, Career k, float x)
        {
            var row = Theme.Row(root, "Wallet", 10).Place(x, 30, 620, 54);
            row.style.justifyContent = Justify.FlexEnd;
            row.style.alignItems = Align.Center;
            Theme.Icon(row, "coin", 30, Theme.Warn);
            Theme.Label(row, $"{k.credits}", 28, Theme.Warn, style: FontStyle.Bold);
            Theme.Label(row, "créditos", Theme.Small, Theme.TextDim).style.marginRight = 16;
            Theme.Icon(row, "wrench", 30, Theme.Hex("C8A27A"));
            Theme.Label(row, $"{k.scrap}", 28, Theme.Hex("C8A27A"), style: FontStyle.Bold);
            Theme.Label(row, "sucata", Theme.Small, Theme.TextDim);
        }

        // Little illustration inside a place's round card (128 px). Shapes only, original placeholder art.
        static void Scene(VisualElement pin, string id, bool open)
        {
            VisualElement S(float x, float y, float w, float h, Color c, Shape sp = Shape.Rounded, float rot = 0)
            {
                var e = Theme.Box(pin, "s", open ? c : Theme.Mix(c, Theme.Bg, 0.45f), sp).Center(x, y, w, h).Rotate(rot);
                if (sp == Shape.Rounded) { var st = e.style; st.borderTopLeftRadius = st.borderTopRightRadius = st.borderBottomLeftRadius = st.borderBottomRightRadius = 3; }
                return e;
            }
            Color wall = Theme.Hex("232838"), dark = Theme.Hex("12151F");
            switch (id)
            {
                case "bar": // building, door, glowing sign, lit windows
                    S(64, 84, 96, 70, wall); S(86, 104, 20, 32, dark);
                    for (int i = 0; i < 3; i++) S(30 + i * 20, 96, 12, 10, Theme.Alpha(Theme.Hex("FFC24A"), 0.7f));
                    S(64, 38, 70, 22, Theme.Hex("2A1E10"));
                    Theme.Label(pin, "BAR", 18, Theme.Hex("FFC24A"), TextAnchor.MiddleCenter, FontStyle.Bold).Center(64, 38, 70, 22);
                    S(64, 38, 84, 34, Theme.Alpha(Theme.Hex("FFC24A"), 0.15f));
                    break;
                case "oficina": // garage with a roll-up door and a wrench
                    S(64, 80, 100, 74, wall); S(64, 44, 108, 14, Theme.Hex("2E3448"));
                    for (int i = 0; i < 5; i++) S(64, 66 + i * 10, 64, 5, Theme.Hex("3A4258"));
                    S(64, 92, 64, 10, Theme.Alpha(Theme.Hex("4AD6FF"), 0.5f)); // light under the door
                    S(98, 34, 8, 34, Theme.Hex("4AD6FF"), rot: -40); S(108, 24, 16, 16, Theme.Hex("4AD6FF"), Shape.Ring);
                    break;
                case "fosso": // the pit: a ring of light, ropes, crowd heads
                    S(64, 80, 104, 44, Theme.Hex("2A0E10"), Shape.Circle);
                    S(64, 80, 104, 44, Theme.Hex("FF5A4A"), Shape.Ring);
                    S(64, 66, 96, 3, Theme.Alpha(Theme.Ink, 0.4f)); S(64, 74, 100, 3, Theme.Alpha(Theme.Ink, 0.3f));
                    for (int i = 0; i < 7; i++) S(22 + i * 14, 40 + (i % 2) * 6, 10, 10, Theme.Hex("1A1D28"), Shape.Circle);
                    S(64, 30, 20, 20, Theme.Alpha(Theme.Hex("FF5A4A"), 0.6f), Shape.Circle);
                    break;
                case "ferro": // scrap pile behind a fence, padlock while closed
                    S(44, 92, 46, 22, Theme.Hex("4A4F60"), rot: 18); S(78, 88, 52, 26, Theme.Hex("5A4A3A"), rot: -12);
                    S(62, 70, 36, 24, Theme.Hex("3A4A5A"), rot: 8); S(56, 54, 26, 26, Theme.Hex("6A6F80"), Shape.Ring);
                    for (int i = 0; i < 6; i++) S(18 + i * 18, 100, 3, 50, Theme.Alpha(Theme.Ink, 0.25f));
                    S(18 + 50, 82, 100, 3, Theme.Alpha(Theme.Ink, 0.25f));
                    if (!open) { S(96, 40, 22, 18, Theme.Hex("8A8FA3")); S(96, 28, 16, 16, Theme.Hex("8A8FA3"), Shape.Ring); }
                    break;
                case "neon": case "porto": // train car with lit windows on a rail
                    S(64, 98, 120, 4, Theme.Hex("8A8FA3"));
                    S(64, 74, 104, 40, Theme.Hex(id == "neon" ? "3A2350" : "3A3020"));
                    for (int i = 0; i < 4; i++) S(28 + i * 24, 70, 16, 14, Theme.Alpha(Theme.Hex(id == "neon" ? "B45AFF" : "FFC24A"), 0.8f));
                    S(64, 46, 70, 4, Theme.Alpha(Theme.Ink, 0.4f));
                    if (!open) { S(100, 36, 22, 18, Theme.Hex("8A8FA3")); S(100, 24, 16, 16, Theme.Hex("8A8FA3"), Shape.Ring); }
                    break;
                case "circuito": // rooftop ring under the rain, neon billboard
                    S(64, 92, 110, 30, Theme.Hex("10202A"), Shape.Circle);
                    S(64, 92, 110, 30, Theme.Hex("4AD6FF"), Shape.Ring);
                    S(64, 46, 80, 26, Theme.Hex("1A1030"));
                    Theme.Label(pin, "CR", 18, Theme.Hex("FF5AD8"), TextAnchor.MiddleCenter, FontStyle.Bold).Center(64, 46, 80, 26);
                    for (int i = 0; i < 6; i++) S(20 + i * 18, 70, 2, 16, Theme.Alpha(Theme.Hex("9EB8FF"), 0.5f), rot: -12);
                    break;
                case "glitch": // dark room, three glowing screens, cables
                    S(64, 70, 110, 80, Theme.Hex("12101C"));
                    S(40, 60, 34, 24, Theme.Alpha(Theme.Hex("B45AFF"), 0.8f)); S(84, 60, 34, 24, Theme.Alpha(Theme.Hex("4AD6FF"), 0.8f));
                    S(62, 88, 40, 20, Theme.Alpha(Theme.Hex("7CFF6B"), 0.7f));
                    S(40, 104, 60, 3, Theme.Alpha(Theme.Ink, 0.3f), rot: 10); S(86, 106, 50, 3, Theme.Alpha(Theme.Ink, 0.3f), rot: -14);
                    break;
            }
        }

        // Clicking a place.
        public void Open(string id)
        {
            if (!IsOpen(id)) { Talk(id == "neon" ? new List<LineDef> { new LineDef { who = "Placa", text = "TREM PARA O DISTRITO NEON — SÓ COM CONVITE." } } : Lines(id == "ferro" ? "ferro_velho" : "locked"), null); return; }
            if (id == "neon" || id == "porto") { K.region = id; app.SaveCareer(); app.ShowMap(); return; }
            if (id == "circuito") { app.ShowCareer("circuit"); return; }
            if (id == "glitch") { Glitch(); return; }
            if (id == "ferro") app.ShowCareer("ferro");
            else if (id == "oficina") app.ShowCareer("workshop");
            else if (id == "fosso") app.ShowCareer("pit");
            else Bar();
        }

        List<LineDef> Lines(string dialogueId) => C.Dialogue(dialogueId).lines.ToList();

        // The bar: who's there to talk to.
        void Bar()
        {
            var choices = new List<(string, Action)> { ("Falar com a Celeste", Celeste) };
            if (K.BettorWaiting(C)) choices.Insert(0, ("Falar com o apostador velho", Bettor));
            choices.Add(("Sair", CloseTalk));
            Talk(new List<LineDef> { new LineDef { who = "Bar Fim da Linha", text = "Fumaça, óleo velho e uma tela passando a luta de ontem." } }, choices);
        }

        public void Celeste()
        {
            var lines = new List<LineDef>();
            foreach (var once in new[] { "celeste_first", "celeste_unlock", "ferro_open", "neon_open" })
                if (!K.Seen(once) && (once == "celeste_first" || K.IsOpen(once == "ferro_open" ? "ferro" : once == "neon_open" ? "neon" : "fosso"))) { lines.AddRange(Lines(once)); K.MarkSeen(once); app.SaveCareer(); }
            if (K.InJob)
            {
                lines.AddRange(Lines("celeste_busy"));
                Talk(lines, new List<(string, Action)> { ("Ver o trabalho", app.ShowJob), ("Rumores sobre o próximo", () => Rumors(K.Stage(C).enemy)), ("Sair", CloseTalk) });
                return;
            }
            lines.AddRange(Lines("celeste_offer"));
            var job = C.Job("celeste_fossos");
            Talk(lines, new List<(string, Action)>
            {
                ("Aceitar o trabalho", () => { K.StartJob(C, job.id); K.lastResult = null; app.SaveCareer(); app.ShowJob(); }),
                ("Rumores sobre a primeira luta", () => Rumors(job.stages[0].enemy)),
                ("Agora não", CloseTalk),
            });
        }

        // Dossier rumors told as dialogue.
        void Rumors(string enemyId)
        {
            var r = C.Robot(enemyId);
            var lines = new List<LineDef> { new LineDef { who = "Celeste", text = $"O {r.name}? Deixa eu ver o que o pessoal anda falando." } };
            var rumors = r.dossier != null && r.dossier.Exists ? r.dossier.rumors : new string[0];
            if (rumors.Length == 0) lines.Add(new LineDef { who = "Celeste", text = "Ninguém perde tempo falando desse aí. Luta de aquecimento." });
            foreach (var t in rumors) lines.Add(new LineDef { who = "Celeste", text = t });
            Talk(lines, new List<(string, Action)> { ("Voltar", Celeste), ("Sair", CloseTalk) });
        }

        public void Bettor()
        {
            Talk(Lines("bettor_clue"), new List<(string, Action)>
            {
                ("Anotar nas pistas", () => { K.LearnClue(C, Career.ClueRigged); app.SaveCareer(); app.ShowMap(); }),
            });
        }

        // Glitch: introduction once, then waits for two Circuito wins, then finds the sale record (second clue).
        public void Glitch()
        {
            var lines = new List<LineDef>();
            if (!K.Seen("glitch_first")) { lines.AddRange(Lines("glitch_first")); K.MarkSeen("glitch_first"); app.SaveCareer(); }
            if (K.GlitchReady)
            {
                lines.AddRange(Lines("glitch_clue"));
                Talk(lines, new List<(string, Action)> { ("Anotar nas pistas", () => { K.LearnClue(C, Career.ClueSale); app.SaveCareer(); app.ShowMap(); }) });
                return;
            }
            if (lines.Count == 0) lines.AddRange(Lines(K.clues.Contains(Career.ClueSale) ? "glitch_done" : "glitch_wait"));
            Talk(lines, new List<(string, Action)> { ("Ir ao Circuito", () => app.ShowCareer("circuit")), ("Sair", CloseTalk) });
        }

        // ---- Dialogue box: one line at a time, then the choices ----
        List<LineDef> lines;
        List<(string label, Action action)> choices;
        int index;

        void Talk(List<LineDef> l, List<(string, Action)> c)
        {
            lines = l; choices = c ?? new List<(string, Action)> { ("Ok", (Action)CloseTalk) }; index = 0;
            DrawTalk();
        }

        public void Next() { if (index < lines.Count - 1) { index++; DrawTalk(); } }

        public void CloseTalk() { talkLayer.Clear(); }

        public string CurrentLine => lines == null || talkLayer.childCount == 0 ? null : lines[index].text;
        public string[] CurrentChoices => lines == null || index < lines.Count - 1 ? new string[0] : choices.Select(c => c.label).ToArray();
        public void Choose(string label) => choices.First(c => c.label == label).action();

        void DrawTalk()
        {
            talkLayer.Clear();
            var shade = Theme.El(talkLayer, "Shade", "shade").Fill();
            shade.pickingMode = PickingMode.Position;
            var box = Theme.Plate(talkLayer, "Dialogue", "dialogue").Place(200, 620, 1520, 420);
            box.pickingMode = PickingMode.Position;
            var line = lines[index];
            Portraits.Create(box, line.who, 170).Place(36, 36, 170, 170);
            Theme.Title(box, line.who.ToUpperInvariant(), 24, Portraits.ColorOf(line.who)).At(240, 34);
            Theme.Label(box, $"{index + 1}/{lines.Count}", Theme.Small, Theme.TextDim, TextAnchor.UpperRight).Place(1300, 36, 180, 30);
            var text = Theme.Scroll(box, "Line").Place(240, 76, 1240, 190);
            Typewriter(Theme.Label(text, line.text, 28, Theme.Ink).Classes("prose"), line.text);
            if (index == 0) Fx.Enter(box, 0, 30);

            if (index < lines.Count - 1)
            {
                var next = Theme.Primary(box, "CONTINUAR  ▸", Next, 24);
                next.name = "Proximo";
                next.Place(1200, 314, 280, 76);
                Theme.Label(box, "ou clique fora da caixa", 15, Theme.TextDim, TextAnchor.MiddleRight).Place(760, 314, 420, 76);
                shade.RegisterCallback<ClickEvent>(_ => Next());
                return;
            }
            var row = Theme.Row(box, "Choices", 16).Place(240, 300, 1240, 96);
            for (int i = 0; i < choices.Count; i++)
            {
                var c = choices[i];
                var b = i == 0 ? Theme.Primary(row, c.label, c.action, 22) : Theme.Button(row, c.label, c.action, null, 22);
                b.name = "Escolha:" + c.label;
                b.style.height = 76; b.style.flexGrow = 1; b.style.flexBasis = 0;
                Fx.Enter(b, i + 2);
            }
        }

        // Reveals the line letter by letter; a click on the text shows it all at once.
        static void Typewriter(Label label, string full)
        {
            if (Fx.Reduced || !Application.isPlaying || full.Contains("<")) return;
            int shown = 0;
            label.text = "";
            label.schedule.Execute(() => { shown = Mathf.Min(full.Length, shown + 2); label.text = full.Substring(0, shown); }).Every(16).Until(() => shown >= full.Length);
            label.pickingMode = PickingMode.Position;
            label.RegisterCallback<ClickEvent>(e => { if (shown < full.Length) { shown = full.Length; label.text = full; e.StopPropagation(); } });
        }

        // ---- Pistas journal ----
        void Journal()
        {
            talkLayer.Clear();
            var shade = Theme.El(talkLayer, "Shade", "shade").Fill();
            shade.pickingMode = PickingMode.Position;
            shade.RegisterCallback<ClickEvent>(_ => CloseTalk());
            var page = Theme.Plate(talkLayer, "Journal", "paper").Center(960, 540, 1100, 780);
            page.pickingMode = PickingMode.Position;
            var header = Theme.El(page, "Header", "paper-header").Place(0, 0, 1100, 72);
            Theme.Icon(header, "book", 40, Theme.Hex("E8D9B8")).At(26, 16);
            Theme.Title(header, "PISTAS", 32, Theme.Hex("E8D9B8"), TextAnchor.MiddleLeft).Place(80, 0, 600, 72);
            var sb = new System.Text.StringBuilder();
            if (K.clues.Length == 0) sb.Append("<i>Nada anotado ainda. Tem gente no porto que sabe mais do que fala.</i>");
            foreach (var id in K.clues)
            {
                var clue = C.Clue(id);
                sb.AppendLine($"<b>{clue.title}</b>");
                sb.AppendLine(clue.text);
                sb.AppendLine($"<color=#B9A98A><i>Fonte: {clue.source}</i></color>\n");
            }
            var body = Theme.Scroll(page, "Body").Place(36, 96, 1028, 560);
            Theme.Label(body, sb.ToString().TrimEnd(), 24, Theme.Hex("E8E1D0")).Classes("prose");
            var close = Theme.Button(page, "FECHAR", CloseTalk, null, 24);
            close.name = "Fechar";
            close.Place(36, 686, 220, 66);
            Fx.Enter(page, 0, 30);
        }
    }
}
