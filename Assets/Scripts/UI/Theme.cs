using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattler.UI
{
    // Single source for the look: palette, manufacturer/element colors, fonts, procedural sprites and UI builders.
    public static class Theme
    {
        public static readonly Vector2 Reference = new Vector2(1920, 1080);

        public static readonly Color Bg = Hex("0B0D14"), Bg2 = Hex("141826"), Panel = Hex("181D2C"), PanelHi = Hex("222943"),
            Line = Hex("2E3756"), Ink = Hex("E6E9F2"), TextDim = Hex("8B93AD"), Good = Hex("5BE38A"), Bad = Hex("FF5A6A"),
            Warn = Hex("FFC24A"), Armor = Hex("5AA8FF"), Exposed = Hex("FF8A3D"), Energy = Hex("FFE066"), Paper = Hex("1D1A16");

        public const int Small = 18, Body = 22, Title = 30, Huge = 64;

        static readonly Dictionary<string, Color> Makers = new Dictionary<string, Color>
        {
            ["Ferrovelho"] = Hex("FF9A3D"), ["Bastion"] = Hex("C9D35A"), ["Kagemori"] = Hex("3DE0FF"), ["Nyx"] = Hex("E05CFF"),
        };
        static readonly Dictionary<string, Color> Elements = new Dictionary<string, Color>
        {
            ["Cinético"] = Hex("D9DEE8"), ["Térmico"] = Hex("FF6A3D"), ["Elétrico"] = Hex("FFE14D"), ["Químico"] = Hex("7CFF6B"),
        };

        public static Color Maker(string maker) => maker != null && Makers.TryGetValue(maker, out var c) ? c : TextDim;
        public static Color Element(string element) => element != null && Elements.TryGetValue(element, out var c) ? c : TextDim;
        public static string HexOf(Color c) => ColorUtility.ToHtmlStringRGB(c);

        public static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        public static Color Alpha(Color c, float a) { c.a = a; return c; }
        public static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, t);

        // ---- Glossary: one source for "?" icons and keyword tooltips (content.json "glossary") ----
        public static CardBattler.Combat.Content Texts; // set when content loads

        public static string Explain(string glossaryId)
        {
            var g = Texts?.Glossary(glossaryId);
            return g == null ? glossaryId : $"{g.title}: {g.text}";
        }

        // ---- Keywords: highlighted in card text, explained by their glossary entry ----
        public static readonly (string glossaryId, string pattern, Color color)[] Keywords =
        {
            ("encadear", @"Encadear", Hex("FFD34D")),
            ("blindagem", @"[Bb]lindagem", Hex("5AA8FF")),
            ("calor", @"Calor", Hex("FF6A3D")),
            ("sobrecarga", @"Sobrecarga", Hex("FFE14D")),
            ("corrosao", @"Corrosão", Hex("7CFF6B")),
            ("impacto", @"Impacto", Hex("FF9A3D")),
            ("esquivar", @"Esquivar|esquiva", Hex("9EF0FF")),
            ("ignora", @"ignora blindagem", Hex("FF8AD8")),
            ("sucata", @"[Ss]ucata", Hex("C8A27A")),
        };

        public static string Highlight(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            foreach (var k in Keywords)
                text = Regex.Replace(text, k.pattern, m => $"<color=#{HexOf(k.color)}><b>{m.Value}</b></color>");
            return text;
        }

        public static IEnumerable<string> HelpFor(string text) =>
            string.IsNullOrEmpty(text) ? Enumerable.Empty<string>() : Keywords.Where(k => Regex.IsMatch(text, k.pattern)).Select(k => Explain(k.glossaryId));

        // Small "?" that explains a glossary entry on hover.
        public static RectTransform HelpIcon(Transform parent, string glossaryId, float size = 26)
        {
            var img = Box(parent, "Help:" + glossaryId, Mix(Panel, Warn, 0.25f), Circle, raycast: true);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            var q = Label(img.transform, "?", Mathf.RoundToInt(size * 0.7f), Warn, TextAnchor.MiddleCenter, FontStyle.Bold);
            q.rectTransform.Fill();
            img.gameObject.AddComponent<CardBattler.UI.HoverHelp>().text = Explain(glossaryId);
            return img.rectTransform;
        }

        // Destroy that also works in EditMode (tests build screens outside Play mode).
        public static void Kill(UnityEngine.Object o)
        {
            if (!o) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o);
        }

        public static void ClearChildren(Transform t)
        {
            foreach (var child in t.Cast<Transform>().ToList()) Kill(child.gameObject);
        }

        // ---- Font & sprites ----
        static Font font;
        public static Font Font => font ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static Sprite rounded, circle, ring, frame;
        public static Sprite Rounded => rounded ? rounded : rounded = MakeRounded(64, 14);
        public static Sprite Frame => frame ? frame : frame = MakeRounded(64, 14, 4); // hollow rounded border
        public static Sprite Circle => circle ? circle : circle = MakeCircle(64, 0);
        public static Sprite Ring => ring ? ring : ring = MakeCircle(128, 10);

        static Sprite MakeRounded(int size, int radius, int border = 0)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    if (border > 0)
                    {
                        // distance to the inner edge: keep only a band `border` pixels wide
                        float ix = Mathf.Min(x + 0.5f, size - x - 0.5f), iy = Mathf.Min(y + 0.5f, size - y - 0.5f);
                        float inner = dx > 0 || dy > 0 ? radius - d : Mathf.Min(ix, iy);
                        a *= Mathf.Clamp01(border - inner + 0.5f);
                    }
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        static Sprite MakeCircle(int size, int thickness)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    float a = Mathf.Clamp01(r - d);
                    if (thickness > 0) a *= Mathf.Clamp01(d - (r - thickness));
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        // ---- Builders (positions in reference pixels, origin top-left of the parent) ----
        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        // Places rt at (x, y) from the parent's top-left, with size (w, h).
        public static RectTransform Place(this RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        // Centers rt at (x, y) from the parent's top-left.
        public static RectTransform Center(this RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float pad = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        public static Image Box(Transform parent, string name, Color color, Sprite sprite = null, bool raycast = false)
        {
            var img = Node(parent, name).gameObject.AddComponent<Image>();
            img.sprite = sprite ?? Rounded;
            img.type = img.sprite == Rounded || img.sprite == Frame ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        // Plain rectangle (no sprite): backgrounds, lines, bars.
        public static Image Solid(Transform parent, string name, Color color)
        {
            var img = Node(parent, name).gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string text, int size = Body, Color? color = null, TextAnchor align = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
        {
            var t = Node(parent, "Text").gameObject.AddComponent<Text>();
            t.font = Font; t.fontSize = size; t.color = color ?? Ink; t.alignment = align; t.fontStyle = style;
            t.supportRichText = true; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.text = text;
            return t;
        }

        public static Button Button(Transform parent, string label, Action onClick, Color? color = null, int size = Body)
        {
            var img = Box(parent, "Button:" + label, color ?? PanelHi, raycast: true);
            var b = img.gameObject.AddComponent<UnityEngine.UI.Button>();
            var colors = b.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.25f, 1.25f, 1.35f); colors.pressedColor = new Color(0.8f, 0.8f, 0.85f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.7f); colors.colorMultiplier = 1.2f;
            b.colors = colors;
            var t = Label(img.transform, label, size, Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Fill(6);
            b.onClick.AddListener(() => onClick?.Invoke());
            return b;
        }

        // Horizontal bar: returns the fill image (set fillAmount) and the label.
        public static (Image fill, Text label) Bar(Transform parent, Color fillColor, int size = Small)
        {
            var bg = Box(parent, "Bar", Hex("0E1220"));
            var fill = Box(bg.transform, "Fill", fillColor, Rounded);
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.sprite = null;
            fill.rectTransform.Fill(2);
            var label = Label(bg.transform, "", size, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            label.rectTransform.Fill(8);
            label.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            return (fill, label);
        }

        // Rounded "chip" with text, sized by the caller.
        public static Text Chip(Transform parent, string text, Color color, int size = Small)
        {
            var bg = Box(parent, "Chip", Alpha(Mix(Bg, color, 0.25f), 0.95f));
            var outline = bg.gameObject.AddComponent<Outline>(); outline.effectColor = Alpha(color, 0.8f); outline.effectDistance = new Vector2(1.5f, -1.5f);
            var t = Label(bg.transform, text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Fill(4);
            return t;
        }
    }
}
