using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardBattler.UI
{
    // Procedural 2D robot built from the equipped parts: silhouette per part, color per manufacturer,
    // visual states for damage, destroyed parts, exposed chassis, armor, heat and statuses.
    public class RobotView : MonoBehaviour
    {
        public const float W = 360, H = 440;

        public class PartView
        {
            public Slot slot;
            public RectTransform rt;
            public readonly List<(Graphic g, Color c)> body = new List<(Graphic, Color)>();
            public Image crackA, crackB, brokenA, brokenB, glow;
            public Text badge;
            public bool broken;
        }

        public readonly Dictionary<Slot, PartView> parts = new Dictionary<Slot, PartView>();
        public RectTransform figure;   // flipped for robots facing left
        public RectTransform Rect => (RectTransform)transform;
        Image shield, shieldFrame, heatGlow, coreRing, coreGlow, chassisHit;
        Text armorChip, chassisLabel;
        RectTransform badges;
        bool facingLeft, animated;
        Action<Slot> onClick;
        readonly Dictionary<Slot, Coroutine> sparks = new Dictionary<Slot, Coroutine>();

        public static RobotView Create(Transform parent, bool facingLeft, bool animated)
        {
            var rt = Theme.Node(parent, "Robot");
            var v = rt.gameObject.AddComponent<RobotView>();
            v.facingLeft = facingLeft;
            v.animated = animated;
            return v;
        }

        // ---- Build ----
        public void Build(Content c, RobotDef def)
        {
            foreach (Transform child in transform) Destroy(child.gameObject);
            parts.Clear();
            foreach (var s in sparks.Values) if (s != null && Fx.I) Fx.I.StopCoroutine(s);
            sparks.Clear();

            heatGlow = Theme.Box(transform, "HeatGlow", Theme.Alpha(Theme.Hex("FF4A1F"), 0), Theme.Circle);
            heatGlow.rectTransform.Center(W / 2, H / 2, W * 1.15f, H * 1.05f);

            figure = Theme.Node(transform, "Figure").Place(0, 0, W, H);
            if (facingLeft) { figure.pivot = new Vector2(0.5f, 0.5f); figure.anchoredPosition = new Vector2(W / 2, -H / 2); figure.localScale = new Vector3(-1, 1, 1); }

            var legs = c.Part(def.legs); var torso = c.Part(def.torso); var head = c.Part(def.head);
            Part(c, Slot.Pernas, legs, 80, 280, 200, 140, DrawLegs);
            Part(c, Slot.BracoEsq, c.Part(def.armL), 0, 110, 90, 210, DrawArm);
            Part(c, Slot.Torso, torso, 90, 110, 180, 165, DrawTorso);
            Part(c, Slot.BracoDir, c.Part(def.armR), 270, 110, 90, 210, DrawArm, mirror: true);
            Part(c, Slot.Cabeca, head, 120, 10, 120, 100, DrawHead);

            // Core + chassis ring (protected/exposed).
            var core = c.Part(def.core);
            var coreColor = core.heatPerTurn > 0 ? Theme.Hex("FF5A3D") : Theme.Energy;
            coreGlow = Theme.Box(figure, "CoreGlow", Theme.Alpha(coreColor, 0.35f), Theme.Circle);
            coreGlow.rectTransform.Center(180, 195, 80, 80);
            Theme.Box(figure, "Core", coreColor, Theme.Circle).rectTransform.Center(180, 195, 42, 42);
            coreRing = Theme.Box(figure, "ChassisRing", Theme.Alpha(Theme.TextDim, 0.6f), Theme.Ring, raycast: true);
            coreRing.rectTransform.Center(180, 195, 92, 92);
            chassisHit = coreRing;
            chassisHit.gameObject.AddComponent<Clicker>().onClick = () => onClick?.Invoke(Slot.Chassi);

            shield = Theme.Box(transform, "Shield", Theme.Alpha(Theme.Armor, 0), Theme.Rounded);
            shield.rectTransform.Center(W / 2, H / 2 + 10, W + 30, H + 10);
            shieldFrame = Theme.Box(transform, "ShieldFrame", Theme.Alpha(Theme.Armor, 0), Theme.Frame);
            shieldFrame.rectTransform.Center(W / 2, H / 2 + 10, W + 30, H + 10);

            badges = Theme.Node(transform, "Badges").Place(-40, H + 6, W + 80, 40);
            var hl = badges.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8; hl.childAlignment = TextAnchor.UpperCenter; hl.childControlWidth = false; hl.childControlHeight = false; hl.childForceExpandWidth = false;

            var armor = Theme.Chip(transform, "", Theme.Armor, Theme.Small);
            armorChip = armor;
            armor.transform.parent.GetComponent<RectTransform>().Center(W / 2, -18, 190, 34);
            chassisLabel = Theme.Label(transform, "", Theme.Small, Theme.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
            chassisLabel.rectTransform.Center(W / 2, 250, 200, 26);
            chassisLabel.gameObject.SetActive(false); // the info panel and the ring already show it
            SetArmor(0); SetHeat(0); SetChassis(false, 1, 1);
        }

        void Part(Content c, Slot slot, PartDef def, float x, float y, float w, float h, Action<PartView, PartDef, float, float> draw, bool mirror = false)
        {
            var pv = new PartView { slot = slot };
            pv.rt = Theme.Node(figure, slot.ToString()).Place(x, y, w, h);
            if (mirror) { pv.rt.pivot = new Vector2(0.5f, 0.5f); pv.rt.anchoredPosition = new Vector2(x + w / 2, -(y + h / 2)); pv.rt.localScale = new Vector3(-1, 1, 1); }
            pv.glow = Theme.Box(pv.rt, "TargetGlow", Theme.Alpha(Theme.Warn, 0), Theme.Rounded, raycast: true);
            pv.glow.rectTransform.Fill(-10);
            pv.glow.gameObject.AddComponent<Clicker>().onClick = () => onClick?.Invoke(slot);
            draw(pv, def, w, h);
            pv.crackA = Shape(pv.rt, w * 0.45f, h * 0.42f, w * 0.7f, 4, Theme.Alpha(Color.black, 0), rot: 28);
            pv.crackB = Shape(pv.rt, w * 0.6f, h * 0.6f, w * 0.45f, 3, Theme.Alpha(Color.black, 0), rot: -35);
            pv.brokenA = Shape(pv.rt, w / 2, h / 2, Mathf.Min(w, h) * 0.8f, 8, Theme.Alpha(Theme.Bad, 0), rot: 45);
            pv.brokenB = Shape(pv.rt, w / 2, h / 2, Mathf.Min(w, h) * 0.8f, 8, Theme.Alpha(Theme.Bad, 0), rot: -45);
            parts[slot] = pv;
        }

        // Adds a body shape (tinted by state later). Coordinates are centers inside the part box.
        Image Body(PartView pv, float x, float y, float w, float h, Color color, Sprite sprite = null, float rot = 0)
        {
            var img = Shape(pv.rt, x, y, w, h, color, sprite, rot);
            pv.body.Add((img, color));
            return img;
        }

        static Image Shape(RectTransform parent, float x, float y, float w, float h, Color color, Sprite sprite = null, float rot = 0)
        {
            var img = Theme.Box(parent, "Shape", color, sprite ?? Theme.Rounded);
            img.rectTransform.Center(x, y, w, h);
            img.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
            return img;
        }

        static Color BodyColor(PartDef d) => Theme.Mix(Theme.Hex("3A4258"), Theme.Maker(d.maker), 0.35f);
        static Color Dark(PartDef d) => Theme.Mix(Theme.Hex("161A26"), Theme.Maker(d.maker), 0.15f);
        static Color Accent(PartDef d) => Theme.Maker(d.maker);
        static Color Elem(PartDef d) => string.IsNullOrEmpty(d.element) ? Theme.Maker(d.maker) : Theme.Element(d.element);

        void DrawHead(PartView pv, PartDef d, float w, float h)
        {
            Color b = BodyColor(d), a = Accent(d), k = Dark(d);
            switch (d.id)
            {
                case "otica_sucata":
                    Body(pv, 26, 18, 4, 34, k); Body(pv, 26, 4, 12, 12, a, Theme.Circle);
                    Body(pv, 60, 58, 92, 70, b); Body(pv, 70, 56, 46, 46, a, Theme.Circle); Body(pv, 70, 56, 20, 20, k, Theme.Circle);
                    break;
                case "sensor_k7":
                    Body(pv, 12, 46, 12, 36, k, rot: 12); Body(pv, 108, 46, 12, 36, k, rot: -12);
                    Body(pv, 60, 60, 108, 58, b); Body(pv, 60, 56, 92, 12, a);
                    break;
                case "elmo_bastion":
                    Body(pv, 60, 12, 34, 16, a); Body(pv, 60, 56, 118, 88, b); Body(pv, 60, 50, 76, 10, k); Body(pv, 60, 84, 60, 8, a);
                    break;
                case "coroa_nyx":
                    Body(pv, 32, 26, 10, 34, a, rot: 22); Body(pv, 60, 18, 10, 38, a); Body(pv, 88, 26, 10, 34, a, rot: -22);
                    Body(pv, 60, 62, 92, 76, b, Theme.Circle); Body(pv, 60, 62, 18, 18, Elem(d), Theme.Circle);
                    break;
                default:
                    Body(pv, 60, 56, 96, 76, b); Body(pv, 60, 52, 60, 12, a);
                    break;
            }
        }

        void DrawTorso(PartView pv, PartDef d, float w, float h)
        {
            Color b = BodyColor(d), a = Accent(d), k = Dark(d);
            switch (d.id)
            {
                case "carcaca_remendada":
                    Body(pv, 90, 84, 164, 146, b); Body(pv, 46, 40, 44, 32, k, rot: -6); Body(pv, 134, 128, 40, 28, k, rot: 8);
                    foreach (var (x, y) in new[] { (20f, 20f), (160f, 20f), (20f, 148f), (160f, 148f) }) Body(pv, x, y, 10, 10, a, Theme.Circle);
                    break;
                case "placa_m2":
                    Body(pv, 90, 84, 180, 158, b); Body(pv, 90, 30, 130, 12, a); Body(pv, 90, 140, 130, 12, a); Body(pv, 18, 84, 10, 120, k); Body(pv, 162, 84, 10, 120, k);
                    break;
                case "chassi_aero":
                    Body(pv, 90, 54, 166, 90, b); Body(pv, 90, 124, 112, 70, b); Body(pv, 90, 90, 6, 130, a);
                    break;
                case "torax_refrigerado":
                    Body(pv, 90, 84, 160, 146, b); Body(pv, 18, 84, 10, 120, a); Body(pv, 162, 84, 10, 120, a);
                    for (int i = 0; i < 3; i++) Body(pv, 90, 40 + i * 20, 110, 8, k);
                    break;
                default:
                    Body(pv, 90, 84, 160, 146, b);
                    break;
            }
        }

        void DrawArm(PartView pv, PartDef d, float w, float h)
        {
            Color b = BodyColor(d), a = Accent(d), k = Dark(d), e = Elem(d);
            Body(pv, 45, 24, 46, 46, b, Theme.Circle);
            Body(pv, 45, 82, 30, 84, b);
            switch (d.id)
            {
                case "punho_hidraulico": Body(pv, 45, 152, 64, 58, Theme.Mix(b, a, 0.4f)); Body(pv, 45, 128, 40, 8, a); break;
                case "serra_circular": Body(pv, 45, 160, 86, 86, a, Theme.Ring); Body(pv, 45, 160, 64, 64, Theme.Mix(b, a, 0.3f), Theme.Circle); Body(pv, 45, 160, 20, 20, k, Theme.Circle); break;
                case "canhao_30mm": Body(pv, 45, 150, 40, 40, b); Body(pv, 45, 182, 24, 70, k); Body(pv, 45, 204, 34, 12, a); break;
                case "escudo_braco": Body(pv, 45, 150, 84, 118, Theme.Mix(b, a, 0.35f)); Body(pv, 45, 150, 10, 90, a); break;
                case "lamina_plasma": Body(pv, 45, 132, 46, 10, k); Body(pv, 45, 180, 14, 96, e); Body(pv, 45, 180, 6, 90, Color.white); break;
                case "pulso_tesla": for (int i = 0; i < 3; i++) Body(pv, 45, 138 + i * 22, 46, 46, e, Theme.Ring); Body(pv, 45, 196, 14, 14, Color.white, Theme.Circle); break;
                case "injetor": Body(pv, 45, 150, 26, 74, Theme.Mix(b, e, 0.5f)); Body(pv, 45, 150, 14, 56, e); Body(pv, 45, 200, 4, 32, Theme.Ink); break;
                case "lanca_chamas": Body(pv, 20, 120, 26, 40, a); Body(pv, 45, 150, 30, 60, k); Body(pv, 45, 192, 40, 40, e, Theme.Circle); break;
                default: Body(pv, 45, 150, 50, 50, a); break;
            }
        }

        void DrawLegs(PartView pv, PartDef d, float w, float h)
        {
            Color b = BodyColor(d), a = Accent(d), k = Dark(d);
            switch (d.id)
            {
                case "rodas_entrega":
                    Body(pv, 100, 36, 160, 52, b); Body(pv, 52, 96, 58, 58, k, Theme.Circle); Body(pv, 148, 96, 58, 58, k, Theme.Circle);
                    Body(pv, 52, 96, 18, 18, a, Theme.Circle); Body(pv, 148, 96, 18, 18, a, Theme.Circle);
                    break;
                case "esteiras":
                    Body(pv, 100, 30, 140, 44, b); Body(pv, 100, 92, 196, 72, k);
                    for (int i = 0; i < 4; i++) Body(pv, 40 + i * 40, 92, 30, 30, b, Theme.Circle);
                    Body(pv, 100, 60, 150, 6, a);
                    break;
                case "pernas_digitigradas":
                    Body(pv, 100, 16, 120, 28, b);
                    Body(pv, 66, 54, 26, 64, b, rot: 22); Body(pv, 134, 54, 26, 64, b, rot: -22);
                    Body(pv, 58, 104, 22, 62, k, rot: -20); Body(pv, 142, 104, 22, 62, k, rot: 20);
                    Body(pv, 50, 134, 44, 12, a); Body(pv, 150, 134, 44, 12, a);
                    break;
                case "hover":
                    Body(pv, 100, 118, 190, 44, Theme.Alpha(a, 0.35f), Theme.Circle); Body(pv, 100, 40, 150, 46, b); Body(pv, 100, 80, 172, 26, a);
                    break;
                default:
                    Body(pv, 100, 60, 150, 90, b);
                    break;
            }
        }

        // ---- State ----
        public void SetPart(Slot slot, int hp, int max, int corrosion = 0, int weak = 0)
        {
            if (!parts.TryGetValue(slot, out var pv)) return;
            bool broken = hp <= 0;
            float dmg = max <= 0 ? 0 : 1 - Mathf.Clamp01((float)hp / max);
            foreach (var (g, c) in pv.body)
            {
                if (!g) continue;
                var grey = new Color(0.32f, 0.33f, 0.36f, c.a);
                var dim = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, c.a);
                g.color = broken ? grey : Color.Lerp(c, dim, dmg * 0.8f);
            }
            pv.crackA.color = Theme.Alpha(Color.black, dmg > 0.15f && !broken ? 0.75f : 0);
            pv.crackB.color = Theme.Alpha(Color.black, dmg > 0.5f && !broken ? 0.75f : 0);
            pv.brokenA.color = pv.brokenB.color = Theme.Alpha(Theme.Bad, broken ? 0.9f : 0);
            if (broken && !pv.broken && animated && Fx.I) sparks[slot] = Fx.Run(Sparks(pv));
            pv.broken = broken;

            string text = (corrosion > 0 ? $"COR {corrosion}" : "") + (weak > 0 ? (corrosion > 0 ? " · " : "") + $"+{weak}" : "");
            if (text.Length > 0)
            {
                if (!pv.badge)
                {
                    pv.badge = Theme.Chip(pv.rt, "", Theme.Hex("7CFF6B"), 16);
                    ((RectTransform)pv.badge.transform.parent).Center(pv.rt.rect.width / 2, 0, 110, 26);
                }
                pv.badge.text = text;
                pv.badge.color = corrosion > 0 ? Theme.Hex("7CFF6B") : Theme.Warn;
                pv.badge.transform.parent.gameObject.SetActive(true);
                Unflip(pv.badge.transform.parent);
            }
            else if (pv.badge) pv.badge.transform.parent.gameObject.SetActive(false);
        }

        IEnumerator Sparks(PartView pv)
        {
            while (pv.rt && pv.broken)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.9f, 1.8f));
                if (pv.rt && pv.broken) Fx.Burst(Fx.PosOf(pv.rt), Theme.Warn, 4, 90);
            }
        }

        public void SetChassis(bool exposed, int chassis, int max)
        {
            coreRing.color = exposed ? Theme.Alpha(Theme.Exposed, 0.95f) : Theme.Alpha(Theme.TextDim, 0.45f);
            coreGlow.enabled = true;
            chassisLabel.text = exposed ? $"CHASSI EXPOSTO {chassis}/{max}" : "";
            chassisLabel.color = Theme.Exposed;
        }

        public void SetArmor(int armor)
        {
            shield.color = Theme.Alpha(Theme.Armor, armor > 0 ? 0.06f : 0);
            shieldFrame.color = Theme.Alpha(Theme.Armor, armor > 0 ? 0.85f : 0);
            armorChip.text = armor > 0 ? $"BLINDAGEM {armor}" : "";
            armorChip.transform.parent.gameObject.SetActive(armor > 0);
        }

        // Glow from Calor 3 up, strongest at the 10+ penalty.
        public void SetHeat(int heat) => heatGlow.color = Theme.Alpha(Theme.Hex("FF4A1F"), heat < 3 ? 0 : Mathf.Clamp01(heat / 10f) * 0.4f);

        public void SetBadges(IEnumerable<(string text, Color color)> items)
        {
            foreach (Transform child in badges) Destroy(child.gameObject);
            foreach (var (text, color) in items)
            {
                var t = Theme.Chip(badges, text, color, 16);
                var rt = (RectTransform)t.transform.parent;
                rt.sizeDelta = new Vector2(t.preferredWidth + 24, 30);
            }
        }

        // Shows the live robot state (battle).
        public void Show(Robot r)
        {
            foreach (var p in r.parts.Values) SetPart(p.slot, p.hp, p.Max, p.corrosion, p.weak);
            SetChassis(r.ChassisOpen, r.chassis, r.chassisMax);
            SetArmor(r.armor);
            SetHeat(r.heat);
            var b = new List<(string, Color)>();
            if (r.heat > 0) b.Add(($"CALOR {r.heat}", r.heat >= Robot.HeatLimit ? Theme.Bad : Theme.Hex("FF6A3D")));
            if (r.overload > 0) b.Add(("SOBRECARGA", Theme.Hex("FFE14D")));
            if (r.dodge > 0) b.Add(($"ESQUIVA {r.dodge}", Theme.Hex("9EF0FF")));
            if (r.aimPending) b.Add(("MIRANDO", Theme.Warn));
            if (r.pierceNext) b.Add(("FINTA", Theme.Hex("FF8AD8")));
            SetBadges(b);
        }

        // Snapshot from a log event (used to animate the enemy turn step by step).
        public void Show(RobotState s, Robot r)
        {
            var hp = new Dictionary<Slot, int> { [Slot.Cabeca] = s.head, [Slot.Torso] = s.torso, [Slot.BracoEsq] = s.armL, [Slot.BracoDir] = s.armR, [Slot.Pernas] = s.legs };
            foreach (var p in r.parts.Values) SetPart(p.slot, hp[p.slot], p.Max);
            SetChassis(hp.Values.Any(v => v <= 0), s.chassis, r.chassisMax);
            SetArmor(s.armor);
            SetHeat(s.heat);
        }

        // Valid targets glow and can be clicked; everything else is dimmed. Pass null to clear.
        public void SetTargets(Func<Slot, bool> valid, Action<Slot> click)
        {
            onClick = click;
            foreach (var pv in parts.Values)
            {
                bool ok = valid != null && valid(pv.slot);
                pv.glow.color = Theme.Alpha(Theme.Warn, ok ? 0.28f : 0);
                pv.glow.raycastTarget = ok;
                var outline = pv.glow.GetComponent<Outline>() ?? pv.glow.gameObject.AddComponent<Outline>();
                outline.effectColor = Theme.Alpha(Theme.Warn, ok ? 0.9f : 0); outline.effectDistance = new Vector2(3, -3);
            }
            bool chassis = valid != null && valid(Slot.Chassi);
            chassisHit.raycastTarget = chassis;
            if (chassis) coreRing.color = Theme.Warn;
        }

        // Keeps text readable inside flipped robots.
        static void Unflip(Transform t)
        {
            if (t.lossyScale.x < 0) t.localScale = new Vector3(-t.localScale.x, t.localScale.y, 1);
        }

        public Vector2 PosOf(Slot slot) => slot == Slot.Chassi || !parts.ContainsKey(slot) ? Fx.PosOf(coreRing.rectTransform) : Fx.PosOf(parts[slot].rt);

        public void Hit(Slot slot)
        {
            Fx.Shake(Rect, 10, 0.22f);
            if (slot != Slot.Chassi && parts.TryGetValue(slot, out var pv))
                foreach (var (g, _) in pv.body.Take(3)) Fx.Flash(g, Color.white, 0.12f);
            else Fx.Flash(coreRing, Color.white, 0.12f);
        }
    }

    // Minimal click handler for raycastable graphics.
    public class Clicker : MonoBehaviour, IPointerClickHandler
    {
        public Action onClick;
        public void OnPointerClick(PointerEventData e) => onClick?.Invoke();
    }
}
