using System;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattler.UI
{
    // Opponent selection: opponents grouped by category, the selected one's dossier, a preview of the player's robot.
    public class OpponentScreen
    {
        readonly GameUI app;
        readonly RectTransform root;
        public static string selected = "enemy_sucata";

        public OpponentScreen(GameUI app, RectTransform root) { this.app = app; this.root = root; }

        Content C => app.content;

        public static RobotDef[] Opponents(Content c) =>
            c.robots.Where(r => !string.IsNullOrEmpty(r.tier)).OrderBy(r => Array.IndexOf(RobotDef.Tiers, r.tier)).ToArray();

        void Rebuild() { Theme.ClearChildren(root); Tooltip.Hide(); Build(); }

        public void Build()
        {
            var all = Opponents(C);
            if (all.All(o => o.id != selected)) selected = all[0].id;
            var op = C.Robot(selected);

            Theme.Label(root, "ADVERSÁRIO", 44, Theme.Ink, style: FontStyle.Bold).rectTransform.Place(60, 30, 500, 60);
            Theme.Label(root, "Escolha contra quem lutar", Theme.Small, Theme.TextDim).rectTransform.Place(62, 80, 500, 30);

            // Grid: one column per category
            for (int t = 0; t < RobotDef.Tiers.Length; t++)
            {
                var tier = RobotDef.Tiers[t];
                float x = 40 + t * 262;
                var color = GarageScreen.TierColor(tier);
                Theme.Label(root, tier.ToUpperInvariant(), 26, color, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Place(x, 116, 250, 36);
                Theme.Solid(root, "TierLine", Theme.Alpha(color, 0.7f)).rectTransform.Place(x + 20, 154, 210, 3);
                float y = 168;
                foreach (var o in all.Where(r => r.tier == tier))
                {
                    var id = o.id;
                    bool sel = id == selected;
                    var card = Theme.Box(root, "Opponent:" + id, sel ? Theme.Mix(Theme.Panel, color, 0.22f) : Theme.Panel, raycast: true);
                    card.rectTransform.Place(x, y, 250, 330);
                    if (sel) Theme.Box(card.transform, "Selected", color, Theme.Frame).rectTransform.Fill(-3);
                    card.gameObject.AddComponent<Clicker>().onClick = () => { selected = id; Rebuild(); };
                    var bot = RobotView.Create(card.transform, false, false);
                    bot.Rect.Center(125, 135, RobotView.W, RobotView.H);
                    bot.transform.localScale = new Vector3(0.52f, 0.52f, 1);
                    bot.Build(C, o);
                    foreach (var g in bot.GetComponentsInChildren<Graphic>()) g.raycastTarget = false; // clicks go to the card
                    var name = Theme.Label(card.transform, o.name, 21, Theme.Ink, TextAnchor.UpperCenter, FontStyle.Bold);
                    name.rectTransform.Place(10, 262, 230, 56);
                    y += 344;
                }
            }

            // Dossier of the selected opponent
            var dossier = Theme.Node(root, "Dossier").Place(1120, 110, 760, 760);
            Dossier(C, dossier, op);

            // Player's robot preview
            var build = app.Garage.Current;
            var stats = BuildStats.Of(C, build);
            var mine = Theme.Box(root, "Mine", Theme.Panel);
            mine.rectTransform.Place(40, 880, 700, 180);
            var mbot = RobotView.Create(mine.transform, false, false);
            mbot.Rect.Center(80, 90, RobotView.W, RobotView.H);
            mbot.transform.localScale = new Vector3(0.36f, 0.36f, 1);
            mbot.Build(C, build);
            Theme.Label(mine.transform, "SEU ROBÔ", 16, Theme.TextDim, style: FontStyle.Bold).rectTransform.Place(160, 14, 300, 24);
            Theme.Label(mine.transform, $"{build.name} · {C.Frame(build.frame).name}", 26, Theme.Ink, style: FontStyle.Bold).rectTransform.Place(160, 40, 520, 34);
            Theme.Label(mine.transform, $"Chassi {stats.chassis} · Integridade {stats.totalIntegrity} · Energia {stats.energy} · Mão {stats.hand}\n" +
                $"{stats.attacks} ataques ({stats.choose} escolhem a parte) · bônus {(stats.makers.Length == 0 ? "nenhum" : string.Join(" + ", stats.makers))}",
                18, Theme.Ink).rectTransform.Place(160, 80, 530, 90);

            var back = Theme.Button(root, "←  VOLTAR À GARAGEM", app.ShowGarage, Theme.Panel, 24);
            ((RectTransform)back.transform).Place(770, 980, 360, 74);
            var fight = Theme.Button(root, "LUTAR", () => app.StartBattle(build, selected), Theme.Mix(Theme.PanelHi, Theme.Exposed, 0.6f), 34);
            fight.name = "Lutar";
            ((RectTransform)fight.transform).Place(1520, 970, 360, 84);
        }

        // The in-world file: header, category stamp, how it fights, rumors, works well / avoid.
        // terrainId: the arena of this fight (null = the opponent's home arena).
        public static void Dossier(Content c, RectTransform parent, RobotDef op, string terrainId = null)
        {
            var terrain = c.Terrain(terrainId ?? op.terrain);
            var paper = Theme.Box(parent, "File", Theme.Paper);
            paper.rectTransform.Fill();
            var o = paper.gameObject.AddComponent<Outline>(); o.effectColor = Theme.Alpha(Theme.Hex("6B5A3E"), 0.8f); o.effectDistance = new Vector2(2, -2);
            Theme.Solid(paper.transform, "Header", Theme.Hex("2B251D")).rectTransform.Place(0, 0, 760, 64);
            var title = Theme.Label(paper.transform, "DOSSIÊ // " + op.name.ToUpperInvariant(), 26, Theme.Hex("E8D9B8"), TextAnchor.MiddleLeft, FontStyle.Bold);
            title.rectTransform.Place(24, 0, 560, 64);
            title.resizeTextForBestFit = true; title.verticalOverflow = VerticalWrapMode.Truncate; title.resizeTextMinSize = 16; title.resizeTextMaxSize = 26;
            var stamp = Theme.Chip(paper.transform, op.tier.ToUpperInvariant(), GarageScreen.TierColor(op.tier), 22);
            var srt = (RectTransform)stamp.transform.parent;
            srt.Place(590, 12, 150, 42); srt.localRotation = Quaternion.Euler(0, 0, -6);

            var d = op.dossier;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<color=#B9A98A>FABRICANTE PRINCIPAL</color>   <color=#{Theme.HexOf(Theme.Maker(c.MainMaker(op)))}><b>{c.MainMaker(op)}</b></color>");
            sb.AppendLine($"<color=#B9A98A>COMO LUTA</color>   {c.Describe(op)}.");
            if (terrain != null) sb.AppendLine($"<color=#B9A98A>ARENA</color>   <b>{terrain.name}</b> ({terrain.place}): {terrain.text}");
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
            bool face = Portraits.Has(op.name);
            body.rectTransform.Place(28, 84, face ? 560 : 704, 660);
            body.resizeTextForBestFit = true; body.resizeTextMinSize = 15; body.resizeTextMaxSize = 21; body.verticalOverflow = VerticalWrapMode.Truncate;
            if (face) Portraits.Create(paper.transform, op.name, 140).Place(600, 84, 140, 140);
            body.lineSpacing = 1.05f;
        }
    }
}
