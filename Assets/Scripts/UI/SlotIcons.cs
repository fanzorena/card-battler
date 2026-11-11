using System;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        public static RectTransform Icon(Transform parent, Slot slot, float size, Color? lit = null) => Icon(parent, Key(slot), size, lit);

        public static RectTransform Icon(Transform parent, string key, float size, Color? lit = null)
        {
            var root = Theme.Node(parent, "SlotIcon:" + key);
            root.sizeDelta = new Vector2(size, size);
            var on = lit ?? Theme.Warn;
            var plate = Theme.Box(root, "Plate", Theme.Mix(Theme.Bg, on, 0.14f), Theme.Circle, raycast: true);
            plate.rectTransform.Fill();
            Theme.Box(root, "Border", Theme.Alpha(on, 0.9f), Theme.Ring).rectTransform.Fill();
            var off = Theme.Alpha(Theme.Ink, 0.2f);
            bool all = key == "Frame";
            Color C(string part) => all ? Theme.Alpha(on, 0.75f) : part == key ? on : off;
            Image S(float x, float y, float w, float h, Color c, Sprite sp = null)
            {
                bool hot = c == on && !all; // the lit part reads bigger
                var i = Theme.Box(root, "s", c, sp ?? Theme.Rounded);
                i.rectTransform.Center(size * x, size * y, size * w * (hot ? 1.15f : 1), size * h * (hot ? 1.15f : 1));
                return i;
            }
            S(0.5f, 0.22f, 0.24f, 0.18f, C("Cabeca"));
            S(0.5f, 0.47f, 0.30f, 0.28f, C("Torso"));
            S(0.27f, 0.47f, 0.10f, 0.28f, C("BracoEsq"));
            S(0.73f, 0.47f, 0.10f, 0.28f, C("BracoDir"));
            S(0.42f, 0.77f, 0.10f, 0.22f, C("Pernas"));
            S(0.58f, 0.77f, 0.10f, 0.22f, C("Pernas"));
            if (key == Nucleo || key == "Chassi") S(0.5f, 0.47f, 0.16f, 0.16f, key == "Chassi" ? Theme.Exposed : on, Theme.Circle);
            if (key == "Chassi") S(0.5f, 0.47f, 0.42f, 0.42f, Theme.Exposed, Theme.Ring);
            if (all) S(0.5f, 0.5f, 0.92f, 0.92f, Theme.Alpha(on, 0.8f), Theme.Ring);
            if (key == "BracoEsq" || key == "BracoDir")
            {
                bool left = key == "BracoEsq";
                // side marker on a small dark tab so it reads even at 30 px
                var tab = Theme.Box(root, "Side", Theme.Bg, Theme.Circle);
                tab.rectTransform.Center(size * (left ? 0.1f : 0.9f), size * 0.12f, size * 0.42f, size * 0.42f);
                var mark = Theme.Label(tab.transform, left ? "E" : "D", Mathf.Max(12, Mathf.RoundToInt(size * 0.36f)), on, TextAnchor.MiddleCenter, FontStyle.Bold);
                mark.rectTransform.Fill();
            }
            plate.gameObject.AddComponent<HoverHelp>().text = Name(key);
            return root;
        }
    }

    // Shows a short tooltip while the pointer is over the graphic.
    public class HoverHelp : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string text;
        public void OnPointerEnter(PointerEventData e) => Tooltip.Show(Fx.PosOf((RectTransform)transform) + new Vector2(0, 120), new[] { text });
        public void OnPointerExit(PointerEventData e) => Tooltip.Hide();
    }
}
