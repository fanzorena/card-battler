using System.Linq;
using CardBattler.Combat;
using UnityEngine;

// Placeholder battle screen drawn with IMGUI. Spawns itself in any scene that has none.
public class BattleView : MonoBehaviour
{
    const float W = 1280, H = 720;
    Content content;
    Battle battle;
    BattleLogger logger;
    Card pending; // card waiting for a target choice
    Target pendingTarget;
    Vector2 logScroll;
    GUIStyle box, small, big, cardStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<BattleView>() == null) new GameObject("BattleView").AddComponent<BattleView>();
    }

    void Start()
    {
        content = Content.Load();
        Restart();
    }

    void Restart()
    {
        logger?.Dispose();
        battle = new Battle(content, System.Environment.TickCount);
        logger = new BattleLogger(battle, LogDir);
        pending = null;
    }

    // Editor: <projeto>/Logs/Battles. Build: persistentDataPath/Battles.
    static string LogDir => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.isEditor ? System.IO.Path.Combine(Application.dataPath, "..", "Logs") : Application.persistentDataPath, "Battles"));

    void OnDestroy() => logger?.Dispose();

    void Styles()
    {
        if (box != null) return;
        box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14, wordWrap = true };
        small = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        big = new GUIStyle(GUI.skin.label) { fontSize = 48, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        cardStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.UpperLeft, fontSize = 13, wordWrap = true };
    }

    void OnGUI()
    {
        if (battle == null) return;
        Styles();
        float s = Mathf.Min(Screen.width / W, Screen.height / H);
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));

        Rect(new Rect(0, 0, W, H), new Color(0.12f, 0.12f, 0.15f));
        DrawRobot(battle.enemy, new Rect(W - 430, 10, 420, 300), pendingTarget == Target.EnemyPart);
        DrawRobot(battle.player, new Rect(10, 250, 420, 300), pendingTarget == Target.OwnPart);
        DrawIntent(new Rect(W - 430, 320, 420, 110));
        DrawLog(new Rect(450, 10, 380, 250));
        DrawHand(new Rect(10, 560, W - 20, 150));

        var p = battle.player;
        GUI.Label(new Rect(450, 270, 400, 30), $"Turno {battle.turn}   Energia: {p.energy}   Deck: {p.deck.Count}   Descarte: {p.discard.Count}", box);
        if (pending != null)
        {
            GUI.Label(new Rect(450, 310, 380, 40), $"{pending.def.name}: escolha uma parte {(pendingTarget == Target.OwnPart ? "sua" : "do inimigo")}.", box);
            if (GUI.Button(new Rect(450, 355, 120, 30), "Cancelar")) pending = null;
        }
        BattleLogger.MirrorToConsole = GUI.Toggle(new Rect(W - 190, 510, 180, 24), BattleLogger.MirrorToConsole, " Log no console");
        if (battle.result == Result.None && pending == null && GUI.Button(new Rect(W - 190, 450, 180, 50), "Encerrar turno"))
            battle.EndPlayerTurn();

        if (battle.result != Result.None)
        {
            Rect(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.75f));
            GUI.Label(new Rect(0, 220, W, 100), battle.result == Result.Win ? "VITÓRIA" : "DERROTA", big);
            if (GUI.Button(new Rect(W / 2 - 100, 360, 200, 50), "Reiniciar")) Restart();
        }
        if (pending == null) pendingTarget = Target.None;
    }

    void DrawRobot(Robot r, Rect area, bool targetable)
    {
        Rect(area, new Color(0.2f, 0.2f, 0.25f));
        GUI.Label(new Rect(area.x + 8, area.y + 4, area.width, 22), $"<b>{r.name}</b>  ({r.frame.name})", new GUIStyle(small) { richText = true, fontSize = 16 });
        string stats = $"Blindagem {r.armor}   Calor {r.heat}{(r.heat >= Robot.HeatLimit ? "!" : "")}   Esquiva {r.dodge}" +
                       (r.overload > 0 ? $"   Sobrecarga {r.overload}" : "") + (r.aimPending ? "   [Mirando]" : "");
        GUI.Label(new Rect(area.x + 8, area.y + 26, area.width, 20), stats, small);
        float y = area.y + 50;
        Bar(new Rect(area.x + 8, y, area.width - 16, 26), "Chassi", r.chassis, r.frame.chassis, new Color(0.5f, 0.5f, 0.6f), false);
        y += 32;
        foreach (var part in r.parts.Values)
        {
            var row = new Rect(area.x + 8, y, area.width - 16, 30);
            string label = $"{Battle.SlotName(part.slot)}: {part.def.name}" + (part.corrosion > 0 ? $"  (Corrosão {part.corrosion})" : "");
            Bar(row, label, part.hp, part.Max, part.Broken ? new Color(0.5f, 0.15f, 0.15f) : new Color(0.2f, 0.6f, 0.3f), targetable);
            if (targetable && GUI.Button(row, GUIContent.none, GUIStyle.none))
            {
                var c = pending; pending = null;
                battle.Play(battle.player, c, part.slot);
            }
            y += 36;
        }
        GUI.Label(new Rect(area.x + 8, y, area.width, 20), $"Núcleo: {r.core.name} ({r.core.energy} energia/turno)", small);
    }

    void DrawIntent(Rect area)
    {
        string text = battle.enemyIntent.Count == 0 ? "Nenhuma ação." :
            string.Join("\n", battle.enemyIntent.Select(battle.IntentText));
        GUI.Label(area, "<b>Intenção do inimigo</b>\n" + text, new GUIStyle(box) { richText = true });
    }

    void DrawLog(Rect area)
    {
        GUI.Box(area, "");
        GUILayout.BeginArea(area);
        logScroll = GUILayout.BeginScrollView(logScroll);
        foreach (var l in battle.log) GUILayout.Label(l, small);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        logScroll.y = float.MaxValue; // keep newest visible
    }

    void DrawHand(Rect area)
    {
        var p = battle.player;
        float w = 150, x = area.x;
        foreach (var c in p.hand.ToList())
        {
            int cost = p.CostOf(c);
            bool broken = p.IsBroken(c.source);
            GUI.enabled = pending == null && battle.CanPlay(p, c);
            string label = $"<b>{c.def.name}</b>  [{cost}]\n<size=11>{Battle.SlotName(c.source)}{(broken ? " (avariada)" : "")}</size>\n{c.def.text}";
            if (GUI.Button(new Rect(x, area.y, w, 115), label, new GUIStyle(cardStyle) { richText = true }))
            {
                var t = battle.NeedsTarget(p, c);
                if (t == Target.None) battle.Play(p, c);
                else { pending = c; pendingTarget = t; }
            }
            GUI.enabled = pending == null && battle.result == Result.None;
            if (GUI.Button(new Rect(x, area.y + 118, w, 26), "Sucata (+1)")) battle.Scrap(p, c);
            GUI.enabled = true;
            x += w + 8;
        }
    }

    static void Bar(Rect r, string label, int value, int max, Color fill, bool highlight)
    {
        Rect(r, highlight ? new Color(0.9f, 0.8f, 0.2f) : new Color(0.08f, 0.08f, 0.1f));
        var inner = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
        Rect(inner, new Color(0.15f, 0.15f, 0.18f));
        Rect(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(max > 0 ? (float)value / max : 0), inner.height), fill);
        GUI.Label(new Rect(inner.x + 6, inner.y + 3, inner.width, inner.height), $"{label}   {value}/{max}");
    }

    static void Rect(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
