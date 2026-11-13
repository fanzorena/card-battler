using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace CardBattler.UI
{
    // Small coroutine-based effects on UI Toolkit elements. One instance lives on the UI root.
    // Positions are stage pixels (1920x1080 reference, origin top-left).
    public class Fx : MonoBehaviour
    {
        public static Fx I;
        public VisualElement overlay; // floating numbers and particles go here (above everything)

        // "Reduzir animações": no shake, particles, trails, lunges, idle motion or collapse. Floating numbers stay (they carry information).
        const string ReducedKey = "reduzirAnimacoes";
        public static bool Reduced
        {
            get => PlayerPrefs.GetInt(ReducedKey, 0) == 1;
            set { PlayerPrefs.SetInt(ReducedKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        void Awake() => I = this;

        static float Ease(float t) => 1 - Mathf.Pow(1 - Mathf.Clamp01(t), 3);
        static bool Alive(VisualElement e) => e != null && e.panel != null;

        // Effects only run in Play mode (EditMode tests build screens without animating them).
        public static Coroutine Run(IEnumerator routine) => I && Application.isPlaying ? I.StartCoroutine(routine) : null;

        public static Coroutine Delay(float seconds, Action then) => Run(DelayRoutine(seconds, then));
        static IEnumerator DelayRoutine(float s, Action then) { yield return new WaitForSeconds(s); then?.Invoke(); }

        // Animates an absolutely placed element's left/top.
        public static Coroutine Move(VisualElement e, Vector2 to, float duration, Action done = null) => Run(MoveRoutine(e, to, duration, done));
        static IEnumerator MoveRoutine(VisualElement e, Vector2 to, float d, Action done)
        {
            var from = new Vector2(e.style.left.value.value, e.style.top.value.value);
            for (float t = 0; t < d && Alive(e); t += Time.unscaledDeltaTime)
            {
                var p = Vector2.LerpUnclamped(from, to, Ease(t / d));
                e.style.left = p.x; e.style.top = p.y;
                yield return null;
            }
            e.style.left = to.x; e.style.top = to.y;
            done?.Invoke();
        }

        public static Coroutine Scale(VisualElement e, float to, float duration) => Run(ScaleRoutine(e, to, duration));
        static IEnumerator ScaleRoutine(VisualElement e, float to, float d)
        {
            float from = e.resolvedStyle.scale.value.y;
            for (float t = 0; t < d && Alive(e); t += Time.unscaledDeltaTime)
            {
                float s = Mathf.Lerp(from, to, Ease(t / d));
                e.style.scale = new Scale(new Vector3(s, s, 1));
                yield return null;
            }
            e.style.scale = new Scale(new Vector3(to, to, 1));
        }

        public static Coroutine Fade(VisualElement e, float to, float duration, Action done = null) => Run(FadeRoutine(e, to, duration, done));
        static IEnumerator FadeRoutine(VisualElement e, float to, float d, Action done)
        {
            float from = e.resolvedStyle.opacity;
            for (float t = 0; t < d && Alive(e); t += Time.unscaledDeltaTime) { e.style.opacity = Mathf.Lerp(from, to, t / d); yield return null; }
            e.style.opacity = to;
            done?.Invoke();
        }

        public static void Shake(VisualElement e, float strength = 12, float duration = 0.25f) { if (!Reduced) Run(ShakeRoutine(e, strength, duration)); }
        static IEnumerator ShakeRoutine(VisualElement e, float s, float d)
        {
            for (float t = 0; t < d && Alive(e); t += Time.unscaledDeltaTime)
            {
                var o = UnityEngine.Random.insideUnitCircle * s * (1 - t / d);
                e.style.translate = new Translate(o.x, o.y);
                yield return null;
            }
            e.style.translate = StyleKeyword.Null;
        }

        // Brief background tint (hit flash).
        public static void Flash(VisualElement e, Color color, float duration = 0.12f) => Run(FlashRoutine(e, color, duration));
        static IEnumerator FlashRoutine(VisualElement e, Color c, float d)
        {
            if (!Alive(e)) yield break;
            var original = e.style.backgroundColor;
            e.style.backgroundColor = c;
            yield return new WaitForSeconds(d);
            e.style.backgroundColor = original;
        }

        // Floating number/text at a stage point, optionally with a slot icon.
        public static void Float(Vector2 pos, string text, Color color, int size = 34, string slotIcon = null)
        {
            if (!I || I.overlay == null) return;
            var row = Theme.Row(I.overlay, "Float");
            row.AddToClassList("float");
            row.style.alignItems = Align.Center;
            if (slotIcon != null) SlotIcons.Icon(row, slotIcon, size * 1.1f, Theme.Ink).style.marginRight = 6;
            var t = Theme.Title(row, text, size, color);
            t.AddToClassList("outlined");
            row.At(pos.x, pos.y);
            row.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            Move(row, pos + new Vector2(0, -70), 0.9f);
            Run(FadeLater(row, 0.5f, 0.45f, () => row.RemoveFromHierarchy()));
            if (!Application.isPlaying) row.RemoveFromHierarchy();
        }

        static IEnumerator FadeLater(VisualElement e, float wait, float d, Action done)
        {
            yield return new WaitForSeconds(wait);
            yield return FadeRoutine(e, 0, d, done);
        }

        // Kenney spark particles bursting out of a point (part break, hits).
        public static void Burst(Vector2 pos, Color color, int count = 14, float speed = 260)
        {
            if (!I || I.overlay == null || Reduced || !Application.isPlaying) return;
            for (int i = 0; i < count; i++)
            {
                float size = UnityEngine.Random.Range(14f, 30f);
                var p = Theme.El(I.overlay, "Spark", "spark", i % 3 == 0 ? "spark-star" : "spark-dot");
                p.style.unityBackgroundImageTintColor = color;
                p.Center(pos.x, pos.y, size, size);
                p.Rotate(UnityEngine.Random.Range(0, 360));
                var dir = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(0.4f, 1f) * speed;
                Move(p, new Vector2(pos.x - size / 2, pos.y - size / 2) + dir, 0.6f);
                Run(FadeLater(p, 0.2f, 0.4f, () => p.RemoveFromHierarchy()));
            }
        }

        // Quick step toward the opponent and back (attacks).
        public static void Lunge(VisualElement e, float dx, float duration = 0.22f) { if (!Reduced) Run(LungeRoutine(e, dx, duration)); }
        static IEnumerator LungeRoutine(VisualElement e, float dx, float d)
        {
            for (float t = 0; t < d && Alive(e); t += Time.unscaledDeltaTime)
            {
                e.style.translate = new Translate(dx * Mathf.Sin(Mathf.PI * t / d), 0);
                yield return null;
            }
            e.style.translate = StyleKeyword.Null;
        }

        // A glowing shot with a short fading trail from one stage point to another.
        public static void Projectile(Vector2 from, Vector2 to, Color color, float duration = 0.2f)
        {
            if (!I || I.overlay == null || Reduced || !Application.isPlaying) return;
            Run(ProjectileRoutine(from, to, color, duration));
        }
        static IEnumerator ProjectileRoutine(Vector2 from, Vector2 to, Color color, float d)
        {
            var head = Theme.El(I.overlay, "Shot", "spark", "spark-shot");
            head.style.unityBackgroundImageTintColor = color;
            for (float t = 0; t < d; t += Time.unscaledDeltaTime)
            {
                var p = Vector2.Lerp(from, to, Ease(t / d));
                head.Center(p.x, p.y, 48, 48);
                var dot = Theme.El(I.overlay, "Trail", "spark", "spark-dot");
                dot.style.unityBackgroundImageTintColor = Theme.Alpha(color, 0.6f);
                dot.Center(p.x, p.y, 22, 22);
                Run(FadeLater(dot, 0, 0.25f, () => dot.RemoveFromHierarchy()));
                yield return null;
            }
            head.RemoveFromHierarchy();
        }

        // Defeated robot: tilts, sinks and dims.
        public static void Collapse(VisualElement e, float direction) { if (!Reduced) Run(CollapseRoutine(e, direction)); }
        static IEnumerator CollapseRoutine(VisualElement e, float dir)
        {
            const float d = 0.8f;
            for (float t = 0; t < d && Alive(e); t += Time.unscaledDeltaTime)
            {
                float k = Ease(t / d);
                e.style.rotate = new Rotate(-14 * dir * k);
                e.style.translate = new Translate(0, 60 * k);
                e.style.opacity = 1 - 0.6f * k;
                yield return null;
            }
        }

        // Endless pulse on opacity while the element is in a panel.
        public static void Pulse(VisualElement e, float min = 0.35f, float max = 1f, float period = 1f) => Run(PulseRoutine(e, min, max, period));
        static IEnumerator PulseRoutine(VisualElement e, float min, float max, float period)
        {
            yield return null; // let it attach
            while (Alive(e))
            {
                e.style.opacity = Mathf.Lerp(min, max, (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2 / period) + 1) / 2);
                yield return null;
            }
        }

        // Elements entering the screen: slide + fade, staggered by index.
        public static void Enter(VisualElement e, int index = 0, float dy = 24)
        {
            if (Reduced || !Application.isPlaying) return;
            e.style.opacity = 0;
            e.style.translate = new Translate(0, dy);
            Run(EnterRoutine(e, index * 0.05f, dy));
        }
        static IEnumerator EnterRoutine(VisualElement e, float wait, float dy)
        {
            if (wait > 0) yield return new WaitForSeconds(wait);
            const float d = 0.28f;
            for (float t = 0; t < d && Alive(e); t += Time.unscaledDeltaTime)
            {
                float k = Ease(t / d);
                e.style.opacity = k;
                e.style.translate = new Translate(0, dy * (1 - k));
                yield return null;
            }
            e.style.opacity = StyleKeyword.Null;
            e.style.translate = StyleKeyword.Null;
        }

        // Stage position of an element's center.
        public static Vector2 PosOf(VisualElement e)
        {
            if (!I || I.overlay == null || e == null || e.panel == null) return Vector2.zero;
            return I.overlay.WorldToLocal(e.worldBound.center);
        }

        // Panel position (pointer events) to stage pixels.
        public static Vector2 ToStage(Vector2 panelPos) => I && I.overlay != null ? I.overlay.WorldToLocal(panelPos) : panelPos;
    }
}
