using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace CardBattler.UI
{
    public enum Shape { Rect, Rounded, Circle, Ring, Frame }

    // Single source for the look: palette, manufacturer/element colors, glossary and UI Toolkit builders.
    // Static look (fonts, Kenney panels/buttons, hover/press states) lives in Resources/UI/Theme.uss;
    // the builders only set what depends on data (colors, sizes, positions).
    public static class Theme
    {
        public static readonly Vector2 Reference = new Vector2(1920, 1080);

        public static readonly Color Bg = Hex("0B0D14"), Bg2 = Hex("141826"), Panel = Hex("181D2C"), PanelHi = Hex("222943"),
            Line = Hex("2E3756"), Ink = Hex("E6E9F2"), TextDim = Hex("9AA2BC"), Good = Hex("5BE38A"), Bad = Hex("FF5A6A"),
            Warn = Hex("FFC24A"), Armor = Hex("5AA8FF"), Exposed = Hex("FF8A3D"), Energy = Hex("FFE066"), Paper = Hex("1D1A16");

        // Minimum readable sizes at 1080p: nothing below Small.
        public const int Small = 18, Body = 22, Heading = 30, Huge = 64;

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
        public static VisualElement HelpIcon(VisualElement parent, string glossaryId, float size = 26)
        {
            var e = Box(parent, "Help:" + glossaryId, Mix(Panel, Warn, 0.25f), Shape.Circle).Size(size, size);
            e.AddToClassList("help");
            Label(e, "?", Mathf.RoundToInt(size * 0.7f), Warn, TextAnchor.MiddleCenter, FontStyle.Bold).Fill();
            Hover(e, Explain(glossaryId));
            return e;
        }

        // Tooltip with fixed lines while the pointer is over e.
        public static T Hover<T>(T e, params string[] lines) where T : VisualElement => Hover(e, () => lines);

        public static T Hover<T>(T e, Func<IEnumerable<string>> lines) where T : VisualElement
        {
            e.pickingMode = PickingMode.Position;
            e.RegisterCallback<PointerEnterEvent>(_ => Tooltip.Show(e, lines()));
            e.RegisterCallback<PointerLeaveEvent>(_ => Tooltip.Hide());
            return e;
        }

        // ---- Assets ----
        static Font font, display;
        public static Font Font => font ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        static StyleSheet sheet;
        public static StyleSheet Sheet => sheet ? sheet : sheet = Resources.Load<StyleSheet>("UI/Theme");

        // Destroy that also works in EditMode (tests build screens outside Play mode).
        public static void Kill(UnityEngine.Object o)
        {
            if (!o) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o); else UnityEngine.Object.DestroyImmediate(o);
        }

        public static void ClearChildren(VisualElement e) => e?.Clear();

        // ---- Layout helpers (reference pixels, origin top-left of the parent) ----
        public static VisualElement El(VisualElement parent, string name = null, params string[] classes)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            foreach (var c in classes) e.AddToClassList(c);
            parent?.Add(e);
            return e;
        }

        public static VisualElement Node(VisualElement parent, string name) => El(parent, name);

        public static T Place<T>(this T e, float x, float y, float w, float h) where T : VisualElement
        {
            var s = e.style;
            s.position = Position.Absolute;
            s.left = x; s.top = y; s.width = w; s.height = h;
            return e;
        }

        // Absolute position only; size comes from content.
        public static T At<T>(this T e, float x, float y) where T : VisualElement
        {
            e.style.position = Position.Absolute;
            e.style.left = x; e.style.top = y;
            return e;
        }

        public static T Center<T>(this T e, float x, float y, float w, float h) where T : VisualElement => e.Place(x - w / 2, y - h / 2, w, h);

        public static T Size<T>(this T e, float w, float h) where T : VisualElement
        {
            e.style.width = w; e.style.height = h; e.style.flexShrink = 0;
            return e;
        }

        public static T Fill<T>(this T e, float pad = 0) where T : VisualElement
        {
            var s = e.style;
            s.position = Position.Absolute;
            s.left = pad; s.top = pad; s.right = pad; s.bottom = pad;
            return e;
        }

        public static T Rotate<T>(this T e, float degrees) where T : VisualElement
        {
            if (degrees != 0) e.style.rotate = new Rotate(degrees);
            return e;
        }

        public static T Classes<T>(this T e, params string[] classes) where T : VisualElement
        {
            foreach (var c in classes) e.AddToClassList(c);
            return e;
        }

        public static T Show<T>(this T e, bool visible) where T : VisualElement
        {
            e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            return e;
        }

        public static VisualElement Row(VisualElement parent, string name = null, float gap = 0)
        {
            var e = El(parent, name, "row");
            if (gap > 0) e.Gap(gap);
            return e;
        }

        public static VisualElement Column(VisualElement parent, string name = null, float gap = 0)
        {
            var e = El(parent, name, "col");
            if (gap > 0) e.Gap(gap);
            return e;
        }

        // Spacing between flex children (margin on all but the first, recomputed on layout changes).
        public static T Gap<T>(this T e, float gap) where T : VisualElement
        {
            bool row = e.ClassListContains("row");
            void Apply()
            {
                for (int i = 0; i < e.childCount; i++)
                {
                    if (row) e[i].style.marginLeft = i == 0 ? 0 : gap;
                    else e[i].style.marginTop = i == 0 ? 0 : gap;
                }
            }
            e.RegisterCallback<GeometryChangedEvent>(_ => Apply());
            e.schedule.Execute(Apply);
            return e;
        }

        // Vertical scroll area that fills its parent (content goes in .contentContainer via Add).
        public static ScrollView Scroll(VisualElement parent, string name = "Scroll")
        {
            var s = new ScrollView(ScrollViewMode.Vertical) { name = name, horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            s.AddToClassList("scroll");
            s.mouseWheelScrollSize = 60;
            parent.Add(s);
            return s;
        }

        // ---- Visual builders ----
        public static VisualElement Box(VisualElement parent, string name, Color color, Shape shape = Shape.Rounded, bool raycast = false)
        {
            var e = El(parent, name);
            e.pickingMode = raycast ? PickingMode.Position : PickingMode.Ignore;
            Paint(e, color, shape);
            return e;
        }

        public static void Paint(VisualElement e, Color color, Shape shape)
        {
            var s = e.style;
            float r = shape == Shape.Circle || shape == Shape.Ring ? 9999 : shape == Shape.Rect ? 0 : 10;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = new Length(r == 9999 ? 50 : r, r == 9999 ? LengthUnit.Percent : LengthUnit.Pixel);
            if (shape == Shape.Ring || shape == Shape.Frame)
            {
                s.backgroundColor = Color.clear;
                Border(e, color, shape == Shape.Ring ? 3 : 2);
            }
            else s.backgroundColor = color;
        }

        public static void Border(VisualElement e, Color color, float width)
        {
            var s = e.style;
            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = width;
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = color;
        }

        public static void Tint(VisualElement e, Color color)
        {
            if (e.style.backgroundColor.value.a > 0 || e.resolvedStyle.borderTopWidth <= 0) e.style.backgroundColor = color;
            else Border(e, color, e.resolvedStyle.borderTopWidth);
        }

        // Plain rectangle: backgrounds, lines, bars.
        public static VisualElement Solid(VisualElement parent, string name, Color color) => Box(parent, name, color, Shape.Rect);

        // Kenney sci-fi panel (USS .panel, .panel-glass, .paper...), optionally tinted.
        public static VisualElement Plate(VisualElement parent, string name, string kind = "panel", Color? tint = null)
        {
            var e = El(parent, name, kind);
            if (tint.HasValue) e.style.unityBackgroundImageTintColor = tint.Value;
            return e;
        }

        public static Label Label(VisualElement parent, string text, int size = Body, Color? color = null, TextAnchor align = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
        {
            var t = new Label(text) { pickingMode = PickingMode.Ignore, enableRichText = true };
            t.AddToClassList("text");
            var s = t.style;
            s.fontSize = size;
            s.color = color ?? Ink;
            s.unityTextAlign = align;
            s.unityFontStyleAndWeight = style;
            s.whiteSpace = WhiteSpace.Normal;
            parent?.Add(t);
            return t;
        }

        // Display font (Kenney Future) for titles, numbers and buttons.
        public static Label Title(VisualElement parent, string text, int size = 44, Color? color = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            var t = Label(parent, text, size, color, align, FontStyle.Normal);
            t.AddToClassList("display");
            return t;
        }

        // One line that never overflows its box: ellipsis plus the full text as a tooltip.
        public static Label OneLine(this Label t)
        {
            t.AddToClassList("one-line");
            t.style.whiteSpace = WhiteSpace.NoWrap;
            t.style.overflow = Overflow.Hidden;
            t.style.textOverflow = TextOverflow.Ellipsis;
            return t;
        }

        // Shrinks the font (down to min) until the text fits its box in one or more lines; needs a fixed-size box.
        public static Label Fit(this Label t, int min)
        {
            int max = (int)t.style.fontSize.value.value;
            // One step per frame: the measurement uses the resolved size, which updates between frames.
            bool Step()
            {
                var box = t.contentRect.size;
                int cur = (int)t.resolvedStyle.fontSize;
                if (box.x <= 0 || box.y <= 0 || cur <= min) return true;
                var need = t.MeasureTextSize(t.text, box.x, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
                if (need.y <= box.y + 1) return true;
                t.style.fontSize = Mathf.Max(min, Mathf.Min(cur - 1, Mathf.FloorToInt(cur * Mathf.Sqrt(box.y / need.y))));
                return false;
            }
            t.RegisterCallback<GeometryChangedEvent>(e =>
            {
                if (e.oldRect.size == e.newRect.size) return;
                t.style.fontSize = max;
                bool done = false;
                t.schedule.Execute(() => done = Step()).Every(16).Until(() => done);
            });
            return t;
        }

        // One line, shrinking the font (down to min) until it fits the box width; ellipsis below that.
        public static Label FitLine(this Label t, int min)
        {
            t.OneLine();
            int max = (int)t.style.fontSize.value.value;
            // text width scales linearly with the font size: measure at the resolved size, scale down, re-check next frame
            bool Step()
            {
                float w = t.contentRect.width;
                int cur = (int)t.resolvedStyle.fontSize;
                if (w <= 0 || cur <= min) return true;
                float need = t.MeasureTextSize(t.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
                if (need <= w + 1) return true;
                t.style.fontSize = Mathf.Max(min, Mathf.Min(cur - 1, Mathf.FloorToInt(cur * w / need)));
                return false;
            }
            t.RegisterCallback<GeometryChangedEvent>(e =>
            {
                if (e.oldRect.size == e.newRect.size) return;
                t.style.fontSize = max;
                bool done = false;
                t.schedule.Execute(() => done = Step()).Every(16).Until(() => done);
            });
            return t;
        }

        public static Button Button(VisualElement parent, string label, Action onClick, Color? color = null, int size = Body, string variant = null)
        {
            var b = new Button { name = "Button:" + label };
            b.AddToClassList("btn");
            if (variant != null) b.AddToClassList(variant);
            b.style.fontSize = size;
            // The label is a child so arrows can be Kenney icons (Kenney Future has no arrow glyphs).
            b.Add(new Label { name = "Text", pickingMode = PickingMode.Ignore, enableRichText = true }.Classes("btn-text"));
            SetLabel(b, label);
            if (color.HasValue) b.style.unityBackgroundImageTintColor = color.Value;
            b.userData = onClick;
            b.clicked += () => { if (b.enabledSelf) { Sfx.Click(); onClick?.Invoke(); } };
            b.RegisterCallback<PointerEnterEvent>(_ => { if (b.enabledSelf) Sfx.Hover(); });
            parent?.Add(b);
            return b;
        }

        public static string LabelOf(Button b) => b.Q<Label>("Text")?.text;

        // Sets a button's text; "← X", "X →", "X ▸", "◀" and "▶" become Kenney arrow icons around the text.
        public static void SetLabel(Button b, string label)
        {
            b.Query(className: "btn-icon").ForEach(e => e.RemoveFromHierarchy());
            var text = b.Q<Label>("Text");
            label = label ?? "";
            string left = null, right = null;
            if (label == "◀") { left = "arrowLeft"; label = ""; }
            else if (label == "▶") { right = "arrowRight"; label = ""; }
            if (label.StartsWith("←")) { left = "arrowLeft"; label = label.Substring(1).Trim(); }
            if (label.EndsWith("→") || label.EndsWith("▸")) { right = "arrowRight"; label = label.Substring(0, label.Length - 1).Trim(); }
            text.text = label;
            text.Show(label.Length > 0);
            float s = Mathf.Max(16, b.style.fontSize.value.value * 0.9f);
            if (left != null) b.Insert(0, Icon(null, left, s, Color.white).Classes("btn-icon"));
            if (right != null) b.Add(Icon(null, right, s, Color.white).Classes("btn-icon"));
        }

        // Primary call-to-action (orange Kenney button).
        public static Button Primary(VisualElement parent, string label, Action onClick, int size = 30) =>
            Button(parent, label, onClick, null, size, "btn-primary");

        // Runs a button's action as a click would (tests and keyboard shortcuts).
        public static void Press(Button b) { if (b != null && b.enabledSelf) (b.userData as Action)?.Invoke(); }

        // Pointer click on any element (cards, map places, opponents).
        public static T OnClick<T>(this T e, Action action) where T : VisualElement
        {
            e.pickingMode = PickingMode.Position;
            e.AddToClassList("clickable");
            e.userData = action;
            e.RegisterCallback<ClickEvent>(_ => { Sfx.Click(); action(); });
            e.RegisterCallback<PointerEnterEvent>(_ => Sfx.Hover());
            return e;
        }

        public static void Click(VisualElement e)
        {
            if (e is Button b) Press(b);
            else (e?.userData as Action)?.Invoke();
        }

        // Horizontal bar: returns the fill (set with SetFill) and the label.
        public static (VisualElement fill, Label label) Bar(VisualElement parent, Color fillColor, int size = Small)
        {
            var bg = El(parent, "Bar", "bar");
            var fill = El(bg, "Fill", "bar-fill");
            fill.style.backgroundColor = fillColor;
            var label = Label(bg, "", size, Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            label.AddToClassList("bar-label");
            label.Fill();
            label.style.paddingLeft = 10;
            return (fill, label);
        }

        public static void SetFill(VisualElement fill, float amount) => fill.style.width = Length.Percent(Mathf.Clamp01(amount) * 100);

        // Rounded "chip" with text, sized by its content unless placed.
        public static Label Chip(VisualElement parent, string text, Color color, int size = Small)
        {
            var bg = El(parent, "Chip", "chip");
            bg.style.backgroundColor = Alpha(Mix(Bg, color, 0.25f), 0.95f);
            Border(bg, Alpha(color, 0.85f), 2);
            var t = Label(bg, text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.style.whiteSpace = WhiteSpace.NoWrap;
            return t;
        }

        // Kenney pictogram (GameIcons, mapped in Theme.uss as .icon-<name>) tinted.
        public static VisualElement Icon(VisualElement parent, string name, float size, Color color)
        {
            var e = El(parent, "Icon:" + name, "icon", "icon-" + name);
            e.style.unityBackgroundImageTintColor = color;
            e.Size(size, size);
            return e;
        }
    }
}
