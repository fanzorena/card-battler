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
        public string terrain; // arena override; "none" = neutral (tutorial fights)
        public string reward;  // part always given for winning this stage (on top of the 1-of-2 choice)
    }

    [Serializable]
    public class JobDef
    {
        public string id, name, fixer, intro, outro;
        public string blueprint;                 // reward for finishing the job (optional)
        public string[] unlocks = new string[0]; // map places opened by finishing it
        public StageDef[] stages = new StageDef[0];
    }

    [Serializable]
    public class LineDef { public string who, text; }

    [Serializable]
    public class DialogueDef
    {
        public string id;
        public LineDef[] lines = new LineDef[0];
    }

    // Ferro-velho stock: exactly one of part / frame / blueprint.
    [Serializable]
    public class ShopItem { public string part, frame, blueprint; public int price; }

    [Serializable]
    public class BlueprintDef { public string id, name, part; public int scrap, credits; }

    [Serializable]
    public class ClueDef { public string id, title, text, source; }

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
        public string[] unlocked = new string[0]; // map places opened ("oficina", "fosso")
        public int pitRank;                      // Fosso ladder fights won (== pit.Length: champion)
        public string lastPit;                   // short text about the last Fosso fight
        public int circuitRank;                  // Circuito Regional fights won
        public string lastCircuit;
        public string region = "porto";          // map shown: "porto" or "neon"
        public string[] clues = new string[0];   // clue ids in the Pistas journal
        public string[] seen = new string[0];    // one-time dialogues already heard
        public string[] blueprints = new string[0]; // known crafting blueprints
        public const int SellOffPrice = 10;      // parts the shop doesn't stock

        public const string ClueRigged = "luta_armada", ClueSale = "venda_aurea";
        public static readonly string[] PortPlaces = { "oficina", "fosso" }; // opened by the first job win

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
        public const string Arena = "fosso"; // the job and the ranking ladder are both fought in the Fossos

        public Battle StartBattle(Content c, int seed, string enemyId, string arena = Arena)
        {
            if (offer.Length > 0) throw new InvalidOperationException("Escolha a recompensa antes da próxima luta.");
            var errors = Validate(c);
            if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
            var b = new Battle(c, seed, Build.Clone(), enemyId, false, c.terrains.Any(t => t.id == arena) ? arena : null);
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
            if (!string.IsNullOrEmpty(st.reward) && !owned.Contains(st.reward))
            {
                owned = owned.Append(st.reward).ToArray();
                offer = offer.Where(id => id != st.reward).ToArray();
                lastResult += $" Ganhou também: {c.Part(st.reward).name}.";
            }
            stage++;
            foreach (var p in PortPlaces) Unlock(p);
            if (JobDone(c)) CompleteJob(c);
        }

        // Rewards for finishing a job: its blueprint and the places it opens.
        void CompleteJob(Content c)
        {
            var j = c.Job(job);
            foreach (var place in j.unlocks) Unlock(place);
            if (!string.IsNullOrEmpty(j.blueprint)) LearnBlueprint(c, j.blueprint);
        }

        // Simulator bot: puts on the owned legs that score best against the next enemy on that arena (legal builds only).
        // score(build) = win rate; the caller caches it. shortcut: legs only, the slot the job rewards change; extend per slot if needed.
        public bool AutoEquipLegs(Content c, Func<RobotDef, float> score)
        {
            var best = Build;
            float bestScore = score(best);
            foreach (var legs in owned.Distinct().Where(id => c.Part(id).slot == "Pernas" && id != Build.legs))
            {
                var b = Build.Clone();
                b.legs = legs;
                if (Validate(c, b).Count > 0) continue;
                float s = score(b);
                if (s > bestScore) { best = b; bestScore = s; }
            }
            if (best == Build) return false;
            garage.slots[garage.selected] = best;
            return true;
        }

        // ---- Ferro-velho: shop, selling, crafting ----
        public void LearnBlueprint(Content c, string id) { c.Blueprint(id); if (!blueprints.Contains(id)) blueprints = blueprints.Append(id).ToArray(); }

        // Arms can be owned twice (one per arm); everything else once. Known blueprints and owned frames aren't sold again.
        public bool CanBuy(Content c, ShopItem item)
        {
            if (credits < item.price) return false;
            if (item.frame != null) return !frames.Contains(item.frame);
            if (item.blueprint != null) return !blueprints.Contains(item.blueprint);
            return owned.Count(o => o == item.part) < (c.Part(item.part).slot == "Braco" ? 2 : 1);
        }

        public bool Buy(Content c, ShopItem item)
        {
            if (!CanBuy(c, item)) return false;
            credits -= item.price;
            if (item.frame != null) frames = frames.Append(item.frame).ToArray();
            else if (item.blueprint != null) LearnBlueprint(c, item.blueprint);
            else owned = owned.Append(item.part).ToArray();
            return true;
        }

        public int SellPrice(Content c, string partId) => c.shop.FirstOrDefault(s => s.part == partId) is ShopItem s ? s.price / 2 : SellOffPrice;

        // A part can be sold only if every saved build still has the copies it uses.
        public bool CanSell(string partId) =>
            owned.Count(o => o == partId) - 1 >= garage.slots.Max(b => b.PartIds.Count(id => id == partId));

        public bool Sell(Content c, string partId)
        {
            if (!CanSell(partId)) return false;
            var list = owned.ToList();
            list.Remove(partId);
            owned = list.ToArray();
            damaged = damaged.Where(d => owned.Contains(d.part)).ToArray();
            credits += SellPrice(c, partId);
            return true;
        }

        public bool CanCraft(Content c, string blueprintId)
        {
            var bp = c.Blueprint(blueprintId);
            return blueprints.Contains(blueprintId) && scrap >= bp.scrap && credits >= bp.credits
                   && owned.Count(o => o == bp.part) < (c.Part(bp.part).slot == "Braco" ? 2 : 1);
        }

        public bool Craft(Content c, string blueprintId)
        {
            if (!CanCraft(c, blueprintId)) return false;
            var bp = c.Blueprint(blueprintId);
            scrap -= bp.scrap; credits -= bp.credits;
            owned = owned.Append(bp.part).ToArray();
            return true;
        }

        // ---- Map / Fosso / Pistas ----
        public bool IsOpen(string place) => place == "bar" || unlocked.Contains(place);
        public void Unlock(string place) { if (!unlocked.Contains(place)) unlocked = unlocked.Append(place).ToArray(); }
        public bool Champion(Content c) => LadderTop(c, "pit");
        public StageDef PitStage(Content c) => LadderStage(c, "pit");

        // Ranking ladders: "pit" (Fosso, porto) and "circuit" (Circuito Regional, Distrito Neon).
        public static StageDef[] Ladder(Content c, string ladder) => ladder == "circuit" ? c.circuit : c.pit;
        public static string ArenaFor(StageDef st, string fallback) => string.IsNullOrEmpty(st.terrain) ? fallback : st.terrain;
        public static string ArenaOf(string ladder) => ladder == "circuit" ? "neon_chuva" : Arena;
        public int RankIn(string ladder) => ladder == "circuit" ? circuitRank : pitRank;
        public bool LadderTop(Content c, string ladder) => RankIn(ladder) >= Ladder(c, ladder).Length;
        public StageDef LadderStage(Content c, string ladder) { var s = Ladder(c, ladder); return s[Math.Min(RankIn(ladder), s.Length - 1)]; }
        public string LastLadder(string ladder) => ladder == "circuit" ? lastCircuit : lastPit;
        // Glitch finds the sale record once you have two Circuito wins.
        public bool GlitchReady => circuitRank >= 2 && !clues.Contains(ClueSale);
        // The old bettor shows up in the bar once you're champion, until he's told his story.
        public bool BettorWaiting(Content c) => Champion(c) && !clues.Contains(ClueRigged);
        public bool Seen(string dialogueId) => seen.Contains(dialogueId);
        public void MarkSeen(string dialogueId) { if (!Seen(dialogueId)) seen = seen.Append(dialogueId).ToArray(); }
        public void LearnClue(Content c, string id)
        {
            c.Clue(id);
            if (!clues.Contains(id)) clues = clues.Append(id).ToArray();
            if (id == ClueRigged) Unlock("neon"); // the first clue points to the Distrito Neon
        }

        // Ranking fight. Win: money, one rung up, damage stays. Loss: one rung down and nothing else
        // (the Fosso crew patches the robot for free, no credits charged).
        public void FinishPitFight(Content c, Battle b) => FinishLadderFight(c, b, "pit");

        public void FinishLadderFight(Content c, Battle b, string ladder)
        {
            var st = LadderStage(c, ladder);
            var name = c.Robot(st.enemy).name;
            bool circuit = ladder == "circuit";
            string where = circuit ? "no Circuito" : "no Fosso";
            int rank = RankIn(ladder), top = Ladder(c, ladder).Length;
            string text;
            if (b.result == Result.Win)
            {
                KeepDamage(c, b);
                credits += st.credits;
                scrap += st.scrap;
                rank = Math.Min(top, rank + 1);
                text = $"Vitória {where} contra {name}: +{st.credits} créditos, +{st.scrap} sucata."
                       + (rank >= top ? circuit ? " Você é o campeão do Circuito Regional!" : " Você é o novo campeão dos Fossos!" : "");
            }
            else
            {
                damaged = new PartHp[0]; chassisDamage = 0;
                rank = Math.Max(0, rank - 1);
                text = $"Derrota {where} contra {name}. Você caiu uma posição no ranking. A equipe remendou o robô de graça.";
            }
            if (circuit) { circuitRank = rank; lastCircuit = text; }
            else { pitRank = rank; lastPit = text; if (Champion(c)) Unlock("ferro"); }
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
                              && (!s.InJob || c.jobs.Any(j => j.id == s.job)) && s.clues.All(id => c.clues.Any(k => k.id == id))
                              && s.blueprints.All(id => c.blueprints.Any(b => b.id == id));
                    if (ok)
                    {
                        s.pitRank = Math.Clamp(s.pitRank, 0, c.pit.Length);
                        s.circuitRank = Math.Clamp(s.circuitRank, 0, c.circuit.Length);
                        if (s.region != "neon" || !s.IsOpen("neon")) s.region = "porto";
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
