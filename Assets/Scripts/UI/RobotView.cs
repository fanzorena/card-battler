using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace CardBattler.UI
{
    // 2D robot built from the equipped parts: art per part (Resources/RobotParts/<id>, procedural shapes as fallback),
    // color per manufacturer, visual states for damage, destroyed parts, exposed chassis, armor, heat and statuses.
    public class RobotView
    {
        public const float W = 360, H = 440;

        public class PartView
        {
            public Slot slot;
            public VisualElement rt;
            public readonly List<(VisualElement g, Color c, bool image)> body = new List<(VisualElement, Color, bool)>();
            public VisualElement crackA, crackB, brokenA, brokenB, glow;
            public Label badge;
            public bool broken;
        }

        public readonly Dictionary<Slot, PartView> parts = new Dictionary<Slot, PartView>();
        public VisualElement Root { get; private set; }
        public VisualElement figure;   // flipped for robots facing left
        VisualElement shield, heatGlow, coreRing, coreGlow, badges;
        Label armorChip;
        bool facingLeft, animated;
        Action<Slot> onClick;
        public bool idle = true;

        public static RobotView Create(VisualElement parent, bool facingLeft, bool animated)
        {
            var v = new RobotView { facingLeft = facingLeft, animated = animated };
            v.Root = Theme.El(parent, "Robot", "robot").Size(W, H);
            if (animated)
                v.Root.schedule.Execute(v.Idle).Every(16);
            return v;
        }

        // Places the robot centered at (x, y) of its parent, scaled.
        public RobotView At(float x, float y, float scale = 1)
        {
            Root.Center(x, y, W, H);
            Root.style.scale = new Scale(new Vector3(scale, scale, 1));
            return this;
        }

        // ---- Build ----
        public void Build(Content c, RobotDef def)
        {
            Root.Clear();
            parts.Clear();

            heatGlow = Theme.Box(Root, "HeatGlow", Theme.Alpha(Theme.Hex("FF4A1F"), 0), Shape.Circle).Center(W / 2, H / 2, W * 1.15f, H * 1.05f);
            Theme.Box(Root, "Shadow", Theme.Alpha(Color.black, 0.35f), Shape.Circle).Center(W / 2, H - 6, W * 0.75f, 34);

            figure = Theme.El(Root, "Figure").Place(0, 0, W, H);
            if (facingLeft) figure.style.scale = new Scale(new Vector3(-1, 1, 1));

            var legs = c.Part(def.legs); var torso = c.Part(def.torso); var head = c.Part(def.head);
            Part(Slot.Pernas, legs, 80, 280, 200, 140, DrawLegs);
            Part(Slot.BracoEsq, c.Part(def.armL), 0, 110, 90, 210, DrawArm);
            Part(Slot.Torso, torso, 90, 110, 180, 165, DrawTorso);
            Part(Slot.BracoDir, c.Part(def.armR), 270, 110, 90, 210, DrawArm, mirror: true);
            Part(Slot.Cabeca, head, 120, 10, 120, 100, DrawHead);

            // Slot icon next to each part: the same icon the cards and panels use.
            foreach (var (slot, x, y) in new[] { (Slot.Cabeca, 262f, 30f), (Slot.BracoEsq, -18f, 190f), (Slot.BracoDir, 378f, 190f), (Slot.Torso, 112f, 252f), (Slot.Pernas, 300f, 410f) })
            {
                var id = slot == Slot.Cabeca ? def.head : slot == Slot.Torso ? def.torso : slot == Slot.BracoEsq ? def.armL : slot == Slot.BracoDir ? def.armR : def.legs;
                SlotIcons.Icon(figure, slot, 48, Theme.Maker(c.Part(id).maker)).Center(x, y, 48, 48);
            }

            // Core + chassis ring (protected/exposed).
            var core = c.Part(def.core);
            var coreColor = core.heatPerTurn > 0 ? Theme.Hex("FF5A3D") : Theme.Energy;
            coreGlow = Theme.Box(figure, "CoreGlow", Theme.Alpha(coreColor, 0.35f), Shape.Circle).Center(180, 195, 80, 80);
            Theme.Box(figure, "Core", coreColor, Shape.Circle).Center(180, 195, 42, 42);
            coreRing = Theme.Box(figure, "ChassisRing", Theme.Alpha(Theme.TextDim, 0.6f), Shape.Ring).Center(180, 195, 92, 92);
            Theme.Border(coreRing, Theme.Alpha(Theme.TextDim, 0.6f), 5);
            coreRing.RegisterCallback<ClickEvent>(_ => onClick?.Invoke(Slot.Chassi));

            shield = Theme.Box(Root, "Shield", Theme.Alpha(Theme.Armor, 0)).Center(W / 2, H / 2 + 10, W + 30, H + 10);
            shield.AddToClassList("shield");

            badges = Theme.Row(Root, "Badges", 8).Place(-60, H + 6, W + 120, 40);
            badges.style.justifyContent = Justify.Center;

            armorChip = Theme.Chip(Root, "", Theme.Armor, Theme.Small);
            armorChip.parent.Center(W / 2, -18, 200, 36);
            SetArmor(0); SetHeat(0); SetChassis(false, 1, 1);
        }

        void Part(Slot slot, PartDef def, float x, float y, float w, float h, Action<PartView, PartDef, float, float> draw, bool mirror = false)
        {
            var pv = new PartView { slot = slot };
            pv.rt = Theme.El(figure, slot.ToString()).Place(x, y, w, h);
            if (mirror) pv.rt.style.scale = new Scale(new Vector3(-1, 1, 1));
            pv.glow = Theme.Box(pv.rt, "TargetGlow", Theme.Alpha(Theme.Warn, 0)).Fill(-10);
            pv.glow.AddToClassList("target-glow");
            pv.glow.RegisterCallback<ClickEvent>(_ => onClick?.Invoke(slot));
            // Art from Resources/RobotParts/<id>.svg (viewBox = part box); procedural shapes for parts without art.
            var art = Resources.Load<Sprite>("RobotParts/" + def.id);
            if (art) ArtBody(pv, art, w, h);
            else draw(pv, def, w, h);
            if (pv.body.Count > 0 && !pv.body[0].image) Detail(pv.body.OrderByDescending(b => b.g.style.width.value.value * b.g.style.height.value.value).First().g);
            pv.crackA = Piece(pv.rt, w * 0.45f, h * 0.42f, w * 0.7f, 4, Theme.Alpha(Color.black, 0), rot: 28);
            pv.crackB = Piece(pv.rt, w * 0.6f, h * 0.6f, w * 0.45f, 3, Theme.Alpha(Color.black, 0), rot: -35);
            pv.brokenA = Piece(pv.rt, w / 2, h / 2, Mathf.Min(w, h) * 0.8f, 8, Theme.Alpha(Theme.Bad, 0), rot: 45);
            pv.brokenB = Piece(pv.rt, w / 2, h / 2, Mathf.Min(w, h) * 0.8f, 8, Theme.Alpha(Theme.Bad, 0), rot: -45);
            parts[slot] = pv;
        }

        static void ArtBody(PartView pv, Sprite art, float w, float h)
        {
            var e = Theme.El(pv.rt, "Art").Place(0, 0, w, h);
            e.style.backgroundImage = new StyleBackground(art);
            e.style.unityBackgroundImageTintColor = Color.white;
            pv.body.Add((e, Color.white, true));
        }

        // Shading for the biggest shape of a part (children inherit its rotation): dark edge, top highlight, bottom shadow, rivets.
        static void Detail(VisualElement main)
        {
            float w = main.style.width.value.value, h = main.style.height.value.value;
            bool round = main.style.borderTopLeftRadius.value.unit == LengthUnit.Percent;
            Theme.Border(main, Theme.Alpha(Color.black, 0.45f), 2);
            if (round)
            {
                Theme.Box(main, "Shine", Theme.Alpha(Color.white, 0.12f), global::CardBattler.UI.Shape.Circle).Center(w * 0.32f, h * 0.32f, w * 0.3f, h * 0.3f);
                return;
            }
            Theme.Solid(main, "Shine", Theme.Alpha(Color.white, 0.16f)).Center(w / 2, 6, w * 0.78f, 4);
            Theme.Solid(main, "Shade", Theme.Alpha(Color.black, 0.25f)).Center(w / 2, h - 7, w * 0.84f, 6);
            if (w > 60 && h > 44)
                foreach (var sx in new[] { -1, 1 }) Theme.Box(main, "Rivet", Theme.Alpha(Color.black, 0.45f), global::CardBattler.UI.Shape.Circle).Center(w / 2 + sx * (w / 2 - 9), 9, 6, 6);
        }

        // Adds a body shape (tinted by state later). Coordinates are centers inside the part box.
        VisualElement Body(PartView pv, float x, float y, float w, float h, Color color, Shape shape = global::CardBattler.UI.Shape.Rounded, float rot = 0)
        {
            var e = Piece(pv.rt, x, y, w, h, color, shape, rot);
            pv.body.Add((e, color, false));
            return e;
        }

        static VisualElement Piece(VisualElement parent, float x, float y, float w, float h, Color color, Shape shape = global::CardBattler.UI.Shape.Rounded, float rot = 0)
        {
            var e = Theme.Box(parent, "Shape", color, shape).Center(x, y, w, h).Rotate(rot);
            if (shape == global::CardBattler.UI.Shape.Rounded) { var s = e.style; s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = Mathf.Min(10, Mathf.Min(w, h) / 3); }
            if (shape == global::CardBattler.UI.Shape.Ring) { e.AddToClassList("ring"); Theme.Border(e, color, Mathf.Max(3, Mathf.Min(w, h) * 0.12f)); }
            return e;
        }

        static Color BodyColor(PartDef d) => Theme.Mix(Theme.Hex("3A4258"), Theme.Maker(d.maker), 0.35f);
        static Color Dark(PartDef d) => Theme.Mix(Theme.Hex("161A26"), Theme.Maker(d.maker), 0.15f);
        static Color Accent(PartDef d) => Theme.Maker(d.maker);
        static Color Elem(PartDef d) => string.IsNullOrEmpty(d.element) ? Theme.Maker(d.maker) : Theme.Element(d.element);

        const Shape Circle = global::CardBattler.UI.Shape.Circle, Ring = global::CardBattler.UI.Shape.Ring, Rounded = global::CardBattler.UI.Shape.Rounded;

        void DrawHead(PartView pv, PartDef d, float w, float h)
        {
            Color b = BodyColor(d), a = Accent(d), k = Dark(d);
            switch (d.id)
            {
                case "otica_sucata":
                    Body(pv, 26, 18, 4, 34, k); Body(pv, 26, 4, 12, 12, a, Circle);
                    Body(pv, 60, 58, 92, 70, b); Body(pv, 70, 56, 46, 46, a, Circle); Body(pv, 70, 56, 20, 20, k, Circle);
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
                    Body(pv, 60, 62, 92, 76, b, Circle); Body(pv, 60, 62, 18, 18, Elem(d), Circle);
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
                    foreach (var (x, y) in new[] { (20f, 20f), (160f, 20f), (20f, 148f), (160f, 148f) }) Body(pv, x, y, 10, 10, a, Circle);
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
            Body(pv, 45, 24, 46, 46, b, Circle);
            Body(pv, 45, 82, 30, 84, b);
            switch (d.id)
            {
                case "punho_hidraulico": Body(pv, 45, 152, 64, 58, Theme.Mix(b, a, 0.4f)); Body(pv, 45, 128, 40, 8, a); break;
                case "serra_circular": Body(pv, 45, 160, 86, 86, a, Ring); Body(pv, 45, 160, 64, 64, Theme.Mix(b, a, 0.3f), Circle); Body(pv, 45, 160, 20, 20, k, Circle); break;
                case "canhao_30mm": Body(pv, 45, 150, 40, 40, b); Body(pv, 45, 182, 24, 70, k); Body(pv, 45, 204, 34, 12, a); break;
                case "escudo_braco": Body(pv, 45, 150, 84, 118, Theme.Mix(b, a, 0.35f)); Body(pv, 45, 150, 10, 90, a); break;
                case "lamina_plasma": Body(pv, 45, 132, 46, 10, k); Body(pv, 45, 180, 14, 96, e); Body(pv, 45, 180, 6, 90, Color.white); break;
                case "pulso_tesla": for (int i = 0; i < 3; i++) Body(pv, 45, 138 + i * 22, 46, 46, e, Ring); Body(pv, 45, 196, 14, 14, Color.white, Circle); break;
                case "injetor": Body(pv, 45, 150, 26, 74, Theme.Mix(b, e, 0.5f)); Body(pv, 45, 150, 14, 56, e); Body(pv, 45, 200, 4, 32, Theme.Ink); break;
                case "lanca_chamas": Body(pv, 20, 120, 26, 40, a); Body(pv, 45, 150, 30, 60, k); Body(pv, 45, 192, 40, 40, e, Circle); break;
                default: Body(pv, 45, 150, 50, 50, a); break;
            }
        }

        void DrawLegs(PartView pv, PartDef d, float w, float h)
        {
            Color b = BodyColor(d), a = Accent(d), k = Dark(d);
            switch (d.id)
            {
                case "rodas_entrega":
                    Body(pv, 100, 36, 160, 52, b); Body(pv, 52, 96, 58, 58, k, Circle); Body(pv, 148, 96, 58, 58, k, Circle);
                    Body(pv, 52, 96, 18, 18, a, Circle); Body(pv, 148, 96, 18, 18, a, Circle);
                    break;
                case "esteiras":
                    Body(pv, 100, 30, 140, 44, b); Body(pv, 100, 92, 196, 72, k);
                    for (int i = 0; i < 4; i++) Body(pv, 40 + i * 40, 92, 30, 30, b, Circle);
                    Body(pv, 100, 60, 150, 6, a);
                    break;
                case "pernas_digitigradas":
                    Body(pv, 100, 16, 120, 28, b);
                    Body(pv, 66, 54, 26, 64, b, rot: 22); Body(pv, 134, 54, 26, 64, b, rot: -22);
                    Body(pv, 58, 104, 22, 62, k, rot: -20); Body(pv, 142, 104, 22, 62, k, rot: 20);
                    Body(pv, 50, 134, 44, 12, a); Body(pv, 150, 134, 44, 12, a);
                    break;
                case "hover":
                    Body(pv, 100, 118, 190, 44, Theme.Alpha(a, 0.35f), Circle); Body(pv, 100, 40, 150, 46, b); Body(pv, 100, 80, 172, 26, a);
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
            foreach (var (g, c, image) in pv.body)
            {
                var grey = new Color(0.32f, 0.33f, 0.36f, c.a);
                var dim = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, c.a);
                var col = broken ? grey : Color.Lerp(c, dim, dmg * 0.8f);
                if (image) g.style.unityBackgroundImageTintColor = col;
                else if (g.ClassListContains("ring")) Theme.Border(g, col, g.style.borderTopWidth.value);
                else g.style.backgroundColor = col;
            }
            pv.crackA.style.backgroundColor = Theme.Alpha(Color.black, dmg > 0.15f && !broken ? 0.75f : 0);
            pv.crackB.style.backgroundColor = Theme.Alpha(Color.black, dmg > 0.5f && !broken ? 0.75f : 0);
            pv.brokenA.style.backgroundColor = pv.brokenB.style.backgroundColor = Theme.Alpha(Theme.Bad, broken ? 0.9f : 0);
            if (broken && !pv.broken && animated) Fx.Run(Sparks(pv));
            pv.broken = broken;

            string text = (corrosion > 0 ? $"COR {corrosion}" : "") + (weak > 0 ? (corrosion > 0 ? " · " : "") + $"+{weak}" : "");
            if (text.Length > 0)
            {
                if (pv.badge == null)
                {
                    // badges sit on the figure (not inside a mirrored part) so their text never reads backwards
                    pv.badge = Theme.Chip(figure, "", Theme.Hex("7CFF6B"), 16);
                    var p = pv.rt;
                    pv.badge.parent.Center(p.style.left.value.value + p.style.width.value.value / 2, p.style.top.value.value, 120, 28);
                }
                pv.badge.text = text;
                pv.badge.style.color = corrosion > 0 ? Theme.Hex("7CFF6B") : Theme.Warn;
                pv.badge.parent.Show(true);
            }
            else pv.badge?.parent.Show(false);
        }

        IEnumerator Sparks(PartView pv)
        {
            while (pv.rt.panel != null && pv.broken)
            {
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.9f, 1.8f));
                if (pv.rt.panel != null && pv.broken) Fx.Burst(Fx.PosOf(pv.rt), Theme.Warn, 4, 90);
            }
        }

        public void SetChassis(bool exposed, int chassis, int max)
        {
            Theme.Border(coreRing, exposed ? Theme.Alpha(Theme.Exposed, 0.95f) : Theme.Alpha(Theme.TextDim, 0.45f), 5);
            coreRing.EnableInClassList("exposed", exposed);
        }

        public void SetArmor(int armor)
        {
            shield.EnableInClassList("on", armor > 0);
            armorChip.text = armor > 0 ? $"BLINDAGEM {armor}" : "";
            armorChip.parent.Show(armor > 0);
        }

        // Glow from Calor 3 up, strongest at the 10+ penalty.
        public void SetHeat(int heat) => heatGlow.style.backgroundColor = Theme.Alpha(Theme.Hex("FF4A1F"), heat < 3 ? 0 : Mathf.Clamp01(heat / 10f) * 0.4f);

        public void SetBadges(IEnumerable<(string text, Color color)> items)
        {
            badges.Clear();
            foreach (var (text, color) in items) Theme.Chip(badges, text, color, 16);
            badges.Gap(8);
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

        // Valid targets glow (pulsing, via USS) and can be clicked; pass null to clear.
        public void SetTargets(Func<Slot, bool> valid, Action<Slot> click)
        {
            onClick = click;
            foreach (var pv in parts.Values)
            {
                bool ok = valid != null && valid(pv.slot);
                pv.glow.EnableInClassList("on", ok);
                pv.glow.pickingMode = ok && click != null ? PickingMode.Position : PickingMode.Ignore;
            }
            bool chassis = valid != null && valid(Slot.Chassi);
            coreRing.pickingMode = chassis && click != null ? PickingMode.Position : PickingMode.Ignore;
            coreRing.EnableInClassList("target", chassis);
        }

        // Part (or exposed chassis) under a panel point, for drag & drop.
        public Slot? SlotAt(Vector2 panelPos)
        {
            if (coreRing.worldBound.Contains(panelPos)) return Slot.Chassi;
            foreach (var s in new[] { Slot.Cabeca, Slot.BracoEsq, Slot.BracoDir, Slot.Pernas, Slot.Torso })
                if (parts.TryGetValue(s, out var pv) && pv.rt.worldBound.Contains(panelPos)) return s;
            return null;
        }

        // Idle: a slow sway and bob so the robots feel alive. Stops when defeated (Collapse takes over) or with reduced motion.
        void Idle()
        {
            if (!idle || Fx.Reduced || !Application.isPlaying || Root.panel == null) return;
            float t = Time.unscaledTime * 1.7f + (facingLeft ? 1.3f : 0);
            figure.style.rotate = new Rotate(Mathf.Sin(t) * 1.1f);
            figure.style.translate = new Translate(0, Mathf.Sin(t * 2) * 2.5f);
        }

        public Vector2 PosOf(Slot slot) => slot == Slot.Chassi || !parts.ContainsKey(slot) ? Fx.PosOf(coreRing) : Fx.PosOf(parts[slot].rt);

        public void Hit(Slot slot)
        {
            Fx.Shake(Root, 10, 0.22f);
            if (slot != Slot.Chassi && parts.TryGetValue(slot, out var pv))
            {
                foreach (var (g, _, image) in pv.body.Take(3)) if (!image) Fx.Flash(g, Color.white, 0.12f);
            }
            else Fx.Flash(coreGlow, Color.white, 0.12f);
        }
    }
}
