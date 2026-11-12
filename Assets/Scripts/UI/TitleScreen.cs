using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattler.UI
{
    // Title screen: Jogar, Sobre (for playtesters), Histórico (how to send the battle logs), Sair.
    // "about" and "history" are panels of this screen.
    public class TitleScreen
    {
        readonly GameUI app;
        readonly RectTransform root;
        readonly string page; // "title", "about" or "history"

        public TitleScreen(GameUI app, RectTransform root, string page) { this.app = app; this.root = root; this.page = page; }

        // Battles recorded in a folder (each one is a battle_*.jsonl + a summary).
        public static int HistoryCount(string dir) =>
            Directory.Exists(dir) ? Directory.GetFiles(dir, "battle_*.jsonl").Length : 0;

        public void Build()
        {
            Theme.Label(root, "CARD BATTLER", 96, Theme.Ink, TextAnchor.UpperLeft, FontStyle.Bold).rectTransform.Place(110, 90, 1000, 120);
            Theme.Label(root, $"Robôs de sucata, cartas e lutas nos Fossos   ·   protótipo v{Application.version}", 24, Theme.Exposed).rectTransform.Place(116, 205, 1000, 36);

            var robot = RobotView.Create(root, false, false);
            robot.Rect.Center(1530, 560, RobotView.W, RobotView.H);
            robot.transform.localScale = new Vector3(1.5f, 1.5f, 1);
            robot.Build(app.content, app.garage.Current);

            if (page == "about") About();
            else if (page == "history") History();
            else Menu();
        }

        void Menu()
        {
            int count = HistoryCount(GameUI.LogDir);
            var items = new (string label, System.Action action)[]
            {
                ("JOGAR", app.ShowGarage),
                ("SOBRE", () => app.ShowTitle("about")),
                ($"HISTÓRICO ({count})", () => app.ShowTitle("history")),
                ("SAIR", GameUI.Quit),
            };
            for (int i = 0; i < items.Length; i++)
            {
                bool main = i == 0;
                var b = Theme.Button(root, items[i].label, items[i].action, main ? Theme.Mix(Theme.PanelHi, Theme.Exposed, 0.6f) : Theme.Panel, main ? 38 : 30);
                b.name = "Menu:" + items[i].label.Split(' ')[0];
                ((RectTransform)b.transform).Place(110, 330 + i * 112, 520, 92);
            }
            Theme.Label(root, "ESC: sair", Theme.Small, Theme.TextDim).rectTransform.Place(116, 790, 400, 30);
        }

        RectTransform Page(string title)
        {
            var panel = Theme.Box(root, "Page", Theme.Alpha(Theme.Panel, 0.96f));
            panel.rectTransform.Place(110, 300, 1020, 720);
            Theme.Label(panel.transform, title, 34, Theme.Ink, style: FontStyle.Bold).rectTransform.Place(36, 26, 900, 46);
            var back = Theme.Button(panel.transform, "←  VOLTAR", () => app.ShowTitle(), Theme.PanelHi, 24);
            back.name = "Voltar";
            ((RectTransform)back.transform).Place(36, 630, 240, 64);
            Theme.Label(panel.transform, "ESC: voltar", Theme.Small, Theme.TextDim).rectTransform.Place(296, 650, 300, 30);
            return panel.rectTransform;
        }

        void About()
        {
            var p = Page("SOBRE O JOGO");
            Theme.Label(p, Theme.Highlight(
                "Você monta um robô de sucata com peças: cabeça, torso, dois braços, pernas e núcleo. Cada peça traz algumas cartas para o seu baralho.\n\n" +
                "Na luta, a cada turno você recebe energia e uma mão de cartas. Use as cartas para atacar as peças do robô inimigo, se defender ou se consertar. " +
                "Quando uma peça do inimigo quebra, o chassi dele fica exposto, e chassi em zero é vitória. O mesmo vale para você.\n\n" +
                "Dicas: cartas baratas acertam uma peça ao acaso; as mais caras deixam você escolher. Arraste uma carta para a Sucata para ganhar energia. " +
                "Antes de lutar, leia o dossiê do adversário: ele conta o que funciona contra ele. Na garagem, o \"?\" explica cada número.\n\n" +
                "O que queremos saber: se ficou claro o que fazer, se as lutas pareceram justas, quais cartas você nunca usou e o que foi chato ou divertido. " +
                "Cada luta fica gravada no Histórico, que você pode nos enviar."), 24, Theme.Ink).rectTransform.Place(36, 92, 948, 520);
        }

        void History()
        {
            var dir = GameUI.LogDir;
            int count = HistoryCount(dir);
            var p = Page($"HISTÓRICO DE LUTAS ({count})");
            string intro = count == 0
                ? "Nenhuma luta gravada ainda. Jogue algumas lutas e volte aqui."
                : $"{count} luta(s) gravada(s). Cada luta gera dois arquivos: o registro completo e um resumo.";
            Theme.Label(p, intro + "\n\n" +
                "Como enviar:\n" +
                "1. Clique em ABRIR PASTA.\n" +
                "2. Selecione todos os arquivos, clique com o botão direito e escolha Compactar (zip).\n" +
                "3. Envie o .zip para quem te convidou para o teste, junto com o que você achou.\n\n" +
                "Os arquivos têm só os dados das lutas: cartas jogadas, dano e resultado. Nada pessoal.",
                24, Theme.Ink).rectTransform.Place(36, 92, 948, 330);
            Theme.Label(p, "Pasta: " + dir, 18, Theme.TextDim).rectTransform.Place(36, 440, 948, 56);
            var open = Theme.Button(p, "ABRIR PASTA", () => { Directory.CreateDirectory(dir); Application.OpenURL("file:///" + dir.Replace('\\', '/')); },
                Theme.Mix(Theme.PanelHi, Theme.Exposed, 0.5f), 26);
            ((RectTransform)open.transform).Place(36, 510, 300, 74);
            Text copied = null;
            var copy = Theme.Button(p, "COPIAR CAMINHO", () => { GUIUtility.systemCopyBuffer = dir; if (copied) copied.text = "Caminho copiado."; }, Theme.PanelHi, 24);
            ((RectTransform)copy.transform).Place(356, 510, 300, 74);
            copied = Theme.Label(p, "", 20, Theme.Good, TextAnchor.MiddleLeft);
            copied.rectTransform.Place(676, 510, 300, 74);
        }
    }
}
