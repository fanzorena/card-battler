using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CardBattler.Combat
{
    // The 3 garage slots, saved as JSON on disk.
    [Serializable]
    public class GarageSave
    {
        public const int SlotCount = 3;
        public RobotDef[] slots = new RobotDef[0];
        public int selected;

        public RobotDef Current => slots[selected];

        // Missing/corrupt file or builds with unknown ids (content changed) fall back to the starter build.
        public static GarageSave Load(string path, Content content)
        {
            GarageSave save = null;
            try { if (path != null && File.Exists(path)) save = JsonUtility.FromJson<GarageSave>(File.ReadAllText(path)); }
            catch (Exception e) { Debug.LogWarning("Garagem: save ilegível, recriando. " + e.Message); }
            return Fix(save, content);
        }

        // Always 3 named slots. A new save has only the first robot equipped; unknown builds go back to that default.
        public static GarageSave Fix(GarageSave save, Content content)
        {
            save ??= new GarageSave();
            var slots = new RobotDef[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                var b = save.slots != null && i < save.slots.Length ? save.slots[i] : null;
                bool known = b != null && content.frames.Any(f => f.id == b.frame) && b.Equipped.All(id => content.parts.Any(p => p.id == id));
                var starter = content.Robot("player");
                slots[i] = known ? b : i == 0 ? starter.Clone() : RobotDef.Empty(starter.frame);
                slots[i].id = "slot" + (i + 1);
                slots[i].name = "Robô " + (i + 1);
            }
            save.slots = slots;
            save.selected = Mathf.Clamp(save.selected, 0, SlotCount - 1);
            return save;
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }
    }
}
