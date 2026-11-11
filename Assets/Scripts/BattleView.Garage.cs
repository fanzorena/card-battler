using System;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;

// Garage screen: pick a save slot, a frame and one part per slot, then fight.
public partial class BattleView
{
    GarageSave garage;
    Vector2 deckScroll;
    int opponentIndex;
    bool showDossier = true;

    // Enemies with a category, easiest first.
    RobotDef[] Opponents => content.robots.Where(r => !string.IsNullOrEmpty(r.tier)).OrderBy(r => Array.IndexOf(RobotDef.Tiers, r.tier)).ToArray();
    RobotDef Opponent => Opponents[(opponentIndex % Opponents.Length + Opponents.Length) % Opponents.Length];

    static string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "garage.json");

    static readonly (string label, string slot, Func<RobotDef, string> get, Action<RobotDef, string> set)[] GarageSlots =
    {
        ("Cabeça", "Cabeca", b => b.head, (b, v) => b.head = v),
        ("Torso", "Torso", b => b.torso, (b, v) => b.torso = v),
        ("Braço Esq", "Braco", b => b.armL, (b, v) => b.armL = v),
        ("Braço Dir", "Braco", b => b.armR, (b, v) => b.armR = v),
        ("Pernas", "Pernas", b => b.legs, (b, v) => b.legs = v),
        ("Núcleo", "Nucleo", b => b.core, (b, v) => b.core = v),
    };

    static string Cycle(string[] ids, string current, int dir)
    {
        int i = Array.IndexOf(ids, current);
        return ids[((i < 0 ? 0 : i + dir) % ids.Length + ids.Length) % ids.Length];
    }

    void DrawGarage()
    {
        var rich = new GUIStyle(small) { richText = true };
        GUI.Label(new Rect(20, 10, 400, 40), "<b>Garagem</b>", new GUIStyle(rich) { fontSize = 26 });
        for (int i = 0; i < GarageSave.SlotCount; i++)
        {
            bool sel = garage.selected == i;
            if (GUI.Button(new Rect(260 + i * 130, 14, 120, 30), (sel ? "▶ " : "") + garage.slots[i].name))
            {
                garage.selected = i;
                garage.Save(SavePath);
            }
        }

        var b = garage.Current;
        bool changed = false;
        float y = 60;

        // Frame row
        var frame = content.Frame(b.frame);
        Rect(new Rect(20, y, 600, 56), new Color(0.2f, 0.2f, 0.25f));
        GUI.Label(new Rect(28, y + 4, 90, 24), "<b>Frame</b>", rich);
        if (GUI.Button(new Rect(110, y + 4, 28, 24), "<")) { b.frame = Cycle(content.frames.Select(f => f.id).ToArray(), b.frame, -1); changed = true; }
        GUI.Label(new Rect(144, y + 6, 220, 24), frame.name, rich);
        if (GUI.Button(new Rect(360, y + 4, 28, 24), ">")) { b.frame = Cycle(content.frames.Select(f => f.id).ToArray(), b.frame, 1); changed = true; }
        GUI.Label(new Rect(28, y + 30, 590, 24),
            $"Chassi {frame.chassis} · Mão {frame.hand} · Carga {frame.carry} · {frame.passiveText} · Assinatura: {content.Card(frame.signature).name}", small);
        y += 62;

        // Part rows
        foreach (var (label, slot, get, set) in GarageSlots)
        {
            var ids = content.PartsFor(slot).Select(p => p.id).ToArray();
            var part = content.Part(get(b));
            Rect(new Rect(20, y, 600, 72), new Color(0.2f, 0.2f, 0.25f));
            GUI.Label(new Rect(28, y + 4, 90, 24), $"<b>{label}</b>", rich);
            if (GUI.Button(new Rect(110, y + 4, 28, 24), "<")) { set(b, Cycle(ids, get(b), -1)); changed = true; }
            GUI.Label(new Rect(144, y + 6, 220, 24), part.name, rich);
            if (GUI.Button(new Rect(360, y + 4, 28, 24), ">")) { set(b, Cycle(ids, get(b), 1)); changed = true; }
            string stats = slot == "Nucleo"
                ? $"{part.energy} energia/turno{(part.heatPerTurn > 0 ? $", +{part.heatPerTurn} Calor/turno" : "")}"
                : $"Integridade {part.integrity}";
            GUI.Label(new Rect(400, y + 6, 215, 24), $"{part.maker} · {(string.IsNullOrEmpty(part.element) ? "—" : part.element)} · Peso {part.weight}", small);
            string cards = string.Join(", ", part.cards.GroupBy(c => c).Select(g => $"{g.Count()}x {content.Card(g.Key).name}"));
            GUI.Label(new Rect(28, y + 30, 590, 40), stats + (cards.Length > 0 ? " · " + cards : ""), small);
            y += 76;
        }
        if (changed) garage.Save(SavePath);

        // Summary
        float x = 640, w = W - x - 20;
        Rect(new Rect(x, 60, w, 640), new Color(0.2f, 0.2f, 0.25f));
        int weight = content.Weight(b);
        var torso = content.Part(b.torso);
        var makers = content.ActiveMakers(b);
        var core = content.Part(b.core);
        var errors = content.Validate(b);
        string weightColor = weight > frame.carry ? "#ff6060" : "#80ff80";
        GUI.Label(new Rect(x + 10, 66, w - 20, 120),
            $"<b>Peso:</b> <color={weightColor}>{weight} / {frame.carry}</color>\n" +
            $"<b>Torso + chassi:</b> {torso.integrity + frame.chassis}   <b>Integridade total:</b> {frame.chassis + b.PartIds.Sum(id => content.Part(id).integrity)}\n" +
            $"<b>Energia:</b> {core.energy}/turno   <b>Mão:</b> {frame.hand}\n" +
            $"<b>Bônus de fabricante:</b> {(makers.Length == 0 ? "nenhum (precisa de 3 partes do mesmo fabricante)" : string.Join("\n   ", makers.Select(m => m.id + ": " + m.text)))}",
            new GUIStyle(rich) { fontSize = 14 });

        var deck = content.Deck(b);
        var op = Opponent;
        if (GUI.Toggle(new Rect(x + 10, 176, 160, 26), !showDossier, $"Deck ({deck.Count} cartas)", GUI.skin.button)) showDossier = false;
        if (GUI.Toggle(new Rect(x + 176, 176, 220, 26), showDossier, "Dossiê do adversário", GUI.skin.button)) showDossier = true;
        GUILayout.BeginArea(new Rect(x + 10, 208, w - 20, 326));
        deckScroll = GUILayout.BeginScrollView(deckScroll);
        var wrap = new GUIStyle(small) { richText = true };
        if (!showDossier)
            foreach (var g in deck.GroupBy(c => c.id))
                GUILayout.Label($"{g.Count()}x {g.First().name} [{g.First().cost}] — {g.First().text}", small);
        else
            DrawDossier(op, wrap);
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        if (errors.Count > 0)
            GUI.Label(new Rect(x + 10, 540, w - 20, 50), "<color=#ff6060>" + string.Join("\n", errors) + "</color>", rich);

        // Opponent picker (changing opponent opens its dossier)
        GUI.Label(new Rect(x + 10, 592, 90, 24), "<b>Adversário</b>", rich);
        if (GUI.Button(new Rect(x + 100, 590, 28, 24), "<")) { opponentIndex--; showDossier = true; }
        GUI.Label(new Rect(x + 134, 592, 330, 24), $"{op.name} <color=#ffd060>({op.tier})</color>", rich);
        if (GUI.Button(new Rect(x + 470, 590, 28, 24), ">")) { opponentIndex++; showDossier = true; }
        GUI.enabled = errors.Count == 0;
        if (GUI.Button(new Rect(x + w - 190, 646, 180, 46), "Lutar")) StartBattle(b);
        GUI.enabled = true;
        BattleLogger.MirrorToConsole = GUI.Toggle(new Rect(x + 10, 660, 200, 24), BattleLogger.MirrorToConsole, " Log no console");
    }

    // What the player "heard" about the opponent before the fight. Never shows win rates.
    void DrawDossier(RobotDef op, GUIStyle style)
    {
        GUILayout.Label($"<b>{op.name}</b> <color=#ffd060>({op.tier})</color> · fabricante principal: {content.MainMaker(op)}", style);
        GUILayout.Label($"<b>Como luta:</b> {content.Describe(op)}.", style);
        var d = op.dossier;
        if (d == null || !d.Exists)
        {
            GUILayout.Label("<i>Ninguém no bar perde tempo falando desse aí. Adversário de treino.</i>", style);
            return;
        }
        GUILayout.Space(4);
        GUILayout.Label("<b>Rumores</b>", style);
        foreach (var r in d.rumors) GUILayout.Label("<i>" + r + "</i>", style);
        GUILayout.Space(4);
        GUILayout.Label("<b><color=#80ff80>Funciona bem</color></b>", style);
        foreach (var t in d.worksWell) GUILayout.Label("• " + t, style);
        GUILayout.Space(4);
        GUILayout.Label("<b><color=#ff8080>Evite</color></b>", style);
        foreach (var t in d.avoid) GUILayout.Label("• " + t, style);
    }
}
