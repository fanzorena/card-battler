using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CardBattler.Combat
{
    // A job ("trabalho") is a fixed sequence of fights offered by a fixer.
    [Serializable]
    public class StageDef
    {
        public string enemy, text;
        public int credits, scrap;
    }

    [Serializable]
    public class JobDef
    {
        public string id, name, fixer, intro, outro;
        public StageDef[] stages = new StageDef[0];
    }

    [Serializable]
    public class PartHp
    {
        public string part;
        public int hp;
    }

    // Progress through a job: inventory, currencies and damage that carries over between fights.
    [Serializable]
    public class RunState
    {
        public const int RepairPerScrap = 3;

        public string job;
        public int stage, credits, scrap, chassisDamage;
        public string[] owned = new string[0];   // part ids
        public string[] offer = new string[0];   // reward choice pending after a win
        public PartHp[] damaged = new PartHp[0]; // parts below full integrity
        public RobotDef build;
        public bool failed;

        public bool Done(Content c) => !failed && stage >= c.Job(job).stages.Length;
        public bool Over(Content c) => failed || Done(c);
        public StageDef Current(Content c) => c.Job(job).stages[Math.Min(stage, c.Job(job).stages.Length - 1)];

        public static RunState Start(Content c, string jobId)
        {
            var b = c.Robot("player").Clone();
            b.id = "run"; b.name = "Vira-lata (Rafa)";
            c.Job(jobId); // throws on unknown job
            return new RunState { job = jobId, build = b, owned = b.PartIds.ToArray() };
        }

        public int HpOf(Content c, string partId) => damaged.FirstOrDefault(d => d.part == partId)?.hp ?? c.Part(partId).integrity;

        // Builds may only use owned parts (frame stays Vira-lata until frames can be found).
        public List<string> Validate(Content c)
        {
            var errors = c.Validate(build);
            foreach (var id in build.PartIds.Where(id => !owned.Contains(id)).Distinct())
                errors.Add($"Você não tem a peça {c.Part(id).name}.");
            if (build.armL == build.armR && owned.Count(o => o == build.armL) < 2)
                errors.Add("Você só tem um " + c.Part(build.armL).name + ".");
            return errors;
        }

        public Battle StartBattle(Content c, int seed)
        {
            if (Over(c)) throw new InvalidOperationException("Trabalho encerrado.");
            if (offer.Length > 0) throw new InvalidOperationException("Escolha a recompensa antes da próxima luta.");
            var errors = Validate(c);
            if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
            var b = new Battle(c, seed, build.Clone(), Current(c).enemy);
            foreach (var p in b.player.parts.Values) p.hp = HpOf(c, p.def.id);
            b.player.chassis = Math.Max(1, b.player.chassisMax - chassisDamage);
            return b;
        }

        // Records the outcome: damage carries over; a win pays the stage reward and offers 2 parts of the beaten robot.
        public void Finish(Content c, Battle b, System.Random rng)
        {
            if (b.result != Result.Win) { failed = true; return; }
            var st = Current(c);
            credits += st.credits;
            scrap += st.scrap;
            var hp = b.player.parts.Values.ToDictionary(p => p.def.id, p => p.hp);
            damaged = owned.Distinct().Where(id => hp.TryGetValue(id, out var h) ? h < c.Part(id).integrity : HpOf(c, id) < c.Part(id).integrity)
                .Select(id => new PartHp { part = id, hp = hp.TryGetValue(id, out var h) ? Math.Max(0, h) : HpOf(c, id) }).ToArray();
            chassisDamage = b.player.chassisMax - b.player.chassis;
            var loot = c.Robot(st.enemy).PartIds.Where(id => !owned.Contains(id)).Distinct().OrderBy(_ => rng.Next()).Take(2).ToArray();
            offer = loot;
            stage++;
        }

        public void Choose(string partId)
        {
            if (!offer.Contains(partId)) throw new ArgumentException("Peça fora da oferta.");
            owned = owned.Append(partId).ToArray();
            offer = new string[0];
        }

        public void SkipReward() => offer = new string[0];

        // 1 sucata repairs RepairPerScrap integrity on a part (or the chassis when partId is null).
        public bool Repair(Content c, string partId)
        {
            if (scrap <= 0) return false;
            if (partId == null)
            {
                if (chassisDamage <= 0) return false;
                chassisDamage = Math.Max(0, chassisDamage - RepairPerScrap);
            }
            else
            {
                var d = damaged.FirstOrDefault(x => x.part == partId);
                if (d == null) return false;
                d.hp = Math.Min(c.Part(partId).integrity, d.hp + RepairPerScrap);
                if (d.hp >= c.Part(partId).integrity) damaged = damaged.Where(x => x != d).ToArray();
            }
            scrap--;
            return true;
        }

        public static RunState Load(string path, Content c)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var s = JsonUtility.FromJson<RunState>(File.ReadAllText(path));
                if (s == null || c.jobs.All(j => j.id != s.job) || s.build == null || s.owned.Any(id => c.parts.All(p => p.id != id))) return null;
                return s;
            }
            catch (Exception e) { Debug.LogWarning("Trabalho salvo ilegível: " + e.Message); return null; }
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }
    }
}
