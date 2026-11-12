using System;
using System.Collections.Generic;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattler.UI
{
    // The port: a 2D map with points of interest (no free movement, no clock). Clicking a place opens it;
    // the bar opens a linear dialogue with Celeste (and, after the Fosso title, an old bettor).
    public class MapScreen
    {
        readonly GameUI app;
        readonly RectTransform root;
        RectTransform talkLayer;

        public MapScreen(GameUI app, RectTransform root) { this.app = app; this.root = root; }

        Content C => app.content;
        Career K => app.career;

        // id, name, subtitle, position, color. "ferro" is always locked (teaser).
        public static readonly (string id, string name, string sub, float x, float y, string color)[] Places =
        {
            ("bar", "Bar Fim da Linha", "Celeste arruma lutas", 430, 640, "FFC24A"),
            ("oficina", "Oficina do Cais", "Reparos e garagem", 900, 380, "4AD6FF"),
            ("fosso", "O Fosso", "Ranking até o campeão", 1360, 620, "FF5A4A"),
            ("ferro", "Ferro-velho", "Fechado", 1560, 300, "8A8FA3"),
        };

        public bool IsOpen(string id) => id != "ferro" && K.IsOpen(id);

        public void Build()
        {
            DrawPort();
            Theme.Label(root, "O PORTO", 44, Theme.Ink, style: FontStyle.Bold).rectTransform.Place(60, 30, 500, 60);
            Theme.Label(root, "Escolha aonde ir", Theme.Small, Theme.TextDim).rectTransform.Place(62, 80, 500, 30);
            Theme.Label(root, $"{K.credits} créditos   ·   {K.scrap} sucata", 26, Theme.Warn, TextAnchor.MiddleRight, FontStyle.Bold).rectTransform.Place(820, 34, 700, 50);
            var clues = Theme.Button(root, $"PISTAS ({K.clues.Length})", Journal, K.clues.Length > 0 ? Theme.Mix(Theme.PanelHi, Theme.Warn, 0.35f) : Theme.Panel, 22);
            clues.name = "Pistas";
            ((RectTransform)clues.transform).Place(1540, 34, 170, 50);
            ((RectTransform)Theme.Button(root, "←  MENU", () => app.ShowTitle(), Theme.Panel, 22).transform).Place(1720, 34, 160, 50);

            foreach (var p in Places) Poi(p);

            // Where the story stands, in one line.
            string hint = K.InJob ? $"Trabalho em andamento: luta {Math.Min(K.stage + 1, C.Job(K.job).stages.Length)} de {C.Job(K.job).stages.Length}. Fale com a Celeste no bar."
                : !K.IsOpen("fosso") ? "Fale com a Celeste no Bar Fim da Linha."
                : K.BettorWaiting(C) ? "Um apostador velho quer falar com você no bar."
                : K.Champion(C) ? "Campeão dos Fossos. A Celeste tem mais trabalho no bar."
                : "Suba no ranking do Fosso ou pegue outro trabalho com a Celeste.";
            if (!string.IsNullOrEmpty(K.lastResult)) hint = K.lastResult + "\n" + hint;
            var bar = Theme.Box(root, "Hint", Theme.Alpha(Theme.Panel, 0.92f));
            bar.rectTransform.Place(60, 960, 1300, 90);
            Theme.Label(bar.transform, hint, 22, Theme.Ink, TextAnchor.MiddleLeft).rectTransform.Place(24, 0, 1260, 90);

            talkLayer = Theme.Node(root, "Talk").Fill();
        }

        // Placeholder port art: water, piers, cranes, containers, neon.
        void DrawPort()
        {
            Theme.Solid(root, "Water", Theme.Hex("0A1A2A")).rectTransform.Place(0, 820, 1920, 260);
            for (int i = 0; i < 14; i++) Theme.Solid(root, "Wave", Theme.Alpha(Theme.Hex("1F4A6A"), 0.5f)).rectTransform.Place(40 + i * 140 + (i % 2) * 40, 860 + (i % 3) * 30, 70, 3);
            Theme.Solid(root, "Quay", Theme.Hex("1A1D28")).rectTransform.Place(0, 760, 1920, 60);
            Theme.Solid(root, "QuayEdge", Theme.Alpha(Theme.Hex("FFC24A"), 0.35f)).rectTransform.Place(0, 816, 1920, 4);
            foreach (var x in new[] { 260f, 1120f }) Theme.Solid(root, "Pier", Theme.Hex("1A1D28")).rectTransform.Place(x, 820, 120, 200);
            // cranes
            foreach (var x in new[] { 700f, 1700f })
            {
                Theme.Solid(root, "Crane", Theme.Hex("2A2F45")).rectTransform.Place(x, 180, 18, 580);
                Theme.Solid(root, "CraneArm", Theme.Hex("2A2F45")).rectTransform.Place(x - 160, 180, 320, 14);
                Theme.Solid(root, "Cable", Theme.Alpha(Theme.Ink, 0.15f)).rectTransform.Place(x + 120, 194, 2, 220);
                Theme.Box(root, "Lamp", Theme.Hex("FF5A4A"), Theme.Circle).rectTransform.Center(x + 9, 172, 12, 12);
            }
            // containers
            var cols = new[] { "6B2E2E", "2E4A6B", "6B5A2E", "2E6B4F", "4A2E6B" };
            for (int i = 0; i < 9; i++)
                Theme.Box(root, "Container", Theme.Alpha(Theme.Hex(cols[i % cols.Length]), 0.55f)).rectTransform.Place(620 + (i % 5) * 92 - (i / 5) * 40, 706 - (i / 5) * 52, 86, 48);
            // dirt roads between places
            Road(430, 640, 900, 380); Road(900, 380, 1360, 620); Road(430, 640, 1360, 620); Road(1360, 620, 1560, 300);
        }

        void Road(float x0, float y0, float x1, float y1)
        {
            var r = Theme.Solid(root, "Road", Theme.Alpha(Theme.Hex("3A3F55"), 0.6f)).rectTransform;
            float dx = x1 - x0, dy = y1 - y0;
            r.Center((x0 + x1) / 2, (y0 + y1) / 2, Mathf.Sqrt(dx * dx + dy * dy), 6);
            r.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
        }

        void Poi((string id, string name, string sub, float x, float y, string color) p)
        {
            bool open = IsOpen(p.id);
            bool teaser = p.id == "ferro";
            var color = open ? Theme.Hex(p.color) : Theme.TextDim;
            var node = Theme.Node(root, "Place:" + p.id);
            node.Center(p.x, p.y, 300, 220);
            Theme.Box(node, "Glow", Theme.Alpha(color, open ? 0.18f : 0.06f), Theme.Circle).rectTransform.Center(150, 70, 190, 190);
            var pin = Theme.Box(node, "Pin", Theme.Mix(Theme.Bg, color, 0.25f), Theme.Circle, raycast: true);
            pin.rectTransform.Center(150, 70, 110, 110);
            Theme.Box(pin.transform, "Ring", color, Theme.Ring).rectTransform.Fill();
            Theme.Label(pin.transform, open ? Glyph(p.id) : "?", 44, color, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Fill();
            var plate = Theme.Box(node, "Plate", Theme.Alpha(Theme.Panel, 0.94f));
            plate.rectTransform.Center(150, 160, 300, 74);
            Theme.Label(plate.transform, open || teaser ? p.name.ToUpperInvariant() : "???", 22, open ? Theme.Ink : Theme.TextDim, TextAnchor.UpperCenter, FontStyle.Bold).rectTransform.Place(0, 8, 300, 30);
            Theme.Label(plate.transform, open ? p.sub : teaser ? "TRANCADO · em breve" : "Trancado", 17, open ? color : Theme.TextDim, TextAnchor.UpperCenter).rectTransform.Place(0, 40, 300, 26);
            if (p.id == "bar" && (K.BettorWaiting(C) || !K.InJob && K.offer.Length == 0))
                Theme.Chip(node, "!", Theme.Warn, 22).transform.parent.GetComponent<RectTransform>().Center(205, 22, 34, 34);
            var id = p.id;
            pin.gameObject.AddComponent<Clicker>().onClick = () => Open(id);
        }

        static string Glyph(string id) => id switch { "bar" => "B", "oficina" => "O", "fosso" => "F", _ => "?" };

        // Clicking a place.
        public void Open(string id)
        {
            if (id == "ferro") { Talk(Lines("ferro_velho"), null); return; }
            if (!IsOpen(id)) { Talk(Lines("locked"), null); return; }
            if (id == "oficina") app.ShowCareer("workshop");
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
            foreach (var once in new[] { "celeste_first", "celeste_unlock" })
                if (!K.Seen(once) && (once == "celeste_first" || K.IsOpen("fosso"))) { lines.AddRange(Lines(once)); K.MarkSeen(once); app.SaveCareer(); }
            if (K.InJob)
            {
                lines.AddRange(Lines("celeste_busy"));
                Talk(lines, new List<(string, Action)> { ("Ver o trabalho", app.ShowJob), ("Rumores sobre o próximo", () => Rumors(K.Stage(C).enemy)), ("Sair", CloseTalk) });
                return;
            }
            lines.AddRange(Lines("celeste_offer"));
            var job = C.jobs[0];
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

        public void CloseTalk() { Theme.ClearChildren(talkLayer); }

        public string CurrentLine => lines == null || talkLayer.childCount == 0 ? null : lines[index].text;
        public string[] CurrentChoices => lines == null || index < lines.Count - 1 ? new string[0] : choices.Select(c => c.label).ToArray();
        public void Choose(string label) => choices.First(c => c.label == label).action();

        void DrawTalk()
        {
            Theme.ClearChildren(talkLayer);
            var shade = Theme.Solid(talkLayer, "Shade", Theme.Alpha(Color.black, 0.45f));
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            var box = Theme.Box(talkLayer, "Dialogue", Theme.Alpha(Theme.Panel, 0.98f), raycast: true);
            box.rectTransform.Place(200, 640, 1520, 400);
            Theme.Box(box.transform, "Edge", Theme.Alpha(Theme.Warn, 0.6f), Theme.Frame).rectTransform.Fill(-2);
            var line = lines[index];
            // Portrait placeholder: initial in a ring.
            var face = Theme.Box(box.transform, "Portrait", Theme.Mix(Theme.Bg, Theme.Warn, 0.2f), Theme.Circle);
            face.rectTransform.Place(40, 40, 160, 160);
            Theme.Box(face.transform, "Ring", Theme.Warn, Theme.Ring).rectTransform.Fill();
            Theme.Label(face.transform, line.who.Substring(0, 1), 72, Theme.Warn, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Fill();
            Theme.Label(box.transform, line.who.ToUpperInvariant(), 24, Theme.Warn, style: FontStyle.Bold).rectTransform.Place(240, 36, 1000, 34);
            Theme.Label(box.transform, line.text, 28, Theme.Ink).rectTransform.Place(240, 80, 1240, 150);
            Theme.Label(box.transform, $"{index + 1}/{lines.Count}", 18, Theme.TextDim, TextAnchor.UpperRight).rectTransform.Place(1300, 36, 180, 30);

            if (index < lines.Count - 1)
            {
                var next = Theme.Button(box.transform, "CONTINUAR  ▸", Next, Theme.PanelHi, 24);
                next.name = "Proximo";
                ((RectTransform)next.transform).Place(1200, 300, 280, 66);
                return;
            }
            for (int i = 0; i < choices.Count; i++)
            {
                var b = Theme.Button(box.transform, choices[i].label, choices[i].action, i == 0 ? Theme.Mix(Theme.PanelHi, Theme.Exposed, 0.5f) : Theme.PanelHi, 22);
                b.name = "Escolha:" + choices[i].label;
                ((RectTransform)b.transform).Place(240 + i * 420, 270, 400, 70);
            }
        }

        // ---- Pistas journal ----
        void Journal()
        {
            Theme.ClearChildren(talkLayer);
            var shade = Theme.Solid(talkLayer, "Shade", Theme.Alpha(Color.black, 0.6f));
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            var page = Theme.Box(talkLayer, "Journal", Theme.Paper, raycast: true);
            page.rectTransform.Center(960, 540, 1100, 760);
            Theme.Solid(page.transform, "Header", Theme.Hex("2B251D")).rectTransform.Place(0, 0, 1100, 70);
            Theme.Label(page.transform, "PISTAS", 32, Theme.Hex("E8D9B8"), TextAnchor.MiddleLeft, FontStyle.Bold).rectTransform.Place(30, 0, 600, 70);
            var sb = new System.Text.StringBuilder();
            if (K.clues.Length == 0) sb.Append("<i>Nada anotado ainda. Tem gente no porto que sabe mais do que fala.</i>");
            foreach (var id in K.clues)
            {
                var clue = C.Clue(id);
                sb.AppendLine($"<b>{clue.title}</b>");
                sb.AppendLine(clue.text);
                sb.AppendLine($"<color=#B9A98A><i>Fonte: {clue.source}</i></color>\n");
            }
            Theme.Label(page.transform, sb.ToString(), 24, Theme.Hex("E8E1D0")).rectTransform.Place(36, 96, 1028, 540);
            var close = Theme.Button(page.transform, "FECHAR", CloseTalk, Theme.Hex("3A3226"), 24);
            close.name = "Fechar";
            ((RectTransform)close.transform).Place(36, 670, 220, 64);
        }
    }
}
