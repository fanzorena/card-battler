using System;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace CardBattler.UI
{
    // Opponent selection: opponents grouped by category, the selected one's dossier, a preview of the player's robot.
    public class OpponentScreen
    {
        readonly GameUI app;
        readonly VisualElement root;
        public static string selected = "enemy_sucata";

        public OpponentScreen(GameUI app, VisualElement root) { this.app = app; this.root = root; }

        Content C => app.content;

        public static RobotDef[] Opponents(Content c) =>
            c.robots.Where(r => !string.IsNullOrEmpty(r.tier)).OrderBy(r => Array.IndexOf(RobotDef.Tiers, r.tier)).ToArray();

        void Rebuild() { root.Clear(); Tooltip.Hide(); Build(); }

        public void Build()
        {
            var all = Opponents(C);
            if (all.All(o => o.id != selected)) selected = all[0].id;
            var op = C.Robot(selected);

            Theme.Title(root, "ADVERSÁRIO", 44).At(60, 26);
            Theme.Label(root, "Escolha contra quem lutar · clique num robô para ler o dossiê", Theme.Small, Theme.TextDim).At(62, 80);

            // Grid: one scrolling column per category
            for (int t = 0; t < RobotDef.Tiers.Length; t++)
            {
                var tier = RobotDef.Tiers[t];
                float x = 40 + t * 262;
                var color = GarageScreen.TierColor(tier);
                Theme.Title(root, tier.ToUpperInvariant(), 24, color, TextAnchor.MiddleCenter).Place(x, 114, 250, 36);
                Theme.Solid(root, "TierLine", Theme.Alpha(color, 0.7f)).Place(x + 20, 152, 210, 3);
                var col = Theme.Scroll(root, "Tier:" + tier).Place(x, 164, 256, 700);
                int i = 0;
                foreach (var o in all.Where(r => r.tier == tier))
                {
                    var id = o.id;
                    bool sel = id == selected;
                    var card = Theme.Plate(col, "Opponent:" + id, "opponent").Size(244, 320).OnClick(() => { selected = id; Rebuild(); });
                    card.style.marginBottom = 12;
                    card.EnableInClassList("selected", sel);
                    Theme.Border(card, sel ? color : Theme.Alpha(color, 0.25f), sel ? 3 : 2);
                    var bot = RobotView.Create(card, false, sel).At(122, 132, 0.52f);
                    bot.Build(C, o);
                    bot.Root.pickingMode = PickingMode.Ignore;
                    bot.Root.Query().ForEach(e => e.pickingMode = PickingMode.Ignore); // clicks go to the card
                    Theme.Label(card, o.name, 21, Theme.Ink, TextAnchor.UpperCenter, FontStyle.Bold).Place(10, 258, 224, 56).Fit(15);
                    Fx.Enter(card, i++ + t);
                }
            }

            // Dossier of the selected opponent
            var dossier = Theme.El(root, "Dossier").Place(1120, 110, 760, 760);
            Dossier(C, dossier, op);

            // Player's robot preview
            var build = app.Garage.Current;
            var stats = BuildStats.Of(C, build);
            var mine = Theme.Plate(root, "Mine").Place(40, 880, 700, 180);
            RobotView.Create(mine, false, false).At(80, 90, 0.36f).Build(C, build);
            Theme.Label(mine, "SEU ROBÔ", 16, Theme.TextDim, style: FontStyle.Bold).At(160, 14);
            Theme.Label(mine, $"{build.name} · {C.Frame(build.frame).name}", 26, Theme.Ink, style: FontStyle.Bold).Place(160, 38, 520, 36).OneLine();
            Theme.Label(mine, $"Chassi {stats.chassis} · Integridade {stats.totalIntegrity} · Energia {stats.energy} · Mão {stats.hand}\n" +
                $"{stats.attacks} ataques ({stats.choose} escolhem a parte) · bônus {(stats.makers.Length == 0 ? "nenhum" : string.Join(" + ", stats.makers))}",
                Theme.Small, Theme.Ink).Place(160, 80, 530, 90).Fit(15);

            Theme.Button(root, "←  GARAGEM", app.ShowGarage, null, 24).Place(770, 978, 300, 76);
            var fight = Theme.Primary(root, "LUTAR", () => app.StartBattle(build, selected), 36);
            fight.name = "Lutar";
            fight.Place(1520, 966, 360, 88);
        }

        // The in-world file: header, category stamp, how it fights, rumors, works well / avoid (scrolls when long).
        // terrainId: the arena of this fight (null = the opponent's home arena).
        public static void Dossier(Content c, VisualElement parent, RobotDef op, string terrainId = null)
        {
            bool neutral = terrainId == "none"; // tutorial fight: no arena rule
            var terrain = neutral ? null : c.Terrain(terrainId ?? op.terrain);
            var paper = Theme.Plate(parent, "File", "paper").Fill();
            var header = Theme.El(paper, "Header", "paper-header").Place(0, 0, 760, 66);
            Theme.Title(header, "DOSSIÊ // " + op.name.ToUpperInvariant(), 24, Theme.Hex("E8D9B8"), TextAnchor.MiddleLeft).Place(24, 0, 560, 66).OneLine();
            var stamp = Theme.Chip(paper, op.tier.ToUpperInvariant(), GarageScreen.TierColor(op.tier), 22);
            stamp.parent.Place(590, 12, 150, 44).Rotate(-6);
            stamp.parent.AddToClassList("stamp");

            var d = op.dossier;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<color=#B9A98A>FABRICANTE PRINCIPAL</color>   <color=#{Theme.HexOf(Theme.Maker(c.MainMaker(op)))}><b>{c.MainMaker(op)}</b></color>");
            sb.AppendLine($"<color=#B9A98A>COMO LUTA</color>   {c.Describe(op)}.");
            if (neutral) sb.AppendLine("<color=#B9A98A>ARENA</color>   <b>Neutra</b> (luta de treino): sem regra de terreno.");
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
            bool face = Portraits.Has(op.name);
            var scroll = Theme.Scroll(paper, "Body").Place(24, 82, face ? 566 : 712, 662);
            Theme.Label(scroll, sb.ToString().TrimEnd(), 21, Theme.Hex("E8E1D0")).Classes("prose");
            if (face) Portraits.Create(paper, op.name, 140).Place(604, 84, 140, 140);
        }
    }
}
