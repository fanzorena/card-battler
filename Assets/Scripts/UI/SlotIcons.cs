using System.Collections.Generic;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace CardBattler.UI
{
    // Slot pictograms: a tiny robot with the relevant part lit. Same icon on cards, panels, garage, intents and the robot,
    // so the player links "this card" to "this part" without reading words.
    public static class SlotIcons
    {
        public const string Nucleo = "Nucleo";

        public static string Key(Slot s) => s.ToString();

        public static string Name(string key) => key switch
        {
            "Cabeca" => "Cabeça", "Torso" => "Torso", "BracoEsq" => "Braço esquerdo", "BracoDir" => "Braço direito",
            "Pernas" => "Pernas", "Frame" => "Frame (carta assinatura)", "Chassi" => "Chassi", Nucleo => "Núcleo", _ => key,
        };

        public static VisualElement Icon(VisualElement parent, Slot slot, float size, Color? lit = null) => Icon(parent, Key(slot), size, lit);

        public static VisualElement Icon(VisualElement parent, string key, float size, Color? lit = null)
        {
            var on = lit ?? Theme.Warn;
            var root = Theme.Box(parent, "SlotIcon:" + key, Theme.Mix(Theme.Bg, on, 0.14f), Shape.Circle).Size(size, size);
            Theme.Border(root, Theme.Alpha(on, 0.9f), Mathf.Max(1.5f, size * 0.05f));
            var off = Theme.Alpha(Theme.Ink, 0.2f);
            bool all = key == "Frame";
            Color C(string part) => all ? Theme.Alpha(on, 0.75f) : part == key ? on : off;
            // shapes are placed in the icon's content box (inside the border)
            float inner = size - 2 * Mathf.Max(1.5f, size * 0.05f);
            void S(float x, float y, float w, float h, Color c, Shape sh = Shape.Rounded)
            {
                bool hot = c == on && !all; // the lit part reads bigger
                float k = hot ? 1.15f : 1;
                var e = Theme.Box(root, "s", c, sh).Center(inner * x, inner * y, inner * w * k, inner * h * k);
                if (sh == Shape.Rounded) RadiusOf(e, inner * 0.05f);
            }
            S(0.5f, 0.22f, 0.24f, 0.18f, C("Cabeca"));
            S(0.5f, 0.47f, 0.30f, 0.28f, C("Torso"));
            S(0.27f, 0.47f, 0.10f, 0.28f, C("BracoEsq"));
            S(0.73f, 0.47f, 0.10f, 0.28f, C("BracoDir"));
            S(0.42f, 0.77f, 0.10f, 0.22f, C("Pernas"));
            S(0.58f, 0.77f, 0.10f, 0.22f, C("Pernas"));
            if (key == Nucleo || key == "Chassi") S(0.5f, 0.47f, 0.16f, 0.16f, key == "Chassi" ? Theme.Exposed : on, Shape.Circle);
            if (key == "Chassi") S(0.5f, 0.47f, 0.42f, 0.42f, Theme.Exposed, Shape.Ring);
            if (all) S(0.5f, 0.5f, 0.92f, 0.92f, Theme.Alpha(on, 0.8f), Shape.Ring);
            if (key == "BracoEsq" || key == "BracoDir")
            {
                bool left = key == "BracoEsq";
                // side marker on a small dark tab so it reads even at 30 px
                var tab = Theme.Box(root, "Side", Theme.Bg, Shape.Circle).Center(inner * (left ? 0.1f : 0.9f), inner * 0.12f, size * 0.46f, size * 0.46f);
                Theme.Label(tab, left ? "E" : "D", Mathf.Max(12, Mathf.RoundToInt(size * 0.36f)), on, TextAnchor.MiddleCenter, FontStyle.Bold).Fill();
            }
            Theme.Hover(root, Name(key));
            return root;
        }

        static void RadiusOf(VisualElement e, float r)
        {
            var s = e.style;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = r;
        }

        // Marks shown on an icon (tests check the arm side letters).
        public static IEnumerable<string> Marks(VisualElement icon) => icon.Query<Label>().ToList().Select(l => l.text);
    }

    // Floating help box. One per panel; lives on the overlay.
    public static class Tooltip
    {
        static VisualElement box;
        static Label text;

        public static void Init(VisualElement overlay)
        {
            box = Theme.El(overlay, "Tooltip", "tooltip");
            text = Theme.Label(box, "", Theme.Small, Theme.Ink);
            box.Show(false);
        }

        // Shows lines next to the hovered element: above it, or below if there is no room.
        public static void Show(VisualElement target, IEnumerable<string> lines)
        {
            var all = lines?.Where(l => !string.IsNullOrEmpty(l)).ToList();
            if (box == null || box.panel == null || all == null || all.Count == 0 || target.panel == null) { Hide(); return; }
            text.text = string.Join("\n", all.Select(Theme.Highlight));
            var r = box.parent.WorldToLocal(target.worldBound);
            float x = Mathf.Clamp(r.center.x - 250, 10, Theme.Reference.x - 510);
            bool below = r.yMin < 220;
            box.style.left = x;
            box.style.top = below ? r.yMax + 12 : r.yMin - 12;
            box.style.translate = new Translate(0, below ? Length.Percent(0) : Length.Percent(-100));
            box.Show(true);
            box.BringToFront();
        }

        public static void Hide() { box?.Show(false); }
    }
}
