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

    // The player's persistent profile: money, inventory, builds, damage carried between fights and the job in progress.
    [Serializable]
    public class Career
    {
        public const int RepairPerScrap = 3, CreditsPerPoint = 2;

        public int credits, scrap;
        public string[] owned = new string[0];   // part ids (one entry per copy)
        public string[] frames = new string[0];  // owned frame ids
        public GarageSave garage = new GarageSave();
        public PartHp[] damaged = new PartHp[0]; // parts below full integrity
        public int chassisDamage;
        public string job;                       // job in progress (null/empty = none)
        public int stage;
        public string[] offer = new string[0];   // reward choice pending after a win
        public string lastResult;                // short text about the last fight, for the job screen

        public RobotDef Build => garage.Current;
        public bool InJob => !string.IsNullOrEmpty(job);

        public static Career New(Content c)
        {
            var starter = c.Robot("player");
            var career = new Career { owned = starter.PartIds.ToArray(), frames = new[] { starter.frame } };
            career.garage = GarageSave.Load(null, c); // three starter slots
            return career;
        }

        public bool Owns(PartDef p) => owned.Contains(p.id);
        public bool OwnsFrame(FrameDef f) => frames.Contains(f.id);
        public int HpOf(Content c, string partId) => damaged.FirstOrDefault(d => d.part == partId)?.hp ?? c.Part(partId).integrity;
        public int MissingIntegrity(Content c) => damaged.Sum(d => c.Part(d.part).integrity - d.hp) + chassisDamage;
        public int FullRepairCost(Content c) => MissingIntegrity(c) * CreditsPerPoint;

        // Builds may only use owned parts and frames.
        public List<string> Validate(Content c, RobotDef b = null)
        {
            b ??= Build;
            var errors = c.Validate(b);
            if (!frames.Contains(b.frame)) errors.Add($"Você não tem o frame {c.Frame(b.frame).name}.");
            foreach (var g in b.PartIds.GroupBy(id => id))
                if (owned.Count(o => o == g.Key) < g.Count())
                    errors.Add(owned.Contains(g.Key) ? $"Você só tem {owned.Count(o => o == g.Key)} {c.Part(g.Key).name}." : $"Você não tem a peça {c.Part(g.Key).name}.");
            return errors;
        }

        // ---- Job ----
        public StageDef Stage(Content c) => c.Job(job).stages[Math.Min(stage, c.Job(job).stages.Length - 1)];
        public bool JobDone(Content c) => InJob && stage >= c.Job(job).stages.Length;

        public void StartJob(Content c, string jobId)
        {
            c.Job(jobId); // throws on unknown job
            job = jobId; stage = 0; offer = new string[0]; lastResult = null;
        }

        public void EndJob() { job = null; stage = 0; offer = new string[0]; }

        // Battle with the current build; damage from earlier fights carries over.
        public Battle StartBattle(Content c, int seed, string enemyId)
        {
            if (offer.Length > 0) throw new InvalidOperationException("Escolha a recompensa antes da próxima luta.");
            var errors = Validate(c);
            if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
            var b = new Battle(c, seed, Build.Clone(), enemyId);
            foreach (var p in b.player.parts.Values) p.hp = HpOf(c, p.def.id);
            b.player.chassis = Math.Max(1, b.player.chassisMax - chassisDamage);
            return b;
        }

        // Keeps the damage the robot took in a fight.
        public void KeepDamage(Content c, Battle b)
        {
            // shortcut: two copies of the same part share one damage entry (the worst one); track per copy if that matters.
            var hp = b.player.parts.Values.GroupBy(p => p.def.id).ToDictionary(g => g.Key, g => Math.Max(0, g.Min(p => p.hp)));
            damaged = owned.Distinct()
                .Select(id => new PartHp { part = id, hp = hp.TryGetValue(id, out var h) ? h : HpOf(c, id) })
                .Where(d => d.hp < c.Part(d.part).integrity).ToArray();
            chassisDamage = b.player.chassisMax - b.player.chassis;
        }

        // Job fight outcome. Win: damage carries over, stage reward and a choice of 2 parts from the beaten robot.
        // Loss: the job ends and the robot is repaired for credits (never lose parts).
        public void FinishJobFight(Content c, Battle b, System.Random rng)
        {
            var st = Stage(c);
            if (b.result != Result.Win)
            {
                KeepDamage(c, b);
                int cost = Math.Min(credits, FullRepairCost(c));
                credits -= cost;
                damaged = new PartHp[0]; chassisDamage = 0;
                lastResult = $"Derrota contra {c.Robot(st.enemy).name}. O trabalho acabou e o reparo custou {cost} créditos.";
                EndJob();
                return;
            }
            KeepDamage(c, b);
            credits += st.credits;
            scrap += st.scrap;
            // New parts first; an arm already owned can come as a second copy (one per arm).
            offer = c.Robot(st.enemy).PartIds.Distinct()
                .Where(id => !owned.Contains(id) || (c.Part(id).slot == "Braco" && owned.Count(o => o == id) < 2))
                .Select(id => (id, k: rng.Next())).OrderBy(x => owned.Contains(x.id)).ThenBy(x => x.k).Select(x => x.id).Take(2).ToArray();
            lastResult = $"Vitória contra {c.Robot(st.enemy).name}: +{st.credits} créditos, +{st.scrap} sucata.";
            stage++;
        }

        public void Choose(string partId)
        {
            if (!offer.Contains(partId)) throw new ArgumentException("Peça fora da oferta.");
            owned = owned.Append(partId).ToArray();
            offer = new string[0];
        }

        public void SkipReward() => offer = new string[0];

        // ---- Repairs ----
        // 1 sucata repairs RepairPerScrap integrity on a part (or the chassis when partId is null).
        public bool RepairWithScrap(Content c, string partId)
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

        public bool RepairAllWithCredits(Content c)
        {
            int cost = FullRepairCost(c);
            if (cost == 0 || credits < cost) return false;
            credits -= cost;
            damaged = new PartHp[0]; chassisDamage = 0;
            return true;
        }

        // ---- Save ----
        // Missing/corrupt file or unknown ids (content changed) start a new career.
        public static Career Load(string path, Content c)
        {
            try
            {
                if (path != null && File.Exists(path))
                {
                    var s = JsonUtility.FromJson<Career>(File.ReadAllText(path));
                    bool ok = s != null && s.owned.All(id => c.parts.Any(p => p.id == id)) && s.frames.All(id => c.frames.Any(f => f.id == id))
                              && (!s.InJob || c.jobs.Any(j => j.id == s.job));
                    if (ok)
                    {
                        s.garage = GarageSave.Fix(s.garage, c);
                        return s;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("Carreira salva ilegível, começando outra: " + e.Message); }
            return null;
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }
    }
}
