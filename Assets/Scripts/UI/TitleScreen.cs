using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace CardBattler.UI
{
    // Title screen: Jogar, Sobre (for playtesters), Histórico (how to send the battle logs), Sair.
    // "about" and "history" are panels of this screen.
    public class TitleScreen
    {
        readonly GameUI app;
        readonly VisualElement root;
        readonly string page; // "title", "about" or "history"

        public TitleScreen(GameUI app, VisualElement root, string page) { this.app = app; this.root = root; this.page = page; }

        // Battles recorded in a folder (each one is a battle_*.jsonl + a summary).
        public static int HistoryCount(string dir) =>
            Directory.Exists(dir) ? Directory.GetFiles(dir, "battle_*.jsonl").Length : 0;

        public void Build()
        {
            Theme.Title(root, "CARD BATTLER", 92, Theme.Ink).Classes("logo").At(110, 70);
            Theme.Label(root, $"Robôs de sucata, cartas e lutas nos Fossos   ·   protótipo v{Application.version}", 24, Theme.Exposed).At(116, 190);

            // Showcase: the current robot idling on a lit pedestal.
            Theme.El(root, "Pedestal", "pedestal").Center(1500, 900, 760, 120);
            var robot = RobotView.Create(root, false, true).At(1500, 560, 1.5f);
            robot.Build(app.content, (app.HasCareer ? app.LoadedCareer?.Build : null) ?? app.garage.Current);

            if (page == "about") About();
            else if (page == "history") History();
            else Menu();
        }

        void Menu()
        {
            int count = HistoryCount(GameUI.LogDir);
            bool saved = app.HasCareer;
            bool armed = false; // "Novo jogo" over an existing save needs a second click
            Button newGame = null;
            var items = new List<(string label, string hint, System.Action action)> { (saved ? "CONTINUAR" : "JOGAR", saved ? "Volta para o porto, de onde parou." : "Começa a carreira no porto.", app.PlayCareer) };
            if (saved) items.Add(("NOVO JOGO", "Apaga o save atual e recomeça.", () =>
            {
                if (armed) { app.NewCareer(); return; }
                armed = true;
                Theme.SetLabel(newGame, "CLIQUE DE NOVO: APAGA O SAVE");
                newGame.AddToClassList("btn-danger");
            }));
            items.Add(("TREINO LIVRE", "Todas as peças liberadas, sem progresso.", app.FreePlay));
            items.Add(("SOBRE", "O que é o jogo e o que queremos saber.", () => app.ShowTitle("about")));
            items.Add(($"HISTÓRICO ({count})", "Suas lutas gravadas e como enviar.", () => app.ShowTitle("history")));
            items.Add(("SAIR", "Fecha o jogo.", GameUI.Quit));

            var menu = Theme.Column(root, "Menu", 14).Place(110, 270, 560, 680);
            for (int i = 0; i < items.Count; i++)
            {
                bool main = i == 0;
                var (label, hint, action) = items[i];
                var b = main ? Theme.Primary(menu, label, action, 36) : Theme.Button(menu, label, action, null, 28);
                b.name = "Menu:" + label.Split(' ')[0];
                b.style.height = main ? 92 : 74;
                Theme.Hover(b, hint);
                if (label == "NOVO JOGO") newGame = b;
                Fx.Enter(b, i);
            }
            Theme.Label(root, "ESC: sair   ·   passe o mouse nos botões para ver o que fazem", Theme.Small, Theme.TextDim).At(116, 960);
            var motion = Theme.Button(root, (Fx.Reduced ? "[x]" : "[ ]") + " Reduzir animações", () => { Fx.Reduced = !Fx.Reduced; app.ShowTitle(); }, null, Theme.Small, "btn-small");
            motion.name = "ReduzirAnimacoes";
            motion.Place(110, 1000, 300, 50);
        }

        // Page panel with a scrolling body; returns the body.
        VisualElement Page(string title)
        {
            var panel = Theme.Plate(root, "Page").Place(110, 270, 1020, 760);
            Theme.Title(panel, title, 34).At(36, 28);
            var scroll = Theme.Scroll(panel).Place(36, 90, 948, 540);
            var back = Theme.Button(panel, "←  VOLTAR", () => app.ShowTitle(), null, 24);
            back.name = "Voltar";
            back.Place(36, 660, 240, 70);
            Theme.Label(panel, "ESC: voltar", Theme.Small, Theme.TextDim, TextAnchor.MiddleLeft).Place(296, 660, 300, 70);
            return scroll;
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
                "Cada luta fica gravada no Histórico, que você pode nos enviar."), 24, Theme.Ink).Classes("prose");
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
                24, Theme.Ink).Classes("prose");
            Theme.Label(p, "Pasta: " + dir, Theme.Small, Theme.TextDim).Classes("prose", "selectable");
            var row = Theme.Row(p, "Actions", 20);
            row.style.marginTop = 20;
            Label copied = null;
            Theme.Primary(row, "ABRIR PASTA", () => { Directory.CreateDirectory(dir); Application.OpenURL("file:///" + dir.Replace('\\', '/')); }, 26).Size(300, 74);
            Theme.Button(row, "COPIAR CAMINHO", () => { GUIUtility.systemCopyBuffer = dir; copied.text = "Caminho copiado."; }, null, 24).Size(300, 74);
            copied = Theme.Label(row, "", 20, Theme.Good, TextAnchor.MiddleLeft);
            copied.style.alignSelf = Align.Center;
        }
    }
}
