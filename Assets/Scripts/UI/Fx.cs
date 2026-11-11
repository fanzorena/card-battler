using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CardBattler.UI
{
    // Small coroutine-based effects. One instance lives on the UI root.
    public class Fx : MonoBehaviour
    {
        public static Fx I;
        public RectTransform overlay; // floating numbers and particles go here (above everything)

        void Awake() => I = this;

        static float Ease(float t) => 1 - Mathf.Pow(1 - Mathf.Clamp01(t), 3);

        // Effects only run in Play mode (EditMode tests build screens without animating them).
        public static Coroutine Run(IEnumerator routine) => I && Application.isPlaying ? I.StartCoroutine(routine) : null;

        public static Coroutine Delay(float seconds, Action then) => Run(DelayRoutine(seconds, then));
        static IEnumerator DelayRoutine(float s, Action then) { yield return new WaitForSeconds(s); then?.Invoke(); }

        public static Coroutine Move(RectTransform rt, Vector2 to, float duration, Action done = null) => Run(MoveRoutine(rt, to, duration, done));
        static IEnumerator MoveRoutine(RectTransform rt, Vector2 to, float d, Action done)
        {
            var from = rt.anchoredPosition;
            for (float t = 0; t < d && rt; t += Time.unscaledDeltaTime) { rt.anchoredPosition = Vector2.LerpUnclamped(from, to, Ease(t / d)); yield return null; }
            if (rt) rt.anchoredPosition = to;
            done?.Invoke();
        }

        public static Coroutine Scale(Transform tr, float to, float duration) => Run(ScaleRoutine(tr, to, duration));
        static IEnumerator ScaleRoutine(Transform tr, float to, float d)
        {
            var from = tr.localScale.x;
            for (float t = 0; t < d && tr; t += Time.unscaledDeltaTime) { var s = Mathf.Lerp(from, to, Ease(t / d)); tr.localScale = new Vector3(s * Mathf.Sign(tr.localScale.x), s, 1); yield return null; }
            if (tr) tr.localScale = new Vector3(to * Mathf.Sign(tr.localScale.x), to, 1);
        }

        public static Coroutine Fade(CanvasGroup g, float to, float duration, Action done = null) => Run(FadeRoutine(g, to, duration, done));
        static IEnumerator FadeRoutine(CanvasGroup g, float to, float d, Action done)
        {
            var from = g.alpha;
            for (float t = 0; t < d && g; t += Time.unscaledDeltaTime) { g.alpha = Mathf.Lerp(from, to, t / d); yield return null; }
            if (g) g.alpha = to;
            done?.Invoke();
        }

        public static void Shake(RectTransform rt, float strength = 12, float duration = 0.25f) => Run(ShakeRoutine(rt, strength, duration));
        static IEnumerator ShakeRoutine(RectTransform rt, float s, float d)
        {
            var origin = rt.anchoredPosition;
            for (float t = 0; t < d && rt; t += Time.unscaledDeltaTime)
            {
                rt.anchoredPosition = origin + UnityEngine.Random.insideUnitCircle * s * (1 - t / d);
                yield return null;
            }
            if (rt) rt.anchoredPosition = origin;
        }

        public static void Flash(Graphic g, Color color, float duration = 0.18f) => Run(FlashRoutine(g, color, duration));
        static IEnumerator FlashRoutine(Graphic g, Color c, float d)
        {
            if (!g) yield break;
            var original = g.color;
            g.color = c;
            yield return new WaitForSeconds(d);
            if (g) g.color = original;
        }

        // Floating number/text at a point of the overlay (reference pixels from top-left), optionally with a slot icon.
        public static void Float(Vector2 overlayPos, string text, Color color, int size = 34, string slotIcon = null)
        {
            if (!I || !I.overlay) return;
            var t = Theme.Label(I.overlay, text, size, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            t.rectTransform.Center(overlayPos.x, overlayPos.y, 260, 60);
            t.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.85f);
            if (slotIcon != null)
            {
                var icon = SlotIcons.Icon(t.transform, slotIcon, size * 1.1f, Theme.Ink);
                icon.Center(130 - t.preferredWidth / 2 - size * 0.7f, 30, size * 1.1f, size * 1.1f);
            }
            var g = t.gameObject.AddComponent<CanvasGroup>();
            Move(t.rectTransform, t.rectTransform.anchoredPosition + new Vector2(0, 70), 0.9f);
            Run(FadeLater(g, 0.5f, 0.45f, () => Destroy(t.gameObject)));
        }

        static IEnumerator FadeLater(CanvasGroup g, float wait, float d, Action done)
        {
            yield return new WaitForSeconds(wait);
            yield return FadeRoutine(g, 0, d, done);
        }

        // Particles bursting out of a point (part break, sparks).
        public static void Burst(Vector2 overlayPos, Color color, int count = 14, float speed = 260)
        {
            if (!I || !I.overlay) return;
            for (int i = 0; i < count; i++)
            {
                var p = Theme.Box(I.overlay, "Spark", color, Theme.Circle);
                float size = UnityEngine.Random.Range(6f, 14f);
                p.rectTransform.Center(overlayPos.x, overlayPos.y, size, size);
                var dir = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(0.4f, 1f) * speed;
                var g = p.gameObject.AddComponent<CanvasGroup>();
                Move(p.rectTransform, p.rectTransform.anchoredPosition + dir, 0.6f);
                Run(FadeLater(g, 0.2f, 0.4f, () => Destroy(p.gameObject)));
            }
        }

        // Endless pulse on a graphic's alpha while it exists and is active.
        public static void Pulse(Graphic g, float min = 0.35f, float max = 1f, float period = 1f) => Run(PulseRoutine(g, min, max, period));
        static IEnumerator PulseRoutine(Graphic g, float min, float max, float period)
        {
            while (g)
            {
                var c = g.color;
                c.a = Mathf.Lerp(min, max, (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2 / period) + 1) / 2);
                g.color = c;
                yield return null;
            }
        }

        // Screen point (pointer) to overlay reference pixels from top-left.
        public static Vector2 ScreenToOverlay(Vector2 screen)
        {
            if (!I || !I.overlay) return screen;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(I.overlay, screen, null, out var local);
            var size = I.overlay.rect.size;
            return new Vector2(local.x + size.x / 2, size.y / 2 - local.y);
        }

        // Overlay position (reference pixels from top-left) of a RectTransform's center.
        public static Vector2 PosOf(RectTransform rt)
        {
            if (!I || !I.overlay || !rt) return Vector2.zero;
            var world = rt.TransformPoint(rt.rect.center);
            var local = I.overlay.InverseTransformPoint(world);
            var size = I.overlay.rect.size;
            return new Vector2(local.x + size.x / 2, size.y / 2 - local.y);
        }
    }
}
