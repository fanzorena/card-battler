using System;
using System.Collections.Generic;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace CardBattler.UI
{
    // What a card mainly does, for its icon and the enemy intent chips.
    public enum CardKind { Attack, Armor, Draw, Repair, Aim, Status, Other }

    public static class Kinds
    {
        public static CardKind Of(CardDef d) =>
            d.TargetsPart && !d.doubleCorrosion ? CardKind.Attack
            : d.armor > 0 || d.dodge > 0 ? CardKind.Armor
            : d.repair > 0 ? CardKind.Repair
            : d.aim || d.weakPoint > 0 || d.pierceNext ? CardKind.Aim
            : d.draw > 0 ? CardKind.Draw
            : d.doubleCorrosion || d.clearHeat || d.cancelIntent ? CardKind.Status
            : CardKind.Other;

        // Big number shown on the card art / intent chip.
        public static string Value(CardDef d, int userArmor = 0) => Of(d) switch
        {
            CardKind.Attack => d.armorDamage ? d.DamageFor(userArmor).ToString() : d.hits > 1 ? $"{d.damage}×{d.hits}" : d.damage.ToString(),
            CardKind.Armor => d.armor > 0 ? d.armor.ToString() : d.dodge.ToString(),
            CardKind.Repair => "+" + d.repair,
            CardKind.Draw => "+" + d.draw,
            CardKind.Aim => d.weakPoint > 0 ? "+" + d.weakPoint : "",
            _ => "",
        };

        public static Color ColorOf(CardKind k) => k switch
        {
            CardKind.Attack => Theme.Bad,
            CardKind.Armor => Theme.Armor,
            CardKind.Repair => Theme.Good,
            CardKind.Draw => Theme.Hex("B9A6FF"),
            CardKind.Aim => Theme.Warn,
            CardKind.Status => Theme.Hex("7CFF6B"),
            _ => Theme.TextDim,
        };


        // Simple original pictogram for each kind.
        public static VisualElement Icon(VisualElement parent, CardKind k, float size)
        {
            var root = Theme.El(parent, "KindIcon").Size(size, size);
            var c = ColorOf(k);
            void S(float x, float y, float w, float h, Color col, Shape sp = Shape.Rounded, float rot = 0)
            {
                var e = Theme.Box(root, "s", col, sp).Center(size * x, size * y, size * w, size * h).Rotate(rot);
                if (sp == Shape.Rounded) { var st = e.style; st.borderTopLeftRadius = st.borderTopRightRadius = st.borderBottomLeftRadius = st.borderBottomRightRadius = size * 0.06f; }
                if (sp == Shape.Ring) Theme.Border(e, col, size * 0.1f);
            }
            switch (k)
            {
                case CardKind.Attack: S(0.5f, 0.5f, 0.95f, 0.16f, c, rot: 45); S(0.5f, 0.5f, 0.95f, 0.16f, c, rot: -45); break;
                case CardKind.Armor: S(0.5f, 0.45f, 0.7f, 0.65f, c); S(0.5f, 0.72f, 0.36f, 0.36f, c, rot: 45); break;
                case CardKind.Repair: S(0.5f, 0.5f, 0.8f, 0.22f, c); S(0.5f, 0.5f, 0.22f, 0.8f, c); break;
                case CardKind.Draw: S(0.42f, 0.55f, 0.5f, 0.68f, Theme.Mix(c, Theme.Bg, 0.4f), rot: 10); S(0.58f, 0.45f, 0.5f, 0.68f, c, rot: -8); break;
                case CardKind.Aim: S(0.5f, 0.5f, 0.9f, 0.9f, c, Shape.Ring); S(0.5f, 0.5f, 0.2f, 0.2f, c, Shape.Circle); break;
                case CardKind.Status: S(0.5f, 0.5f, 0.75f, 0.75f, c, Shape.Circle); S(0.5f, 0.5f, 0.35f, 0.35f, Theme.Bg, Shape.Circle); break;
                default: S(0.5f, 0.5f, 0.6f, 0.6f, c, Shape.Circle); break;
            }
            return root;
        }

        public static string TargetBadge(CardDef d, bool chooses) => !d.TargetsPart ? "" : chooses ? "ESCOLHE A PARTE" : "PARTE ALEATÓRIA";
    }

    // A card. In the hand it lifts on hover, can be clicked or dragged; the screen decides what a drop means.
    public class CardView
    {
        public const float W = 200, H = 280;
        public Card card;
        public CardDef def;
        public Action<CardView> onClick, onDragStart;
        public Action<CardView, Vector2, Vector2> onDragMove, onDrop; // stage position, panel position
        public string sourceKey;
        public bool interactive = true;
        public bool animating; // sliding in from the deck: hover must not fight the animation
        public VisualElement Root { get; private set; }

        VisualElement chainGlow, handParent;
        Label costText, targetText;
        Vector2 home;
        bool playable = true, dragging, pressed;
        Vector2 pressPos;
        string element;

        // sourceKey: slot key of the part the card comes from (SlotIcons), e.g. "BracoEsq" or "Frame".
        public static CardView Create(VisualElement parent, CardDef def, string maker, string sourceKey, Card card = null, string element = null)
        {
            var v = new CardView { card = card, def = def, sourceKey = sourceKey, element = def.TargetsPart ? element : null };
            var accent = Theme.Maker(maker);
            var rt = v.Root = Theme.El(parent, "Card:" + def.name, "card").Size(W, H);
            rt.pickingMode = PickingMode.Position;

            v.chainGlow = Theme.El(rt, "ChainGlow", "chain-glow").Fill(-8);
            var face = Theme.El(rt, "Face", "card-face").Fill();
            Theme.Border(face, Theme.Alpha(accent, 0.9f), 3);

            var band = Theme.Box(face, "Band", Theme.Mix(Theme.Panel, accent, 0.35f)).Place(4, 4, W - 14, 46);
            Theme.Label(band, def.name, 21, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold).Place(52, 0, W - 106, 46).FitLine(13);
            SlotIcons.Icon(face, sourceKey, 40, accent).Place(W - 52, 7, 40, 40);

            var costBg = Theme.Box(rt, "Cost", Theme.Bg, Shape.Circle).Center(26, 26, 58, 58);
            costBg.AddToClassList("cost");
            v.costText = Theme.Title(costBg, def.cost.ToString(), 30, Theme.Energy, TextAnchor.MiddleCenter);
            v.costText.Fill();

            var kind = Kinds.Of(def);
            var art = Theme.Box(face, "Art", Theme.Mix(Theme.Bg, Kinds.ColorOf(kind), 0.14f)).Place(10, 60, W - 26, 92);
            art.AddToClassList("card-art");
            Kinds.Icon(art, kind, 66).Place(12, 13, 66, 66);
            Theme.Title(art, Kinds.Value(def), 38, Kinds.ColorOf(kind), TextAnchor.MiddleRight).Classes("outlined").Place(80, 0, W - 26 - 90, 92).FitLine(22);
            if (!string.IsNullOrEmpty(v.element))
                Theme.Chip(art, v.element.ToUpperInvariant(), Theme.Element(v.element), 13).parent.At(6, 64);

            v.targetText = Theme.Label(face, "", 14, Theme.Warn, TextAnchor.MiddleCenter, FontStyle.Bold).Place(4, 154, W - 14, 20);
            Theme.Label(face, Theme.Highlight(def.text), 18, Theme.Ink, TextAnchor.UpperCenter).Place(8, 176, W - 22, H - 186).Fit(13);

            Theme.Hover(rt, v.Help);
            rt.RegisterCallback<PointerEnterEvent>(_ => v.Lift(true));
            rt.RegisterCallback<PointerLeaveEvent>(_ => v.Lift(false));
            rt.RegisterCallback<PointerDownEvent>(v.Down);
            rt.RegisterCallback<PointerMoveEvent>(v.Move);
            rt.RegisterCallback<PointerUpEvent>(v.Up);
            return v;
        }

        IEnumerable<string> Help() =>
            new[] { "Origem: " + SlotIcons.Name(sourceKey) }
                .Concat(string.IsNullOrEmpty(element) ? new string[0] : new[] { $"{element}: {Battle.ElementEffect(element)}" })
                .Concat(Theme.HelpFor(def.text))
                .Concat(def.TargetsPart ? new[] { "Custo 1: parte aleatória. Custo 2 ou mais (ou Mirar): você escolhe a parte." } : new string[0]);

        // Live state in the hand.
        public void SetState(bool canPlay, int cost, string targetBadge, bool chains, bool brokenSource)
        {
            playable = canPlay;
            costText.text = cost.ToString();
            costText.style.color = brokenSource ? Theme.Bad : Theme.Energy;
            targetText.text = targetBadge;
            targetText.style.color = targetBadge.StartsWith("ESCOLHE") ? Theme.Warn : Theme.TextDim;
            Root.EnableInClassList("unplayable", !canPlay);
            chainGlow.EnableInClassList("on", chains && canPlay);
        }

        // Home = center of the card in its hand container.
        public void SetHome(Vector2 center) { home = center; Root.Center(center.x, center.y, W, H); }

        void Lift(bool on)
        {
            if (!interactive || animating || dragging) return;
            Root.EnableInClassList("lifted", on);
            if (on) Root.BringToFront();
        }

        // ---- Pointer: click or drag (drag starts after a few pixels) ----
        void Down(PointerDownEvent e)
        {
            if (e.button != 0) return;
            pressed = true;
            pressPos = e.position;
            Root.CapturePointer(e.pointerId);
        }

        void Move(PointerMoveEvent e)
        {
            if (!pressed || !Root.HasPointerCapture(e.pointerId)) return;
            if (!dragging && interactive && !animating && Fx.I && ((Vector2)e.position - pressPos).sqrMagnitude > 64) BeginDrag();
            if (!dragging) return;
            var p = Fx.ToStage(e.position);
            Root.Center(p.x, p.y, W, H);
            onDragMove?.Invoke(this, p, e.position);
        }

        void Up(PointerUpEvent e)
        {
            if (!pressed) return;
            pressed = false;
            if (Root.HasPointerCapture(e.pointerId)) Root.ReleasePointer(e.pointerId);
            if (dragging) { EndDrag(Fx.ToStage(e.position), e.position); return; }
            if (interactive && playable) onClick?.Invoke(this);
            else if (interactive) { Fx.Shake(Root, 6, 0.15f); Sfx.Play(b => b.error, 0.5f); }
        }

        void BeginDrag()
        {
            dragging = true;
            Tooltip.Hide();
            Root.RemoveFromClassList("lifted");
            handParent = Root.parent;
            Fx.I.overlay.Add(Root);
            Root.AddToClassList("dragging");
            Sfx.Play(b => b.card, 0.5f, 1.1f);
            onDragStart?.Invoke(this);
        }

        void EndDrag(Vector2 stage, Vector2 panel)
        {
            dragging = false;
            Root.RemoveFromClassList("dragging");
            onDrop?.Invoke(this, stage, panel);
        }

        // Puts a dragged card back in the hand at its home position.
        public void ReturnToHand()
        {
            if (handParent == null) return;
            handParent.Add(Root);
            SetHome(home);
        }

        // Card flies to a stage point and fades: played or scrapped.
        public void FlyAway(Vector2 target, float scale = 0.6f)
        {
            interactive = false;
            Tooltip.Hide();
            if (!Fx.I || Fx.I.overlay == null || !Application.isPlaying) { Root.RemoveFromHierarchy(); return; }
            var from = Fx.PosOf(Root);
            Fx.I.overlay.Add(Root);
            Root.RemoveFromClassList("lifted");
            Root.Center(from.x, from.y, W, H);
            Fx.Move(Root, target - new Vector2(W / 2, H / 2), 0.3f);
            Fx.Scale(Root, scale, 0.3f);
            Fx.Fade(Root, 0, 0.35f, () => Root.RemoveFromHierarchy());
        }
    }
}
