using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CardBattler.Combat
{
    // Writes every BattleEvent of one match as JSON Lines (battle_*.jsonl) plus a readable summary (battle_*_resumo.txt).
    public sealed class BattleLogger : IDisposable
    {
        public static bool MirrorToConsole;
        public readonly string jsonlPath, summaryPath;
        readonly Battle battle;
        StreamWriter writer;

        public BattleLogger(Battle battle, string dir)
        {
            this.battle = battle;
            Directory.CreateDirectory(dir);
            string name = $"battle_{DateTime.Now:yyyyMMdd_HHmmss}_{battle.seed}";
            jsonlPath = Path.Combine(dir, name + ".jsonl");
            summaryPath = Path.Combine(dir, name + "_resumo.txt");
            writer = new StreamWriter(jsonlPath, false, new UTF8Encoding(false)) { AutoFlush = true };
            foreach (var e in battle.history) Write(e); // events emitted by the Battle constructor
            battle.Logged += Write;
        }

        void Write(BattleEvent e)
        {
            writer?.WriteLine(JsonUtility.ToJson(e));
            if (MirrorToConsole) Debug.Log($"[Batalha T{e.turn} {e.actor}] {e.msg}");
            if (e.evt == "battle_end") Dispose();
        }

        public void Dispose()
        {
            if (writer == null) return;
            battle.Logged -= Write;
            writer.Dispose();
            writer = null;
            File.WriteAllText(summaryPath, Summary(battle), new UTF8Encoding(false));
            if (MirrorToConsole) Debug.Log("Log da batalha salvo em " + jsonlPath);
        }

        public static string Summary(Battle b)
        {
            var h = b.history;
            var s = new StringBuilder();
            string res = b.result == Result.Win ? "Vitória" : b.result == Result.Lose ? "Derrota" : "Interrompida";
            s.AppendLine($"Resultado: {res}   Turnos: {b.turn}   Seed: {b.seed}");
            foreach (var r in new[] { b.player, b.enemy })
            {
                s.AppendLine($"  {r.name}: frame {r.frame.name}, peso {r.weight}/{r.frame.carry}, bônus {Content.MakerNames(r.makers)}, partes " +
                    string.Join(", ", r.parts.Values.Select(p => p.def.name)) + $", núcleo {r.core.name}");
                s.AppendLine($"  {r.name}: chassi {r.chassis}/{r.frame.chassis}, " +
                    string.Join(", ", r.parts.Values.Select(p => $"{Battle.SlotName(p.slot)} {p.hp}/{p.Max}")));
            }

            foreach (var actor in new[] { "player", "enemy" })
            {
                var mine = h.Where(e => e.actor == actor).ToList();
                s.AppendLine();
                s.AppendLine(actor == "player" ? "== Jogador ==" : "== Inimigo ==");
                int generated = mine.Where(e => e.evt == "turn_start").Sum(e => e.value) + mine.Where(e => e.evt == "scrap" || e.evt == "chain_energy").Sum(e => e.value);
                int used = mine.Where(e => e.evt == "play").Sum(e => e.cost);
                int wasted = mine.Where(e => e.evt == "turn_end").Sum(e => e.value);
                s.AppendLine($"Energia: gerada {generated} (sucata {mine.Count(e => e.evt == "scrap")}), usada {used}, desperdiçada {wasted}");
                s.AppendLine("Dano por carta (vezes jogada / dano na integridade / na blindagem / esquivado):");
                foreach (var g in mine.Where(e => e.evt == "play").GroupBy(e => e.card).OrderByDescending(g => g.Count()))
                {
                    var atk = mine.Where(e => e.evt == "attack" && e.card == g.Key).ToList();
                    s.AppendLine($"  {g.Key}: {g.Count()}x / {atk.Sum(e => e.value)} / {atk.Sum(e => e.absorbed)} / {atk.Sum(e => e.dodged)}" +
                        (atk.Any(e => e.chained) ? $"  (Encadear {atk.Count(e => e.chained)}x)" : ""));
                }
                var deck = mine.FirstOrDefault(e => e.evt == "battle_start")?.cards ?? new string[0];
                var played = mine.Where(e => e.evt == "play").Select(e => e.card).ToHashSet();
                var never = deck.Distinct().Where(c => !played.Contains(c)).ToList();
                s.AppendLine("Cartas nunca jogadas: " + (never.Count == 0 ? "nenhuma" : string.Join(", ", never)));
                s.AppendLine("Partes avariadas: " + string.Join(", ", mine.Where(e => e.evt == "part_broken").Select(e => $"{e.target} (T{e.turn})").DefaultIfEmpty("nenhuma")));
            }
            return s.ToString();
        }
    }
}
