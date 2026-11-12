using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattler.UI
{
    // Career places reached from the map.
    // "job": Celeste's job (offer, stage track, next opponent's dossier, repairs, reward choice, closing words).
    // "workshop": the Oficina (repairs + garage; crafting later). "pit": the Fosso ranking ladder.
    public class JobScreen
    {
        readonly GameUI app;
        readonly RectTransform root;
        readonly string page;
        bool abandonArmed;

        public JobScreen(GameUI app, RectTransform root, string page = "job") { this.app = app; this.root = root; this.page = page; }

        Content C => app.content;
        Career K => app.career;

        void Changed() { app.SaveCareer(); Theme.ClearChildren(root); Tooltip.Hide(); Build(); }

        public void Build()
        {
            Theme.Label(root, page == "workshop" ? "OFICINA" : page == "pit" ? "FOSSO" : "TRABALHO", 44, Theme.Ink, style: FontStyle.Bold).rectTransform.Place(60, 30, 500, 60);
            Theme.Label(root, $"{K.credits} créditos   ·   {K.scrap} sucata", 26, Theme.Warn, TextAnchor.MiddleRight, FontStyle.Bold).rectTransform.Place(1000, 34, 700, 50);
            var back = Theme.Button(root, "←  MAPA", app.ShowMap, Theme.Panel, 22);
            back.name = "Mapa";
            ((RectTransform)back.transform).Place(1720, 34, 160, 50);

            if (page == "workshop") Workshop();
            else if (page == "pit") Pit();
            else if (!K.InJob) Offer(C.jobs[0]);
            else if (K.offer.Length > 0) Reward();
            else if (K.JobDone(C)) Done();
            else Hub();
        }

        // ---- No job: the fixer's offer ----
        void Offer(JobDef job)
        {
            var panel = Theme.Box(root, "Offer", Theme.Alpha(Theme.Panel, 0.96f));
            panel.rectTransform.Place(60, 120, 1000, 820);
            Theme.Label(panel.transform, job.name.ToUpperInvariant(), 34, Theme.Ink, style: FontStyle.Bold).rectTransform.Place(36, 26, 920, 46);
            Theme.Label(panel.transform, "Oferecido por " + job.fixer, Theme.Small, Theme.TextDim).rectTransform.Place(36, 76, 920, 30);
            var intro = string.IsNullOrEmpty(K.lastResult) ? "" : $"<color=#{Theme.HexOf(Theme.Warn)}>{K.lastResult}</color>\n\n";
            Theme.Label(panel.transform, intro + $"<i>{job.intro}</i>", 24, Theme.Ink).rectTransform.Place(36, 120, 920, 360);
            Track(panel.transform, job.stages, -1, 36, 500);
            var accept = Theme.Button(panel.transform, "ACEITAR TRABALHO", () => { K.StartJob(C, job.id); K.lastResult = null; Changed(); },
                Theme.Mix(Theme.PanelHi, Theme.Exposed, 0.6f), 30);
            accept.name = "Aceitar";
            ((RectTransform)accept.transform).Place(36, 720, 420, 80);
            ((RectTransform)Theme.Button(panel.transform, "GARAGEM", app.ShowGarage, Theme.PanelHi, 26).transform).Place(476, 720, 260, 80);
            Status(1120, 120, false);
        }

        // The four fights in a row: done, current, ahead.
        void Track(Transform parent, StageDef[] stages, int current, float x, float y, string label = "LUTA")
        {
            for (int i = 0; i < stages.Length; i++)
            {
                var st = stages[i];
                var r = C.Robot(st.enemy);
                bool done = i < current, now = i == current;
                var color = GarageScreen.TierColor(r.tier);
                var box = Theme.Box(parent, "Stage" + i, now ? Theme.Mix(Theme.Panel, color, 0.3f) : Theme.Alpha(Theme.PanelHi, done ? 0.5f : 1));
                box.rectTransform.Place(x + i * 232, y, 220, 180);
                if (now) Theme.Box(box.transform, "Now", color, Theme.Frame).rectTransform.Fill(-3);
                Theme.Label(box.transform, $"{label} {i + 1}", 16, Theme.TextDim, style: FontStyle.Bold).rectTransform.Place(14, 10, 190, 24);
                var name = Theme.Label(box.transform, r.name, 22, done ? Theme.TextDim : Theme.Ink, style: FontStyle.Bold);
                name.rectTransform.Place(14, 36, 196, 60);
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 14; name.resizeTextMaxSize = 22; name.verticalOverflow = VerticalWrapMode.Truncate;
                Theme.Label(box.transform, r.tier.ToUpperInvariant(), 16, color, style: FontStyle.Bold).rectTransform.Place(14, 98, 190, 24);
                Theme.Label(box.transform, done ? "✓ VENCIDA" : $"+{st.credits} cr · +{st.scrap} sucata", 16, done ? Theme.Good : Theme.TextDim).rectTransform.Place(14, 140, 196, 26);
            }
        }

        // ---- Job in progress ----
        void Hub()
        {
            var job = C.Job(K.job);
            int idx = System.Math.Min(K.stage, job.stages.Length - 1);
            var st = job.stages[idx];
            Theme.Label(root, job.name, Theme.Small, Theme.TextDim).rectTransform.Place(62, 80, 900, 30);
            Track(root, job.stages, K.stage, 60, 120);

            var talk = Theme.Box(root, "Fixer", Theme.Alpha(Theme.Panel, 0.96f));
            talk.rectTransform.Place(60, 316, 1000, 190);
            var text = (string.IsNullOrEmpty(K.lastResult) ? "" : $"<color=#{Theme.HexOf(Theme.Warn)}>{K.lastResult}</color>\n") + $"<b>{job.fixer}:</b> <i>{st.text}</i>";
            Theme.Label(talk.transform, text, 22, Theme.Ink).rectTransform.Place(24, 18, 952, 160);

            Status(60, 524);

            var dossier = Theme.Node(root, "Dossier").Place(1120, 110, 760, 760);
            OpponentScreen.Dossier(C, dossier, C.Robot(st.enemy));

            ((RectTransform)Theme.Button(root, "GARAGEM", app.ShowGarage, Theme.Panel, 26).transform).Place(1120, 970, 220, 84);
            var quit = Theme.Button(root, "ABANDONAR", () =>
            {
                if (!abandonArmed) { abandonArmed = true; Changed(); return; }
                K.EndJob(); K.lastResult = "Você abandonou o trabalho. O dano continua no robô."; Changed();
            }, Theme.Mix(Theme.Panel, Theme.Bad, abandonArmed ? 0.6f : 0.2f), 22);
            quit.name = "Abandonar";
            ((RectTransform)quit.transform).Place(1350, 970, 160, 84);
            if (abandonArmed) Theme.Label(root, "Clique de novo para abandonar", Theme.Small, Theme.Bad, TextAnchor.MiddleRight).rectTransform.Place(1120, 920, 760, 40);

            var errors = K.Validate(C);
            var fight = Theme.Button(root, "LUTAR", app.StartJobFight, Theme.Mix(Theme.PanelHi, Theme.Exposed, 0.6f), 34);
            fight.name = "Lutar";
            ((RectTransform)fight.transform).Place(1520, 970, 360, 84);
            fight.interactable = errors.Count == 0 && K.offer.Length == 0;
            if (errors.Count > 0) Theme.Label(root, "Robô inválido: ajuste na garagem", Theme.Small, Theme.Bad, TextAnchor.MiddleRight).rectTransform.Place(1120, 880, 760, 40);
        }

        // Robot condition and repairs (shown with or without a job).
        // wide = 1000 px with the robot drawn, otherwise 760 px.
        void Status(float x, float y, bool wide = true)
        {
            var b = K.Build;
            var panel = Theme.Box(root, "Status", Theme.Alpha(Theme.Panel, 0.96f));
            panel.rectTransform.Place(x, y, wide ? 1000 : 760, 420);
            float ox = wide ? 290 : 30;
            if (wide)
            {
                var bot = RobotView.Create(panel.transform, false, false);
                bot.Rect.Center(140, 210, RobotView.W, RobotView.H);
                bot.transform.localScale = new Vector3(0.55f, 0.55f, 1);
                bot.Build(C, b);
            }
            Theme.Label(panel.transform, $"{b.name.ToUpperInvariant()} · ESTADO", 18, Theme.TextDim, style: FontStyle.Bold).rectTransform.Place(ox, 14, 600, 26);

            var rows = new (string icon, string partId)[] { ("Cabeca", b.head), ("Torso", b.torso), ("BracoEsq", b.armL), ("BracoDir", b.armR), ("Pernas", b.legs) };
            float ry = 48;
            foreach (var (icon, id) in rows)
            {
                var part = C.Part(id);
                int hp = K.HpOf(C, id);
                Row(panel.transform, ox, ry, icon, part.name, hp, part.integrity, hp < part.integrity ? id : null);
                ry += 52;
            }
            var frame = C.Frame(b.frame);
            int chassis = frame.chassis - K.chassisDamage;
            Row(panel.transform, ox, ry, "Chassi", "Chassi", chassis, frame.chassis, K.chassisDamage > 0 ? "" : null);

            int cost = K.FullRepairCost(C);
            var all = Theme.Button(panel.transform, cost == 0 ? "ROBÔ INTEIRO" : $"REPARO TOTAL ({cost} cr)", () => { if (K.RepairAllWithCredits(C)) Changed(); },
                Theme.PanelHi, 22);
            all.name = "RepararTudo";
            all.interactable = cost > 0 && K.credits >= cost;
            ((RectTransform)all.transform).Place(ox, 360, 320, 48);
            Theme.Label(panel.transform, $"1 sucata = +{Career.RepairPerScrap} integridade · crédito: {Career.CreditsPerPoint} por ponto", 16, Theme.TextDim, TextAnchor.MiddleLeft)
                .rectTransform.Place(ox + 334, 360, 380, 48);
        }

        // partId: null = intact, "" = chassis, otherwise the part to repair with scrap.
        void Row(Transform parent, float x, float y, string icon, string name, int hp, int max, string partId)
        {
            SlotIcons.Icon(parent, icon, 34, hp < max ? Theme.Warn : Theme.Good).Place(x, y + 4, 34, 34);
            Theme.Label(parent, name, 20, Theme.Ink, TextAnchor.MiddleLeft).rectTransform.Place(x + 44, y, 260, 42);
            var (fill, label) = Theme.Bar(parent, hp <= max / 3 ? Theme.Bad : hp < max ? Theme.Warn : Theme.Good, 18);
            ((RectTransform)fill.transform.parent).Place(x + 310, y + 6, 220, 30);
            fill.fillAmount = max == 0 ? 0 : (float)hp / max;
            label.text = $"{hp}/{max}";
            if (partId == null) return;
            var btn = Theme.Button(parent, "+ SUCATA", () => { if (K.RepairWithScrap(C, partId == "" ? null : partId)) Changed(); }, Theme.PanelHi, 16);
            btn.name = "Reparar:" + (partId == "" ? "Chassi" : partId);
            btn.interactable = K.scrap > 0;
            ((RectTransform)btn.transform).Place(x + 544, y + 4, 140, 34);
        }

        // ---- Oficina: repairs and the garage; crafting comes later ----
        void Workshop()
        {
            Theme.Label(root, "Cais do porto · bancada da Mecânica", Theme.Small, Theme.TextDim).rectTransform.Place(62, 80, 900, 30);
            var talk = Theme.Box(root, "Mechanic", Theme.Alpha(Theme.Panel, 0.96f));
            talk.rectTransform.Place(60, 120, 1000, 120);
            var line = C.Dialogue("oficina").lines[0];
            Theme.Label(talk.transform, $"<b>{line.who}:</b> <i>{line.text}</i>", 22, Theme.Ink).rectTransform.Place(24, 18, 952, 90);
            Status(60, 260);

            var side = Theme.Box(root, "Bench", Theme.Alpha(Theme.Panel, 0.96f));
            side.rectTransform.Place(1120, 120, 760, 560);
            Theme.Label(side.transform, "BANCADA", 26, Theme.Ink, style: FontStyle.Bold).rectTransform.Place(30, 24, 700, 36);
            Theme.Label(side.transform, $"Peças no estoque: {K.owned.Length} · frames: {K.frames.Length}\n\nNa garagem você monta os 3 robôs com as peças que já tem.", 22, Theme.Ink)
                .rectTransform.Place(30, 76, 700, 140);
            var garage = Theme.Button(side.transform, "ABRIR GARAGEM", app.ShowGarage, Theme.Mix(Theme.PanelHi, Theme.Exposed, 0.6f), 28);
            garage.name = "Garagem";
            ((RectTransform)garage.transform).Place(30, 230, 400, 80);
            var craft = Theme.Button(side.transform, "FABRICAR  ·  em breve", () => { }, Theme.Panel, 22);
            craft.interactable = false;
            ((RectTransform)craft.transform).Place(30, 330, 400, 64);
            Theme.Label(side.transform, "Transformar sucata em peças novas. Ainda não está aberto.", 18, Theme.TextDim).rectTransform.Place(30, 404, 700, 60);
        }

        // ---- Fosso: ranking ladder up to the champion. Losing costs only the ranking ----
        void Pit()
        {
            bool champ = K.Champion(C);
            Theme.Label(root, champ ? "Você é o campeão dos Fossos. Defenda o título quando quiser." : $"Ranking do Fosso · posição {C.pit.Length - K.pitRank + 1} · perder só custa a posição",
                Theme.Small, champ ? Theme.Good : Theme.TextDim).rectTransform.Place(62, 80, 1000, 30);
            Track(root, C.pit, K.pitRank, 60, 120, "DEGRAU");

            var st = K.PitStage(C);
            var talk = Theme.Box(root, "Announcer", Theme.Alpha(Theme.Panel, 0.96f));
            talk.rectTransform.Place(60, 316, 1000, 190);
            var text = (string.IsNullOrEmpty(K.lastPit) ? "" : $"<color=#{Theme.HexOf(Theme.Warn)}>{K.lastPit}</color>\n")
                       + (champ ? "<b>Locutor:</b> <i>O campeão está na casa! Quem sobe pra tirar o cinturão?</i>" : $"<b>Locutor:</b> <i>{st.text}</i>");
            Theme.Label(talk.transform, text, 22, Theme.Ink).rectTransform.Place(24, 18, 952, 160);
            Status(60, 524);

            var dossier = Theme.Node(root, "Dossier").Place(1120, 110, 760, 760);
            OpponentScreen.Dossier(C, dossier, C.Robot(st.enemy));

            ((RectTransform)Theme.Button(root, "GARAGEM", app.ShowGarage, Theme.Panel, 26).transform).Place(1120, 970, 220, 84);
            var errors = K.Validate(C);
            var fight = Theme.Button(root, champ ? "DEFENDER TÍTULO" : "LUTAR", app.StartPitFight, Theme.Mix(Theme.PanelHi, Theme.Exposed, 0.6f), champ ? 28 : 34);
            fight.name = "Lutar";
            ((RectTransform)fight.transform).Place(1520, 970, 360, 84);
            fight.interactable = errors.Count == 0 && K.offer.Length == 0;
            if (K.offer.Length > 0) Theme.Label(root, "Escolha a recompensa do trabalho antes", Theme.Small, Theme.Bad, TextAnchor.MiddleRight).rectTransform.Place(1120, 880, 760, 40);
            else if (errors.Count > 0) Theme.Label(root, "Robô inválido: ajuste na garagem", Theme.Small, Theme.Bad, TextAnchor.MiddleRight).rectTransform.Place(1120, 880, 760, 40);
        }

        // ---- After a win: pick 1 of 2 parts from the beaten robot ----
        void Reward()
        {
            var shade = Theme.Solid(root, "Reward", Theme.Alpha(Theme.Bg, 0.6f));
            shade.rectTransform.Fill();
            shade.raycastTarget = true;
            Theme.Label(shade.transform, "ESCOLHA UMA PEÇA", 56, Theme.Good, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform.Center(960, 170, 1200, 80);
            Theme.Label(shade.transform, K.lastResult + "\nDo que sobrou do adversário, você pode levar uma peça.", 24, Theme.Ink, TextAnchor.MiddleCenter).rectTransform.Center(960, 250, 1400, 80);
            for (int i = 0; i < K.offer.Length; i++)
            {
                var part = C.Part(K.offer[i]);
                var id = part.id;
                float cx = K.offer.Length == 1 ? 960 : 700 + i * 520;
                var card = Theme.Box(shade.transform, "Offer:" + id, Theme.Panel);
                card.rectTransform.Center(cx, 560, 460, 420);
                Theme.Box(card.transform, "Edge", Theme.Maker(part.maker), Theme.Frame).rectTransform.Fill(-3);
                SlotIcons.Icon(card.transform, part.slot == "Braco" ? "BracoDir" : part.slot, 56, Theme.Maker(part.maker)).Place(24, 24, 56, 56);
                Theme.Label(card.transform, part.name, 30, Theme.Ink, style: FontStyle.Bold).rectTransform.Place(96, 20, 340, 40);
                Theme.Label(card.transform, part.maker ?? "", 20, Theme.Maker(part.maker)).rectTransform.Place(96, 60, 340, 28);
                string stats = part.slot == "Nucleo" ? $"{part.energy} energia/turno · Peso {part.weight}" : $"Integridade {part.integrity} · Peso {part.weight}";
                string cards = string.Join("\n", part.cards.GroupBy(c => c).Select(g => $"{g.Count()}x {C.Card(g.Key).name}"));
                string copy = K.owned.Contains(id) ? $"\n\n<color=#{Theme.HexOf(Theme.Warn)}>Você já tem uma: a segunda vai no outro braço.</color>" : "";
                Theme.Label(card.transform, stats + "\n\n" + cards + copy, 22, Theme.Ink).rectTransform.Place(24, 110, 412, 210);
                var take = Theme.Button(card.transform, "PEGAR", () => { K.Choose(id); Changed(); }, Theme.Mix(Theme.PanelHi, Theme.Good, 0.45f), 28);
                take.name = "Pegar:" + id;
                ((RectTransform)take.transform).Place(24, 336, 412, 64);
            }
            var skip = Theme.Button(shade.transform, K.offer.Length == 0 ? "CONTINUAR" : "DISPENSAR", () => { K.SkipReward(); Changed(); }, Theme.PanelHi, 24);
            skip.name = "Dispensar";
            ((RectTransform)skip.transform).Center(960, 870, 300, 70);
        }

        // ---- Job finished ----
        void Done()
        {
            var job = C.Job(K.job);
            var panel = Theme.Box(root, "Done", Theme.Alpha(Theme.Panel, 0.96f));
            panel.rectTransform.Place(60, 120, 1000, 820);
            Theme.Label(panel.transform, "TRABALHO CONCLUÍDO", 40, Theme.Good, style: FontStyle.Bold).rectTransform.Place(36, 26, 920, 54);
            Theme.Label(panel.transform, $"{K.lastResult}\n\n<b>{job.fixer}:</b> <i>{job.outro}</i>", 24, Theme.Ink).rectTransform.Place(36, 100, 920, 380);
            Track(panel.transform, job.stages, job.stages.Length, 36, 500);
            var end = Theme.Button(panel.transform, "ENCERRAR", () => { K.EndJob(); K.lastResult = "Trabalho concluído. A Celeste disse que te chama de novo."; app.SaveCareer(); app.ShowMap(); },
                Theme.Mix(Theme.PanelHi, Theme.Exposed, 0.6f), 30);
            end.name = "Encerrar";
            ((RectTransform)end.transform).Place(36, 720, 360, 80);
            Status(1120, 120, false);
        }
    }
}
