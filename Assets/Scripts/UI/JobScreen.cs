using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace CardBattler.UI
{
    // Career places reached from the map.
    // "job": Celeste's job (offer, stage track, next opponent's dossier, repairs, reward choice, closing words).
    // "workshop": the Oficina (repairs + garage; crafting later). "pit": the Fosso ranking ladder.
    public class JobScreen
    {
        readonly GameUI app;
        readonly VisualElement root;
        readonly string page;
        bool abandonArmed;

        public JobScreen(GameUI app, VisualElement root, string page = "job") { this.app = app; this.root = root; this.page = page; }

        Content C => app.content;
        Career K => app.career;

        void Changed() { app.SaveCareer(); root.Clear(); Tooltip.Hide(); Build(); }

        public void Build()
        {
            Theme.Title(root, page == "workshop" ? "OFICINA" : page == "pit" ? "FOSSO" : page == "circuit" ? "CIRCUITO REGIONAL" : page == "ferro" ? "FERRO-VELHO" : "TRABALHO", 44).At(60, 26);
            MapScreen.Wallet(root, K, 1030);
            var back = Theme.Button(root, "←  MAPA", app.ShowMap, null, 22);
            back.name = "Mapa";
            back.Place(1670, 30, 210, 54);

            if (page == "workshop") Workshop();
            else if (page == "ferro") Ferro();
            else if (page == "pit" || page == "circuit") Ladder(page);
            else if (!K.InJob) Offer(C.jobs[0]);
            else if (K.offer.Length > 0) Reward();
            else if (K.JobDone(C)) Done();
            else Hub();
        }

        // ---- No job: the fixer's offer ----
        void Offer(JobDef job)
        {
            var panel = Theme.Plate(root, "Offer");
            panel.Place(60, 120, 1000, 820);
            Theme.Title(panel, job.name.ToUpperInvariant(), 32).Place(36, 26, 920, 46).OneLine();
            Theme.Label(panel, "Oferecido por " + job.fixer, Theme.Small, Theme.TextDim).Place(36, 76, 920, 30);
            var intro = string.IsNullOrEmpty(K.lastResult) ? "" : $"<color=#{Theme.HexOf(Theme.Warn)}>{K.lastResult}</color>\n\n";
            var introBox = Theme.Scroll(panel, "Intro").Place(36, 120, 928, 360);
            Theme.Label(introBox, intro + $"<i>{job.intro}</i>", 24, Theme.Ink).Classes("prose");
            Track(panel, job.stages, -1, 36, 500);
            var accept = Theme.Button(panel, "ACEITAR TRABALHO", () => { K.StartJob(C, job.id); K.lastResult = null; Changed(); }, null, 30, "btn-primary");
            accept.name = "Aceitar";
            accept.Place(36, 720, 420, 80);
            Theme.Button(panel, "GARAGEM", app.ShowGarage, null, 26).Place(476, 720, 260, 80);
            Status(1120, 120, false);
        }

        // The four fights in a row: done, current, ahead.
        void Track(VisualElement parent, StageDef[] stages, int current, float x, float y, string label = "LUTA")
        {
            for (int i = 0; i < stages.Length; i++)
            {
                var st = stages[i];
                var r = C.Robot(st.enemy);
                bool done = i < current, now = i == current;
                var color = GarageScreen.TierColor(r.tier);
                var box = Theme.Box(parent, "Stage" + i, now ? Theme.Mix(Theme.Panel, color, 0.3f) : Theme.Alpha(Theme.PanelHi, done ? 0.5f : 1)).Classes("stage-card");
                box.Place(x + i * 232, y, 220, 180);
                box.EnableInClassList("now", now);
                box.EnableInClassList("done", done);
                Theme.Border(box, now ? color : Theme.Alpha(color, 0.25f), now ? 3 : 1);
                Fx.Enter(box, i);
                Theme.Hover(box, $"{r.name} ({r.tier})", done ? "Vencida." : $"Prêmio: +{st.credits} créditos e +{st.scrap} sucata.");
                Theme.Label(box, $"{label} {i + 1}", 16, Theme.TextDim, style: FontStyle.Bold).Place(14, 10, 190, 24);
                Theme.Label(box, r.name, 22, done ? Theme.TextDim : Theme.Ink, style: FontStyle.Bold).Place(14, 36, 196, 60).Fit(15);
                Theme.Label(box, r.tier.ToUpperInvariant(), 16, color, style: FontStyle.Bold).Place(14, 98, 190, 24);
                Theme.Label(box, done ? "✓ VENCIDA" : $"+{st.credits} cr · +{st.scrap} sucata", 16, done ? Theme.Good : Theme.TextDim).Place(14, 140, 196, 26).OneLine();
            }
        }

        // ---- Job in progress ----
        void Hub()
        {
            var job = C.Job(K.job);
            int idx = System.Math.Min(K.stage, job.stages.Length - 1);
            var st = job.stages[idx];
            Theme.Label(root, job.name, Theme.Small, Theme.TextDim).Place(62, 80, 900, 30);
            Track(root, job.stages, K.stage, 60, 120);

            var talk = Theme.Plate(root, "Fixer");
            talk.Place(60, 316, 1000, 190);
            var text = (string.IsNullOrEmpty(K.lastResult) ? "" : $"<color=#{Theme.HexOf(Theme.Warn)}>{K.lastResult}</color>\n") + $"<b>{job.fixer}:</b> <i>{st.text}</i>";
            Portraits.Create(talk, job.fixer, 150).Place(20, 20, 150, 150);
            Theme.Label(Theme.Scroll(talk, "Said").Place(190, 18, 792, 160), text, 22, Theme.Ink).Classes("prose");

            Status(60, 524);

            var dossier = Theme.El(root, "Dossier").Place(1120, 110, 760, 760);
            OpponentScreen.Dossier(C, dossier, C.Robot(st.enemy), Career.ArenaFor(st, Career.Arena));

            Theme.Button(root, "GARAGEM", app.ShowGarage, null, 26).Place(1120, 970, 220, 84);
            var quit = Theme.Button(root, "ABANDONAR", () =>
            {
                if (!abandonArmed) { abandonArmed = true; Changed(); return; }
                K.EndJob(); K.lastResult = "Você abandonou o trabalho. O dano continua no robô."; Changed();
            }, null, 22, abandonArmed ? "btn-danger" : null);
            quit.name = "Abandonar";
            quit.Place(1350, 970, 160, 84);
            if (abandonArmed) Theme.Label(root, "Clique de novo para abandonar", Theme.Small, Theme.Bad, TextAnchor.MiddleRight).Place(1120, 920, 760, 40);

            var errors = K.Validate(C);
            var fight = Theme.Button(root, "LUTAR", app.StartJobFight, null, 34, "btn-primary");
            fight.name = "Lutar";
            fight.Place(1520, 970, 360, 84);
            fight.SetEnabled(errors.Count == 0 && K.offer.Length == 0);
            if (errors.Count > 0) Theme.Label(root, "Robô inválido: ajuste na garagem", Theme.Small, Theme.Bad, TextAnchor.MiddleRight).Place(1120, 880, 760, 40);
        }

        // Robot condition and repairs (shown with or without a job).
        // wide = 1000 px with the robot drawn, otherwise 760 px.
        void Status(float x, float y, bool wide = true)
        {
            var b = K.Build;
            var panel = Theme.Plate(root, "Status");
            panel.Place(x, y, wide ? 1000 : 760, 420);
            float ox = wide ? 290 : 30;
            if (wide)
            {
                RobotView.Create(panel, false, true).At(140, 210, 0.55f).Build(C, b);
            }
            Theme.Label(panel, $"{b.name.ToUpperInvariant()} · ESTADO", 18, Theme.TextDim, style: FontStyle.Bold).Place(ox, 14, 600, 26);

            var rows = new (string icon, string partId)[] { ("Cabeca", b.head), ("Torso", b.torso), ("BracoEsq", b.armL), ("BracoDir", b.armR), ("Pernas", b.legs) };
            float ry = 48;
            foreach (var (icon, id) in rows)
            {
                var part = C.Part(id);
                int hp = K.HpOf(C, id);
                Row(panel, ox, ry, icon, part.name, hp, part.integrity, hp < part.integrity ? id : null);
                ry += 52;
            }
            var frame = C.Frame(b.frame);
            int chassis = frame.chassis - K.chassisDamage;
            Row(panel, ox, ry, "Chassi", "Chassi", chassis, frame.chassis, K.chassisDamage > 0 ? "" : null);

            int cost = K.FullRepairCost(C);
            var all = Theme.Button(panel, cost == 0 ? "ROBÔ INTEIRO" : $"REPARO TOTAL ({cost} cr)", () => { if (K.RepairAllWithCredits(C)) Changed(); }, null, 20);
            all.name = "RepararTudo";
            all.SetEnabled(cost > 0 && K.credits >= cost);
            all.Place(ox, 360, 320, 48);
            Theme.Label(panel, $"1 sucata = +{Career.RepairPerScrap} integridade · crédito: {Career.CreditsPerPoint} por ponto", 16, Theme.TextDim, TextAnchor.MiddleLeft)
                .Place(ox + 334, 356, wide ? 370 : 390, 56).Fit(14);
        }

        // partId: null = intact, "" = chassis, otherwise the part to repair with scrap.
        void Row(VisualElement parent, float x, float y, string icon, string name, int hp, int max, string partId)
        {
            SlotIcons.Icon(parent, icon, 34, hp < max ? Theme.Warn : Theme.Good).Place(x, y + 4, 34, 34);
            Theme.Label(parent, name, 20, Theme.Ink, TextAnchor.MiddleLeft).Place(x + 44, y, 260, 42).OneLine();
            var (fill, label) = Theme.Bar(parent, hp <= max / 3 ? Theme.Bad : hp < max ? Theme.Warn : Theme.Good, 18);
            fill.parent.Place(x + 310, y + 6, 220, 30);
            Theme.SetFill(fill, max == 0 ? 0 : (float)hp / max);
            label.text = $"{hp}/{max}";
            if (partId == null) return;
            var btn = Theme.Button(parent, "+ SUCATA", () => { if (K.RepairWithScrap(C, partId == "" ? null : partId)) Changed(); }, null, 16, "btn-small");
            btn.name = "Reparar:" + (partId == "" ? "Chassi" : partId);
            btn.SetEnabled(K.scrap > 0);
            btn.Place(x + 544, y + 4, 140, 34);
        }

        // ---- Oficina: repairs and the garage; crafting comes later ----
        void Workshop()
        {
            Theme.Label(root, "Cais do porto · bancada da Mecânica", Theme.Small, Theme.TextDim).Place(62, 80, 900, 30);
            var talk = Theme.Plate(root, "Mechanic");
            talk.Place(60, 120, 1000, 120);
            var line = C.Dialogue("oficina").lines[0];
            Portraits.Create(talk, line.who, 96).Place(14, 12, 96, 96);
            Theme.Label(talk, $"<b>{line.who}:</b> <i>{line.text}</i>", 22, Theme.Ink).Place(126, 14, 850, 92).Fit(Theme.Small);
            Status(60, 260);

            var side = Theme.Plate(root, "Bench");
            side.Place(1120, 120, 760, 560);
            Theme.Title(side, "BANCADA", 26).At(30, 24);
            Theme.Label(side, $"Peças no estoque: {K.owned.Length} · frames: {K.frames.Length}\nNa garagem você monta os 3 robôs com as peças que já tem.", 20, Theme.Ink)
                .Place(30, 70, 700, 60);
            var garage = Theme.Button(side, "ABRIR GARAGEM", app.ShowGarage, null, 26, "btn-primary");
            garage.name = "Garagem";
            garage.Place(30, 140, 360, 64);

            // FABRICAR: known blueprints turn scrap + credits into a part.
            Theme.Title(side, "FABRICAR", 24).At(30, 226);
            if (K.blueprints.Length == 0)
                Theme.Label(side, "Nenhuma planta ainda. O Ferro-velho do Duda vende e troca plantas.", 18, Theme.TextDim).Place(30, 264, 700, 50);
            float y = 264;
            foreach (var id in K.blueprints)
            {
                var bp = C.Blueprint(id);
                var part = C.Part(bp.part);
                SlotIcons.Icon(side, part.slot == "Braco" ? "BracoDir" : part.slot, 40, Theme.Maker(part.maker)).Place(30, y + 6, 40, 40);
                Theme.Label(side, $"<b>{part.name}</b>  <color=#{Theme.HexOf(Theme.Maker(part.maker))}>{part.maker}</color>\n<size=16>{bp.scrap} sucata + {bp.credits} créditos</size>", 20, Theme.Ink)
                    .Place(80, y, 380, 52);
                var make = Theme.Button(side, "FABRICAR", () => { if (K.Craft(C, id)) Changed(); }, null, 20, "btn-good");
                make.name = "Fabricar:" + id;
                make.SetEnabled(K.CanCraft(C, id));
                make.Place(500, y + 4, 220, 46);
                y += 62;
            }
        }

        // ---- Ferro-velho: Duda's shop (buy / sell) and his job ----
        void Ferro()
        {
            Theme.Label(root, "Pátio do Duda · compra, venda e plantas", Theme.Small, Theme.TextDim).Place(62, 80, 900, 30);
            var duda = C.jobs.FirstOrDefault(j => j.fixer == "Duda");
            bool done = duda != null && K.blueprints.Contains(duda.blueprint);
            var talk = Theme.Plate(root, "Duda");
            talk.Place(60, 120, 1000, 190);
            Portraits.Create(talk, "Duda", 150).Place(20, 20, 150, 150);
            var line = C.Dialogue(done ? "duda_done" : "duda").lines[0];
            string offer = duda == null || done ? "" : K.InJob ? (K.job == duda.id ? "\n<color=#FFC24A>Trabalho em andamento.</color>" : "\n<color=#8A8FA3>Termine o trabalho atual para pegar o do Duda.</color>") : $"\n<i>{duda.intro}</i>";
            Theme.Label(Theme.Scroll(talk, "Said").Place(190, 14, 566, 166), $"<b>{line.who}:</b> <i>{line.text}</i>{offer}", 20, Theme.Ink).Classes("prose");
            if (duda != null && !done)
            {
                bool mine = K.job == duda.id;
                var go = Theme.Button(talk, mine ? "VER TRABALHO" : "ACEITAR TRABALHO", () =>
                {
                    if (!mine) { K.StartJob(C, duda.id); K.lastResult = null; app.SaveCareer(); }
                    app.ShowJob();
                }, null, 20, "btn-primary");
                go.name = "TrabalhoDuda";
                go.SetEnabled(mine || !K.InJob);
                go.Place(770, 60, 210, 70);
            }

            // Buy
            var buy = Theme.Plate(root, "Buy");
            buy.Place(60, 326, 1000, 640);
            Theme.Title(buy, "COMPRAR", 24).At(24, 14);
            var shop = Theme.Scroll(buy, "Shop").Place(12, 54, 976, 576);
            var grid = Theme.El(shop, "Grid", "shop-grid").Size(960, 58 * ((C.shop.Length + 1) / 2));
            for (int i = 0; i < C.shop.Length; i++)
            {
                var item = C.shop[i];
                float x = 12 + (i % 2) * 480, y = (i / 2) * 58;
                string label, icon; Color col;
                if (item.part != null) { var pd = C.Part(item.part); label = $"<b>{pd.name}</b>  <size=15><color=#{Theme.HexOf(Theme.Maker(pd.maker))}>{pd.maker}</color>{(K.owned.Contains(pd.id) ? " · tem " + K.owned.Count(o => o == pd.id) : "")}</size>"; icon = pd.slot == "Braco" ? "BracoDir" : pd.slot; col = Theme.Maker(pd.maker); }
                else if (item.frame != null) { var fd = C.Frame(item.frame); label = $"<b>Frame {fd.name}</b>  <size=15>chassi {fd.chassis} · mão {fd.hand}</size>"; icon = "Frame"; col = Theme.Exposed; }
                else { var bp = C.Blueprint(item.blueprint); label = $"<b>{bp.name}</b>  <size=15>fabrica {C.Part(bp.part).name}</size>"; icon = SlotIcons.Nucleo; col = Theme.Good; }
                SlotIcons.Icon(grid, icon, 36, col).Place(x, y + 4, 36, 36);
                Theme.Label(grid, label, 18, Theme.Ink, TextAnchor.MiddleLeft).Place(x + 44, y, 290, 50).Fit(14);
                var it = item;
                var b = Theme.Button(grid, $"{item.price} cr", () => { if (K.Buy(C, it)) Changed(); }, null, 18, "btn-small");
                b.name = "Comprar:" + (item.part ?? item.frame ?? item.blueprint);
                b.SetEnabled(K.CanBuy(C, item));
                b.EnableInClassList("btn-good", K.CanBuy(C, item));
                b.Place(x + 340, y + 4, 120, 38);
            }

            // Sell
            var sell = Theme.Plate(root, "Sell");
            sell.Place(1120, 120, 760, 846);
            Theme.Title(sell, "VENDER", 24).At(24, 14);
            Theme.Label(sell, "Peças em uso nos seus 3 robôs não podem ser vendidas.", 16, Theme.TextDim).Place(24, 48, 700, 24);
            var list = Theme.Scroll(sell, "Parts").Place(12, 80, 736, 754);
            foreach (var id in K.owned.Distinct().OrderBy(id => C.Part(id).slot))
            {
                var row = Theme.Row(list, "Sell:" + id, 10);
                row.AddToClassList("list-row");
                var pd = C.Part(id);
                SlotIcons.Icon(row, pd.slot == "Braco" ? "BracoDir" : pd.slot, 32, Theme.Maker(pd.maker));
                var nm = Theme.Label(row, $"{pd.name} <size=15><color=#9AA2BC>x{K.owned.Count(o => o == id)}</color></size>", 18, Theme.Ink, TextAnchor.MiddleLeft);
                nm.style.flexGrow = 1;
                var pid = id;
                var b = Theme.Button(row, $"VENDER {K.SellPrice(C, id)} cr", () => { if (K.Sell(C, pid)) Changed(); }, null, 16, "btn-small");
                b.name = "Vender:" + id;
                b.SetEnabled(K.CanSell(id));
                b.EnableInClassList("btn-good", K.CanSell(id));
                b.Size(200, 40);
                if (!K.CanSell(id)) Theme.Hover(b, "Em uso num dos seus robôs.");
            }
        }

        // ---- Fosso: ranking ladder up to the champion. Losing costs only the ranking ----
        void Ladder(string ladder)
        {
            bool circuit = ladder == "circuit";
            var stages = Career.Ladder(C, ladder);
            bool champ = K.LadderTop(C, ladder);
            string title = circuit ? "Circuito Regional" : "Fosso";
            Theme.Label(root, champ ? $"Você é o campeão do {title}. Defenda o título quando quiser." : $"Ranking do {title} · posição {stages.Length - K.RankIn(ladder) + 1} · perder só custa a posição",
                Theme.Small, champ ? Theme.Good : Theme.TextDim).Place(62, 80, 1000, 30);
            Track(root, stages, K.RankIn(ladder), 60, 120, circuit ? "LAJE" : "DEGRAU");

            var st = K.LadderStage(C, ladder);
            var last = K.LastLadder(ladder);
            var talk = Theme.Plate(root, "Announcer");
            talk.Place(60, 316, 1000, 190);
            var text = (string.IsNullOrEmpty(last) ? "" : $"<color=#{Theme.HexOf(Theme.Warn)}>{last}</color>\n")
                       + (champ ? "<b>Locutor:</b> <i>O campeão está na casa! Quem sobe pra tirar o cinturão?</i>" : $"<b>Locutor:</b> <i>{st.text}</i>");
            Portraits.Create(talk, "Locutor", 150).Place(20, 20, 150, 150);
            Theme.Label(Theme.Scroll(talk, "Said").Place(190, 18, 792, 160), text, 22, Theme.Ink).Classes("prose");
            Status(60, 524);

            var dossier = Theme.El(root, "Dossier").Place(1120, 110, 760, 760);
            OpponentScreen.Dossier(C, dossier, C.Robot(st.enemy), Career.ArenaFor(st, Career.ArenaOf(ladder)));

            Theme.Button(root, "GARAGEM", app.ShowGarage, null, 26).Place(1120, 970, 220, 84);
            var errors = K.Validate(C);
            var fight = Theme.Button(root, champ ? "DEFENDER TÍTULO" : "LUTAR", () => app.StartLadderFight(ladder), null, champ ? 28 : 34, "btn-primary");
            fight.name = "Lutar";
            fight.Place(1520, 970, 360, 84);
            fight.SetEnabled(errors.Count == 0 && K.offer.Length == 0);
            if (K.offer.Length > 0) Theme.Label(root, "Escolha a recompensa do trabalho antes", Theme.Small, Theme.Bad, TextAnchor.MiddleRight).Place(1120, 880, 760, 40);
            else if (errors.Count > 0) Theme.Label(root, "Robô inválido: ajuste na garagem", Theme.Small, Theme.Bad, TextAnchor.MiddleRight).Place(1120, 880, 760, 40);
        }

        // ---- After a win: pick 1 of 2 parts from the beaten robot ----
        void Reward()
        {
            var shade = Theme.El(root, "Reward", "shade").Fill();
            shade.pickingMode = PickingMode.Position;
            Theme.Title(shade, "ESCOLHA UMA PEÇA", 56, Theme.Good, TextAnchor.MiddleCenter).Classes("outlined").Center(960, 170, 1400, 80);
            Theme.Label(shade, K.lastResult + "\nDo que sobrou do adversário, você pode levar uma peça.", 24, Theme.Ink, TextAnchor.MiddleCenter).Center(960, 250, 1400, 80);
            for (int i = 0; i < K.offer.Length; i++)
            {
                var part = C.Part(K.offer[i]);
                var id = part.id;
                float cx = K.offer.Length == 1 ? 960 : 700 + i * 520;
                var card = Theme.Plate(shade, "Offer:" + id, "reward-card").Center(cx, 560, 460, 420);
                Theme.Border(card, Theme.Maker(part.maker), 3);
                Fx.Enter(card, i + 1, 40);
                SlotIcons.Icon(card, part.slot == "Braco" ? "BracoDir" : part.slot, 56, Theme.Maker(part.maker)).Place(24, 24, 56, 56);
                Theme.Label(card, part.name, 30, Theme.Ink, style: FontStyle.Bold).Place(96, 20, 340, 40).OneLine();
                Theme.Label(card, part.maker ?? "", 20, Theme.Maker(part.maker)).Place(96, 60, 340, 28);
                string stats = part.slot == "Nucleo" ? $"{part.energy} energia/turno · Peso {part.weight}" : $"Integridade {part.integrity} · Peso {part.weight}";
                string cards = string.Join("\n", part.cards.GroupBy(c => c).Select(g => $"{g.Count()}x {C.Card(g.Key).name}"));
                string copy = K.owned.Contains(id) ? $"\n\n<color=#{Theme.HexOf(Theme.Warn)}>Você já tem uma: a segunda vai no outro braço.</color>" : "";
                Theme.Label(Theme.Scroll(card, "Info").Place(24, 104, 418, 220), stats + "\n\n" + cards + copy, 22, Theme.Ink).Classes("prose");
                var take = Theme.Button(card, "PEGAR", () => { K.Choose(id); Changed(); }, null, 28, "btn-good");
                take.name = "Pegar:" + id;
                take.Place(24, 336, 412, 64);
            }
            var skip = Theme.Button(shade, K.offer.Length == 0 ? "CONTINUAR" : "DISPENSAR", () => { K.SkipReward(); Changed(); }, null, 24);
            skip.name = "Dispensar";
            skip.Center(960, 870, 300, 70);
        }

        // ---- Job finished ----
        void Done()
        {
            var job = C.Job(K.job);
            var panel = Theme.Plate(root, "Done");
            panel.Place(60, 120, 1000, 820);
            Theme.Title(panel, "TRABALHO CONCLUÍDO", 38, Theme.Good).At(36, 26);
            var outro = Theme.Scroll(panel, "Outro").Place(36, 100, 928, 380);
            Theme.Label(outro, $"{K.lastResult}\n\n<b>{job.fixer}:</b> <i>{job.outro}</i>", 24, Theme.Ink).Classes("prose");
            Track(panel, job.stages, job.stages.Length, 36, 500);
            var end = Theme.Button(panel, "ENCERRAR", () => { K.EndJob(); K.lastResult = $"Trabalho concluído: {job.name}." + (string.IsNullOrEmpty(job.blueprint) ? "" : $" Você ganhou a {C.Blueprint(job.blueprint).name}; fabrique na Oficina."); app.SaveCareer(); app.ShowMap(); }, null, 30, "btn-primary");
            end.name = "Encerrar";
            end.Place(36, 720, 360, 80);
            Status(1120, 120, false);
        }
    }
}
