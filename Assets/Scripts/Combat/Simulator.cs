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
        public string[] policies = { "greedy", "random" };
        public RobotDef[] builds = new RobotDef[0];
        public string[] enemies = new string[0];
    }

    public class CardStat { public long drawn, played, scrapped, damage, absorbed, armor; }

    public class MatchupResult
    {
        public string build, enemy, policy, makers;
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

        public static MatchupResult Run(Content content, RobotDef build, string enemyId, string policy, int matches, int seed, string jsonlDir = null)
        {
            var r = new MatchupResult { build = build.name, enemy = content.Robot(enemyId).name, policy = policy, makers = Content.MakerNames(content.ActiveMakers(build)) };
            foreach (var c in content.Deck(build)) Stat(r.cards, c.name);
            for (int i = 0; i < matches; i++)
            {
                var b = new Battle(content, seed + i, build, enemyId);
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

        public static List<MatchupResult> RunAll(Content content, SimConfig cfg, Action<string, float> progress = null, string jsonlDir = null)
        {
            var builds = cfg.builds.Length > 0 ? cfg.builds : content.presets;
            var enemies = cfg.enemies.Length > 0 ? cfg.enemies : content.robots.Where(r => r.id.StartsWith("enemy")).Select(r => r.id).ToArray();
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
            int total = builds.Length * enemies.Length * cfg.policies.Length, done = 0;
            foreach (var b in builds)
                foreach (var e in enemies)
                    foreach (var pol in cfg.policies)
                    {
                        progress?.Invoke($"{b.name} vs {e} ({pol})", (float)done++ / total);
                        results.Add(Run(content, b, e, pol, cfg.matches, cfg.seed, cfg.jsonl ? jsonlDir : null));
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
            File.WriteAllText(Path.Combine(dir, "report.md"), Markdown(results, cfg, DateTime.Now - t0), utf8);
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
            var s = new StringBuilder("build;enemy;policy;bonus;matches;win_rate;wins;losses;draws;avg_turns;median_turns;remaining_integrity_wins;dealt_per_match;taken_per_match;energy_gen;energy_used;energy_wasted\n");
            foreach (var r in rs)
                s.AppendLine(string.Join(";", Csv(r.build), Csv(r.enemy), r.policy, Csv(r.makers), r.matches, F(r.WinRate * 100), r.wins, r.losses, r.draws,
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

        public static string Markdown(List<MatchupResult> rs, SimConfig cfg, TimeSpan took)
        {
            var s = new StringBuilder();
            s.AppendLine("# Simulação de balanceamento");
            s.AppendLine();
            s.AppendLine($"{cfg.matches} lutas por confronto, seeds {cfg.seed}–{cfg.seed + cfg.matches - 1}, políticas: {string.Join(", ", cfg.policies)}. " +
                         $"Limite de {TurnLimit} turnos (acima disso conta como empate). Tempo: {took.TotalSeconds:0.0}s.");
            s.AppendLine();
            s.AppendLine("## Confrontos");
            s.AppendLine();
            s.AppendLine("| Build | Inimigo | Política | Vitórias | Turnos (méd/med) | Integridade restante (vitórias) | Dano causado/recebido por luta | Energia gerada/usada/desperdiçada por luta | Bônus |");
            s.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (var r in rs)
                s.AppendLine($"| {r.build} | {r.enemy} | {r.policy} | {Pct(r.WinRate)} | {F(r.AvgTurns)} / {F(r.MedianTurns)} | " +
                             $"{F(r.wins == 0 ? 0 : (float)r.remainingWins / r.wins)} | {F(r.Per(r.dealt))} / {F(r.Per(r.taken))} | " +
                             $"{F(r.Per(r.energyGen))} / {F(r.Per(r.energyUsed))} / {F(r.Per(r.energyWasted))} | " +
                             $"{r.makers}{(r.bonus.Count > 0 ? " (" + string.Join(", ", r.bonus.Select(kv => $"{kv.Key} {F(r.Per(kv.Value))}/luta")) + ")" : "")} |");

            var greedy = rs.Where(r => r.policy == "greedy").ToList();
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
            var vsRandom = rs.GroupBy(r => (r.build, r.enemy)).Where(g => g.Count() == 2)
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
