using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CardBattler.Combat
{
    // Optional matchup file (JSON). Empty builds/enemies = content.presets / every robot whose id starts with "enemy".
    [Serializable]
    public class SimConfig
    {
        public int matches = 500, seed = 1;
        public bool jsonl; // also write one battle_*.jsonl per match (slow)
        public bool strategyImpact = true; // also run greedy vs each enemy with its strategies off
        public string[] policies = { "greedy", "random" };
        public RobotDef[] builds = new RobotDef[0];
        public string[] enemies = new string[0];
    }

    public class JobResult
    {
        public string job;
        public int runs, completed;
        public int[] lostAt = new int[0]; // runs that ended at each stage
        public float CompletionRate => runs == 0 ? 0 : (float)completed / runs;
    }

    public class CardStat { public long drawn, played, scrapped, damage, absorbed, armor; }

    public class MatchupResult
    {
        public string build, enemy, policy, makers, tier, enemyAI = "estrategia", strategies, buildId, enemyId;
        public int matches, wins, losses, draws;
        public readonly List<int> turns = new List<int>();
        public long remainingWins, dealt, taken, energyGen, energyUsed, energyWasted;
        public readonly Dictionary<string, CardStat> cards = new Dictionary<string, CardStat>(), enemyCards = new Dictionary<string, CardStat>();
        public readonly Dictionary<string, int> firstBrokenPlayer = new Dictionary<string, int>(), firstBrokenEnemy = new Dictionary<string, int>();
        public readonly Dictionary<string, long> bonus = new Dictionary<string, long>();
        public float WinRate => matches == 0 ? 0 : (float)wins / matches;
        public float AvgTurns => turns.Count == 0 ? 0 : (float)turns.Average();
        public float MedianTurns => turns.Count == 0 ? 0 : turns.OrderBy(t => t).ElementAt(turns.Count / 2);
        public float Per(long total) => matches == 0 ? 0 : (float)total / matches;
    }

    public static class Simulator
    {
        public const int TurnLimit = 50;

        // Win-rate targets for the greedy player, per category (from the design brief).
        public static readonly Dictionary<string, (float min, float max)> Targets = new Dictionary<string, (float, float)>
        {
            ["Fácil"] = (0.80f, 1f), ["Moderado"] = (0.65f, 0.75f), ["Difícil"] = (0.55f, 0.60f), ["Chefe"] = (0.55f, 0.60f),
        };
        public const float MinDecisionGap = 0.25f; // greedy must beat random by this much on Difícil/Chefe

        public static MatchupResult Run(Content content, RobotDef build, string enemyId, string policy, int matches, int seed, string jsonlDir = null, bool basicEnemy = false)
        {
            var ed = content.Robot(enemyId);
            var r = new MatchupResult { build = build.name, buildId = build.id, enemyId = enemyId, enemy = ed.name, policy = policy, makers = Content.MakerNames(content.ActiveMakers(build)),
                tier = string.IsNullOrEmpty(ed.tier) ? "-" : ed.tier, enemyAI = basicEnemy ? "basico" : "estrategia",
                strategies = ed.strategies == null || ed.strategies.Length == 0 ? "nenhuma" : string.Join(", ", ed.strategies) };
            foreach (var c in content.Deck(build)) Stat(r.cards, c.name);
            for (int i = 0; i < matches; i++)
            {
                var b = new Battle(content, seed + i, build, enemyId, basicEnemy);
                var logger = jsonlDir == null ? null : new BattleLogger(b, jsonlDir);
                var rng = new System.Random(seed + i);
                while (b.result == Result.None && b.turn < TurnLimit)
                {
                    if (policy == "random") Bot.PlayTurnRandom(b, rng); else Bot.PlayTurn(b);
                    b.EndPlayerTurn();
                }
                logger?.Dispose();
                Aggregate(r, b);
            }
            return r;
        }

        static CardStat Stat(Dictionary<string, CardStat> d, string card) => d.TryGetValue(card, out var s) ? s : d[card] = new CardStat();

        public static void Aggregate(MatchupResult r, Battle b)
        {
            r.matches++;
            if (b.result == Result.Win) r.wins++; else if (b.result == Result.Lose) r.losses++; else r.draws++;
            r.turns.Add(b.turn);
            if (b.result == Result.Win) r.remainingWins += b.player.chassis + b.player.parts.Values.Sum(p => Math.Max(0, p.hp));
            bool firstP = true, firstE = true;
            foreach (var e in b.history)
            {
                var cards = e.actor == "player" ? r.cards : r.enemyCards;
                switch (e.evt)
                {
                    case "draw": foreach (var c in e.cards) Stat(cards, c).drawn++; break;
                    case "play": Stat(cards, e.card).played++; if (e.actor == "player") r.energyUsed += e.cost; break;
                    case "scrap": Stat(cards, e.card).scrapped++; if (e.actor == "player") r.energyGen += e.value; break;
                    case "attack": var s = Stat(cards, e.card); s.damage += e.value; s.absorbed += e.absorbed; break;
                    case "armor": if (!string.IsNullOrEmpty(e.card)) Stat(cards, e.card).armor += e.value; break;
                    case "damage": if (e.actor == "player") r.taken += e.value; else r.dealt += e.value; break;
                    case "turn_start": if (e.actor == "player") r.energyGen += e.value; break;
                    case "chain_energy": if (e.actor == "player") r.energyGen += e.value; break;
                    case "turn_end": if (e.actor == "player") r.energyWasted += e.value; break;
                    case "bonus": if (e.actor == "player") r.bonus[e.target] = (r.bonus.TryGetValue(e.target, out var v) ? v : 0) + e.value; break;
                    case "part_broken":
                        if (e.actor == "player" && firstP) { firstP = false; Inc(r.firstBrokenPlayer, e.target); }
                        if (e.actor == "enemy" && firstE) { firstE = false; Inc(r.firstBrokenEnemy, e.target); }
                        break;
                }
            }
        }

        static void Inc(Dictionary<string, int> d, string k) => d[k] = (d.TryGetValue(k, out var v) ? v : 0) + 1;

        // Plays a whole job with a fresh career: damage carries over, the bot spends scrap on repairs (most damaged
        // equipped part first, then the chassis), pays a full repair with credits when it can, and takes the first part offered.
        // shortcut: the bot never rebuilds with new parts, so rewards don't help it.
        public static JobResult RunJob(Content content, string jobId, int runs, int seed)
        {
            var job = content.Job(jobId);
            var r = new JobResult { job = job.name, runs = runs, lostAt = new int[job.stages.Length] };
            for (int i = 0; i < runs; i++)
            {
                var rng = new System.Random(seed + i);
                var s = Career.New(content);
                s.StartJob(content, jobId);
                while (s.InJob && !s.JobDone(content))
                {
                    if (!s.RepairAllWithCredits(content))
                        while (s.scrap > 0)
                        {
                            var hurt = s.Build.PartIds.Where(id => s.HpOf(content, id) < content.Part(id).integrity).OrderBy(id => s.HpOf(content, id)).FirstOrDefault();
                            if (hurt == null && s.chassisDamage == 0) break;
                            if (!s.RepairWithScrap(content, hurt)) break;
                        }
                    int stage = s.stage;
                    var b = s.StartBattle(content, seed + i * 31 + stage, s.Stage(content).enemy);
                    while (b.result == Result.None && b.turn < TurnLimit) { Bot.PlayTurn(b); b.EndPlayerTurn(); }
                    s.FinishJobFight(content, b, rng);
                    if (!s.InJob) r.lostAt[stage]++;
                    else if (s.offer.Length > 0) s.Choose(s.offer[0]);
                }
                if (s.InJob && s.JobDone(content)) r.completed++;
            }
            return r;
        }

        public static List<MatchupResult> RunAll(Content content, SimConfig cfg, Action<string, float> progress = null, string jsonlDir = null)
        {
            var builds = cfg.builds.Length > 0 ? cfg.builds : content.presets;
            var enemies = cfg.enemies.Length > 0 ? cfg.enemies : content.robots.Where(r => !string.IsNullOrEmpty(r.tier)).Select(r => r.id).ToArray();
            if (cfg.matches < 1 || cfg.matches > 100000) throw new ArgumentException("matches deve estar entre 1 e 100000.");
            var badPolicy = cfg.policies.FirstOrDefault(p => p != "greedy" && p != "random");
            if (badPolicy != null) throw new ArgumentException($"Política desconhecida: {badPolicy} (use greedy ou random).");
            foreach (var e in enemies) content.Robot(e); // throws on unknown enemy id
            foreach (var b in builds)
            {
                var errors = content.Validate(b);
                if (errors.Count > 0) throw new ArgumentException($"Build '{b.name}' inválida: {string.Join(" ", errors)}");
            }
            var results = new List<MatchupResult>();
            var runs = cfg.policies.Select(pol => (pol, basic: false)).ToList();
            if (cfg.strategyImpact && cfg.policies.Contains("greedy")) runs.Add(("greedy", true));
            int total = builds.Length * enemies.Length * runs.Count, done = 0;
            foreach (var b in builds)
                foreach (var e in enemies)
                    foreach (var (pol, basic) in runs)
                    {
                        progress?.Invoke($"{b.name} vs {e} ({pol}{(basic ? ", IA básica" : "")})", (float)done++ / total);
                        results.Add(Run(content, b, e, pol, cfg.matches, cfg.seed, cfg.jsonl ? jsonlDir : null, basic));
                    }
            return results;
        }

        // Runs everything and writes report.md + matchups.csv + cards.csv into <rootDir>/<timestamp>/. Returns that folder.
        public static string RunAndSave(Content content, SimConfig cfg, string rootDir, Action<string, float> progress = null)
        {
            string dir = Path.GetFullPath(Path.Combine(rootDir, DateTime.Now.ToString("yyyyMMdd_HHmmss")));
            Directory.CreateDirectory(dir);
            var t0 = DateTime.Now;
            var results = RunAll(content, cfg, progress, Path.Combine(dir, "battles"));
            var utf8 = new UTF8Encoding(false);
            var md = Markdown(results, cfg, DateTime.Now - t0, content);
            var jobs = new StringBuilder("\n## Trabalhos (sequência de lutas com dano acumulado)\n\n| Trabalho | Concluídos | Onde as tentativas terminaram |\n|---|---|---|\n");
            foreach (var j in content.jobs)
            {
                var jr = RunJob(content, j.id, cfg.matches, cfg.seed);
                jobs.AppendLine($"| {jr.job} | {Pct(jr.CompletionRate)} | " + string.Join(", ", j.stages.Select((st, k) => $"{k + 1}. {content.Robot(st.enemy).name}: {Pct((float)jr.lostAt[k] / jr.runs)}")) + " |");
            }
            File.WriteAllText(Path.Combine(dir, "report.md"), md + jobs, utf8);
            File.WriteAllText(Path.Combine(dir, "matchups.csv"), MatchupCsv(results), utf8);
            File.WriteAllText(Path.Combine(dir, "cards.csv"), CardCsv(results), utf8);
            File.WriteAllText(Path.Combine(dir, "config.json"), JsonUtility.ToJson(cfg, true), utf8);
            return dir;
        }

        static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        static string Pct(float v) => (v * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

        public static string MatchupCsv(List<MatchupResult> rs)
        {
            var s = new StringBuilder("build;enemy;tier;enemy_ai;policy;bonus;matches;win_rate;wins;losses;draws;avg_turns;median_turns;remaining_integrity_wins;dealt_per_match;taken_per_match;energy_gen;energy_used;energy_wasted\n");
            foreach (var r in rs)
                s.AppendLine(string.Join(";", Csv(r.build), Csv(r.enemy), Csv(r.tier), r.enemyAI, r.policy, Csv(r.makers), r.matches, F(r.WinRate * 100), r.wins, r.losses, r.draws,
                    F(r.AvgTurns), F(r.MedianTurns), F(r.wins == 0 ? 0 : (float)r.remainingWins / r.wins), F(r.Per(r.dealt)), F(r.Per(r.taken)),
                    F(r.Per(r.energyGen)), F(r.Per(r.energyUsed)), F(r.Per(r.energyWasted))));
            return s.ToString();
        }

        public static string CardCsv(List<MatchupResult> rs)
        {
            var s = new StringBuilder("build;enemy;policy;actor;card;drawn_per_match;played_per_match;scrapped_per_match;damage_per_match;absorbed_per_match;armor_per_match;damage_per_play\n");
            foreach (var r in rs)
                foreach (var (actor, d) in new[] { ("player", r.cards), ("enemy", r.enemyCards) })
                    foreach (var kv in d.OrderBy(k => k.Key))
                        s.AppendLine(string.Join(";", Csv(r.build), Csv(r.enemy), r.policy, actor, Csv(kv.Key), F(r.Per(kv.Value.drawn)), F(r.Per(kv.Value.played)),
                            F(r.Per(kv.Value.scrapped)), F(r.Per(kv.Value.damage)), F(r.Per(kv.Value.absorbed)), F(r.Per(kv.Value.armor)),
                            F(kv.Value.played == 0 ? 0 : (float)kv.Value.damage / kv.Value.played)));
            return s.ToString();
        }

        public static string Markdown(List<MatchupResult> rs, SimConfig cfg, TimeSpan took, Content content = null)
        {
            var s = new StringBuilder();
            s.AppendLine("# Simulação de balanceamento");
            s.AppendLine();
            s.AppendLine($"{cfg.matches} lutas por confronto, seeds {cfg.seed}–{cfg.seed + cfg.matches - 1}, políticas: {string.Join(", ", cfg.policies)}. " +
                         $"Limite de {TurnLimit} turnos (acima disso conta como empate). Tempo: {took.TotalSeconds:0.0}s.");
            s.AppendLine();
            AppendCategories(s, rs);
            if (content != null) AppendDossierCheck(s, rs, content);
            s.AppendLine("## Confrontos");
            s.AppendLine();
            s.AppendLine("| Build | Inimigo | Política | IA inimiga | Vitórias | Turnos (méd/med) | Integridade restante (vitórias) | Dano causado/recebido por luta | Energia gerada/usada/desperdiçada por luta | Bônus |");
            s.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in rs)
                s.AppendLine($"| {r.build} | {r.enemy} | {r.policy} | {r.enemyAI} | {Pct(r.WinRate)} | {F(r.AvgTurns)} / {F(r.MedianTurns)} | " +
                             $"{F(r.wins == 0 ? 0 : (float)r.remainingWins / r.wins)} | {F(r.Per(r.dealt))} / {F(r.Per(r.taken))} | " +
                             $"{F(r.Per(r.energyGen))} / {F(r.Per(r.energyUsed))} / {F(r.Per(r.energyWasted))} | " +
                             $"{r.makers}{(r.bonus.Count > 0 ? " (" + string.Join(", ", r.bonus.Select(kv => $"{kv.Key} {F(r.Per(kv.Value))}/luta")) + ")" : "")} |");

            var greedy = rs.Where(r => r.policy == "greedy" && r.enemyAI == "estrategia").ToList();
            s.AppendLine();
            s.AppendLine("## Destaques (política greedy)");
            s.AppendLine();
            foreach (var r in greedy.Where(r => r.WinRate >= 0.8f || r.WinRate <= 0.2f))
                s.AppendLine($"- {(r.WinRate >= 0.8f ? "Fácil demais" : "Difícil demais")}: {r.build} vs {r.enemy} ({Pct(r.WinRate)}).");
            var allCards = greedy.SelectMany(r => r.cards.Select(kv => (r, card: kv.Key, st: kv.Value))).Where(x => x.st.played > 0)
                .GroupBy(x => x.card).Select(g => (card: g.Key, dpp: (float)g.Sum(x => x.st.damage) / g.Sum(x => x.st.played))).Where(x => x.dpp > 0)
                .OrderByDescending(x => x.dpp).ToList();
            if (allCards.Count > 0)
                s.AppendLine("- Mais dano por jogada: " + string.Join(", ", allCards.Take(5).Select(x => $"{x.card} ({F(x.dpp)})")) + ".");
            // Summed over every enemy, per build.
            var unused = greedy.SelectMany(r => r.cards.Select(kv => (r.build, card: kv.Key, st: kv.Value)))
                .GroupBy(x => (x.build, x.card)).Select(g => (g.Key, drawn: g.Sum(x => x.st.drawn), played: g.Sum(x => x.st.played)))
                .Where(x => x.drawn > 0 && (float)x.played / x.drawn < 0.15f)
                .Select(x => $"{x.Key.card} em {x.Key.build} ({F(100f * x.played / x.drawn)}%)").ToList();
            s.AppendLine("- Cartas quase nunca jogadas (<15% das vezes que foram compradas): " + (unused.Count == 0 ? "nenhuma" : string.Join("; ", unused)) + ".");
            var vsRandom = rs.Where(r => r.enemyAI == "estrategia").GroupBy(r => (r.build, r.enemy)).Where(g => g.Count() == 2)
                .Select(g => (g.Key, d: g.First(x => x.policy == "greedy").WinRate - g.First(x => x.policy == "random").WinRate)).ToList();
            if (vsRandom.Count > 0) s.AppendLine($"- Greedy vence {Pct(vsRandom.Average(x => x.d))} a mais que jogadas aleatórias, em média (quanto maior, mais as decisões importam).");

            foreach (var r in greedy)
            {
                s.AppendLine();
                s.AppendLine($"## {r.build} vs {r.enemy} (greedy, {Pct(r.WinRate)})");
                s.AppendLine();
                s.AppendLine("Primeira parte avariada — jogador: " + Top(r.firstBrokenPlayer, r.matches) + "; inimigo: " + Top(r.firstBrokenEnemy, r.matches) + ".");
                s.AppendLine();
                s.AppendLine("| Carta | Compras | Jogadas | Sucata | Dano | Na blindagem | Blindagem ganha | Dano por jogada |");
                s.AppendLine("|---|---|---|---|---|---|---|---|");
                foreach (var kv in r.cards.OrderByDescending(k => k.Value.played))
                {
                    var c = kv.Value;
                    s.AppendLine($"| {kv.Key} | {F(r.Per(c.drawn))} | {F(r.Per(c.played))} | {F(r.Per(c.scrapped))} | {F(r.Per(c.damage))} | {F(r.Per(c.absorbed))} | {F(r.Per(c.armor))} | {F(c.played == 0 ? 0 : (float)c.damage / c.played)} |");
                }
            }
            s.AppendLine();
            s.AppendLine("Valores de cartas são médias por luta. Detalhe completo em matchups.csv e cards.csv (separador ;).");
            return s.ToString();
        }

        static float Avg(IEnumerable<MatchupResult> rs) => rs.Any() ? rs.Average(r => r.WinRate) : float.NaN;

        static string Status(string tier, float win)
        {
            if (!Targets.TryGetValue(tier, out var t) || float.IsNaN(win)) return "-";
            return win < t.min ? "⚠ abaixo da meta" : win > t.max ? "⚠ acima da meta" : "✔ na meta";
        }

        public const float BuildTolerance = 0.10f;

        // A build's win rate in a category, flagged when it is more than BuildTolerance away from the target range.
        public static bool BuildInBand(string tier, float win) =>
            !Targets.TryGetValue(tier, out var t) || (t.max >= 1 ? win >= t.min - BuildTolerance : Math.Abs(win - (t.min + t.max) / 2) <= BuildTolerance);

        static string BuildCell(string tier, float win) => Pct(win) + (BuildInBand(tier, win) ? "" : " ⚠");

        static string Target(string tier) => Targets.TryGetValue(tier, out var t) ? (t.max >= 1 ? $"> {Pct(t.min)}" : $"{Pct(t.min)}–{Pct(t.max)}") : "-";

        // Category summary (greedy player, average over builds), decision gap and per-enemy strategy impact.
        public static void AppendCategories(StringBuilder s, List<MatchupResult> rs)
        {
            var strat = rs.Where(r => r.enemyAI == "estrategia").ToList();
            s.AppendLine("## Categorias (média das builds de referência)");
            s.AppendLine();
            s.AppendLine("| Categoria | Meta (greedy) | Greedy | Aleatório | Diferença | Situação |");
            s.AppendLine("|---|---|---|---|---|---|");
            foreach (var tier in RobotDef.Tiers.Where(t => strat.Any(r => r.tier == t)))
            {
                float g = Avg(strat.Where(r => r.tier == tier && r.policy == "greedy")), rnd = Avg(strat.Where(r => r.tier == tier && r.policy == "random"));
                string gap = float.IsNaN(rnd) ? "-" : Pct(g - rnd);
                string status = Status(tier, g);
                if ((tier == "Difícil" || tier == "Chefe") && !float.IsNaN(rnd) && g - rnd < MinDecisionGap) status += $", ⚠ diferença < {Pct(MinDecisionGap)}";
                s.AppendLine($"| {tier} | {Target(tier)} | {Pct(g)} | {(float.IsNaN(rnd) ? "-" : Pct(rnd))} | {gap} | {status} |");
            }
            s.AppendLine();
            s.AppendLine("### Por build (greedy)");
            s.AppendLine();
            var tiers = RobotDef.Tiers.Where(t => strat.Any(r => r.tier == t)).ToList();
            s.AppendLine("| Build | " + string.Join(" | ", tiers) + " |");
            s.AppendLine("|---|" + string.Concat(tiers.Select(_ => "---|")));
            foreach (var g in strat.Where(r => r.policy == "greedy").GroupBy(r => r.build))
                s.AppendLine($"| {g.Key} | " + string.Join(" | ", tiers.Select(t => BuildCell(t, Avg(g.Where(r => r.tier == t))))) + " |");
            s.AppendLine();
            s.AppendLine($"⚠ = build a mais de {Pct(BuildTolerance)} da meta da categoria. A meta vale para a média; builds fora da faixa indicam desequilíbrio entre builds, não entre inimigos.");
            s.AppendLine();
            s.AppendLine("## Inimigos");
            s.AppendLine();
            s.AppendLine("| Inimigo | Categoria | Estratégias | Greedy | Aleatório | Greedy vs IA básica | Impacto das estratégias | Situação |");
            s.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var g in rs.GroupBy(r => r.enemy))
            {
                var first = g.First();
                float greedy = Avg(g.Where(r => r.policy == "greedy" && r.enemyAI == "estrategia"));
                float rnd = Avg(g.Where(r => r.policy == "random" && r.enemyAI == "estrategia"));
                float basic = Avg(g.Where(r => r.enemyAI == "basico"));
                s.AppendLine($"| {first.enemy} | {first.tier} | {first.strategies} | {Pct(greedy)} | {(float.IsNaN(rnd) ? "-" : Pct(rnd))} | " +
                             $"{(float.IsNaN(basic) ? "-" : Pct(basic))} | {(float.IsNaN(basic) ? "-" : "-" + Pct(basic - greedy) + " de vitória")} | {Status(first.tier, greedy)} |");
            }
            s.AppendLine();
            s.AppendLine("Impacto das estratégias = quanto a vitória do jogador cai quando o inimigo usa as estratégias, comparado à IA básica (cartas aleatórias) com as mesmas partes.");
            s.AppendLine();
        }

        // Per enemy: reference builds ranked by greedy win rate, checked against the dossier's good/bad builds.
        // Returns warnings (empty = every dossier agrees with the results).
        public static List<string> DossierWarnings(List<MatchupResult> rs, Func<string, DossierDef> dossierOf, out List<string> lines)
        {
            var warnings = new List<string>();
            lines = new List<string>();
            foreach (var g in rs.Where(r => r.policy == "greedy" && r.enemyAI == "estrategia").GroupBy(r => r.enemyId))
            {
                var ranked = g.OrderByDescending(r => r.WinRate).ToList();
                int half = (ranked.Count + 1) / 2;
                var top = ranked.Take(half).Select(r => r.buildId).ToList();
                var d = dossierOf(g.Key);
                string Fmt(MatchupResult r) => $"{r.build} ({Pct(r.WinRate)})";
                lines.Add($"| {ranked[0].enemy} | {string.Join(", ", ranked.Take(2).Select(Fmt))} | {string.Join(", ", ranked.Skip(ranked.Count - 2).Select(Fmt))} | " +
                          (d == null || !d.Exists ? "sem dossiê" : "{0}") + " |");
                if (d == null || !d.Exists) continue;
                var bad = d.goodBuilds.Where(id => !top.Contains(id)).Select(id => $"\"{id}\" está em \"funciona bem\" mas não fica na metade de cima")
                    .Concat(d.badBuilds.Where(id => top.Contains(id)).Select(id => $"\"{id}\" está em \"evite\" mas fica na metade de cima")).ToList();
                foreach (var w in bad) warnings.Add($"{ranked[0].enemy}: {w}");
                lines[lines.Count - 1] = lines[lines.Count - 1].Replace("{0}", bad.Count == 0 ? "✔ de acordo" : "⚠ " + string.Join("; ", bad));
            }
            return warnings;
        }

        static void AppendDossierCheck(StringBuilder s, List<MatchupResult> rs, Content content)
        {
            DossierWarnings(rs, id => content.Robot(id).dossier, out var lines);
            s.AppendLine("## Dossiês dos adversários");
            s.AppendLine();
            s.AppendLine("| Inimigo | Melhores builds | Piores builds | Dossiê |");
            s.AppendLine("|---|---|---|---|");
            foreach (var l in lines) s.AppendLine(l);
            s.AppendLine();
            s.AppendLine("Se aparecer ⚠, atualize o dossiê do inimigo em content.json (dicas, goodBuilds, badBuilds) para bater com os resultados.");
            s.AppendLine();
        }

        static string Top(Dictionary<string, int> d, int matches) =>
            d.Count == 0 ? "nenhuma" : string.Join(", ", d.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Pct((float)kv.Value / matches)}"));

        public static string DefaultRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Simulations"));

        // Entry point for the pipeline / script eval: Simulator.RunFromArgs(500, 1). matches 0 = use the file/default (500).
        public static string RunFromArgs(int matches = 0, int seed = 1, string matchupFile = null, bool jsonl = false)
        {
            var content = Content.Load();
            var cfg = string.IsNullOrEmpty(matchupFile) ? new SimConfig() : JsonUtility.FromJson<SimConfig>(File.ReadAllText(matchupFile));
            if (matches > 0) cfg.matches = matches;
            cfg.seed = seed;
            cfg.jsonl |= jsonl;
            return RunAndSave(content, cfg, DefaultRoot);
        }
    }
}
