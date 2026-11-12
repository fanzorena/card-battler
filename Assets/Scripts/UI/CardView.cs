using System;
using System.Collections.Generic;
using System.Linq;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        public static RectTransform Icon(Transform parent, CardKind k, float size)
        {
            var root = Theme.Node(parent, "Icon");
            var c = ColorOf(k);
            Image S(float x, float y, float w, float h, Color col, Sprite sp = null, float rot = 0)
            {
                var i = Theme.Box(root, "s", col, sp ?? Theme.Rounded);
                i.rectTransform.Center(size * x, size * y, size * w, size * h);
                i.rectTransform.localRotation = Quaternion.Euler(0, 0, rot);
                return i;
            }
            switch (k)
            {
                case CardKind.Attack: S(0.5f, 0.5f, 0.95f, 0.16f, c, rot: 45); S(0.5f, 0.5f, 0.95f, 0.16f, c, rot: -45); break;
                case CardKind.Armor: S(0.5f, 0.45f, 0.7f, 0.65f, c); S(0.5f, 0.78f, 0.36f, 0.3f, c, rot: 45); break;
                case CardKind.Repair: S(0.5f, 0.5f, 0.8f, 0.22f, c); S(0.5f, 0.5f, 0.22f, 0.8f, c); break;
                case CardKind.Draw: S(0.42f, 0.55f, 0.5f, 0.68f, Theme.Mix(c, Theme.Bg, 0.4f), rot: 10); S(0.58f, 0.45f, 0.5f, 0.68f, c, rot: -8); break;
                case CardKind.Aim: S(0.5f, 0.5f, 0.9f, 0.9f, c, Theme.Ring); S(0.5f, 0.5f, 0.2f, 0.2f, c, Theme.Circle); break;
                case CardKind.Status: S(0.5f, 0.5f, 0.75f, 0.75f, c, Theme.Circle); S(0.5f, 0.5f, 0.35f, 0.35f, Theme.Bg, Theme.Circle); break;
                default: S(0.5f, 0.5f, 0.6f, 0.6f, c, Theme.Circle); break;
            }
            return root;
        }

        public static string TargetBadge(CardDef d, bool chooses) => !d.TargetsPart ? "" : chooses ? "ESCOLHE A PARTE" : "PARTE ALEATÓRIA";
    }

    // Floating keyword help. One per canvas.
    public static class Tooltip
    {
        static RectTransform box;
        static Text text;

        public static void Init(RectTransform overlay)
        {
            var img = Theme.Box(overlay, "Tooltip", Theme.Alpha(Theme.Bg, 0.96f));
            img.gameObject.AddComponent<Outline>().effectColor = Theme.Alpha(Theme.Line, 1);
            box = img.rectTransform;
            text = Theme.Label(box, "", Theme.Small, Theme.Ink);
            text.rectTransform.Fill(12);
            box.gameObject.SetActive(false);
        }

        // anchor: top-center of the hovered element (overlay coords). The box sits above it, or below if there is no room.
        public static void Show(Vector2 anchor, IEnumerable<string> lines, float elementHeight = 0)
        {
            var all = lines.ToList();
            if (!box || all.Count == 0) { Hide(); return; }
            text.text = string.Join("\n", all.Select(Theme.Highlight));
            float x = Mathf.Clamp(anchor.x - 240, 10, Theme.Reference.x - 490);
            box.Place(x, 0, 480, 100);
            float h = text.preferredHeight + 28;
            float y = anchor.y - h - 12;
            if (y < 10) y = anchor.y + elementHeight + 12;
            box.Place(x, Mathf.Min(y, Theme.Reference.y - h - 10), 480, h);
            box.gameObject.SetActive(true);
            box.SetAsLastSibling();
        }

        public static void Hide() { if (box) box.gameObject.SetActive(false); }
    }

    public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const float W = 200, H = 280;
        public Card card;
        public CardDef def;
        public Action<CardView> onClick, onDragStart;
        public Action<CardView, Vector2, Vector2> onDragMove, onDrop; // overlay position, screen position
        public string sourceKey;
        bool dragging;
        Transform handParent;
        public bool interactive = true;
        public bool animating; // sliding in from the deck: hover must not fight the animation
        public RectTransform Rect => (RectTransform)transform;

        Image frame, chainGlow;
        Text costText, targetText;
        CanvasGroup group;
        Vector2 home;
        bool playable = true;
        string element;

        // sourceKey: slot key of the part the card comes from (SlotIcons), e.g. "BracoEsq" or "Frame".
        public static CardView Create(Transform parent, CardDef def, string maker, string sourceKey, Card card = null, string element = null)
        {
            var rt = Theme.Node(parent, "Card:" + def.name);
            rt.sizeDelta = new Vector2(W, H);
            var v = rt.gameObject.AddComponent<CardView>();
            v.card = card; v.def = def; v.sourceKey = sourceKey; v.element = def.TargetsPart ? element : null;
            v.group = rt.gameObject.AddComponent<CanvasGroup>();
            var accent = Theme.Maker(maker);

            v.chainGlow = Theme.Box(rt, "ChainGlow", Theme.Alpha(Theme.Hex("FFD34D"), 0));
            v.chainGlow.rectTransform.Fill(-8);

            v.frame = Theme.Box(rt, "Frame", Theme.Panel, raycast: true);
            v.frame.rectTransform.Fill();
            Theme.Box(rt, "Border", Theme.Alpha(accent, 0.9f), Theme.Frame).rectTransform.Fill(-2); // border sprite: Outline copies would show through when dimmed

            var band = Theme.Box(rt, "Band", Theme.Mix(Theme.Panel, accent, 0.35f));
            band.rectTransform.Place(6, 6, W - 12, 46);
            var name = Theme.Label(rt, def.name, 21, Theme.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            name.rectTransform.Place(62, 6, W - 112, 46);
            SlotIcons.Icon(rt, sourceKey, 40, accent).Place(W - 48, 9, 40, 40);
            name.resizeTextForBestFit = true; name.verticalOverflow = VerticalWrapMode.Truncate; name.resizeTextMinSize = 14; name.resizeTextMaxSize = 21;

            var costBg = Theme.Box(rt, "Cost", Theme.Bg, Theme.Circle);
            costBg.rectTransform.Center(30, 30, 60, 60);
            Theme.Box(costBg.transform, "Ring", Theme.Energy, Theme.Ring).rectTransform.Fill();
            v.costText = Theme.Label(costBg.transform, def.cost.ToString(), 34, Theme.Energy, TextAnchor.MiddleCenter, FontStyle.Bold);
            v.costText.rectTransform.Fill();


            var kind = Kinds.Of(def);
            var art = Theme.Box(rt, "Art", Theme.Mix(Theme.Bg, Kinds.ColorOf(kind), 0.12f));
            art.rectTransform.Place(14, 76, W - 28, 92);
            Kinds.Icon(art.transform, kind, 72).Place(14, 10, 72, 72);
            var value = Theme.Label(art.transform, Kinds.Value(def), 40, Kinds.ColorOf(kind), TextAnchor.MiddleRight, FontStyle.Bold);
            value.rectTransform.Place(80, 0, W - 28 - 90, 92);
            value.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.7f);

            v.targetText = Theme.Label(rt, "", 14, Theme.Warn, TextAnchor.MiddleCenter, FontStyle.Bold);
            v.targetText.rectTransform.Place(6, 170, W - 12, 20);

            if (!string.IsNullOrEmpty(v.element))
                ((RectTransform)Theme.Chip(art.transform, v.element.ToUpperInvariant(), Theme.Element(v.element), 13).transform.parent).Place(6, 66, 96, 22);
            var body = Theme.Label(rt, Theme.Highlight(def.text), 19, Theme.Ink, TextAnchor.UpperCenter);
            body.rectTransform.Place(12, 192, W - 24, H - 198);
            return v;
        }

        // Live state in the hand.
        public void SetState(bool canPlay, int cost, string targetBadge, bool chains, bool brokenSource)
        {
            playable = canPlay;
            costText.text = cost.ToString();
            costText.color = brokenSource ? Theme.Bad : Theme.Energy;
            targetText.text = targetBadge;
            targetText.color = targetBadge.StartsWith("ESCOLHE") ? Theme.Warn : Theme.TextDim;
            group.alpha = canPlay ? 1 : 0.55f;
            chainGlow.color = Theme.Alpha(Theme.Hex("FFD34D"), chains && canPlay ? 0.55f : 0);
        }

        public void SetHome(Vector2 pos) { home = pos; Rect.anchoredPosition = pos; }

        public void OnPointerEnter(PointerEventData e)
        {
            if (dragging) return;
            Tooltip.Show(Fx.PosOf(Rect) - new Vector2(0, H * transform.lossyScale.y / transform.root.lossyScale.y / 2), new[] { "Origem: " + SlotIcons.Name(sourceKey) }
                .Concat(string.IsNullOrEmpty(element) ? new string[0] : new[] { $"{element}: {Battle.ElementEffect(element)}" })
                .Concat(Theme.HelpFor(def.text))
                .Concat(def.TargetsPart ? new[] { "Custo 1: parte aleatória. Custo 2 ou mais (ou Mirar): você escolhe a parte." } : new string[0]));
            if (!interactive || animating) return;
            transform.SetAsLastSibling();
            Fx.Move(Rect, home + new Vector2(0, 40), 0.12f);
            Fx.Scale(transform, 1.1f, 0.12f);
        }

        public void OnPointerExit(PointerEventData e)
        {
            Tooltip.Hide();
            if (!interactive || animating || dragging) return;
            Fx.Move(Rect, home, 0.12f);
            Fx.Scale(transform, 1f, 0.12f);
        }

        // ---- Drag: the card follows the pointer on the overlay; the screen decides what a drop means. ----
        public void OnBeginDrag(PointerEventData e)
        {
            if (!interactive || animating || !Fx.I) return;
            dragging = true;
            Tooltip.Hide();
            handParent = transform.parent;
            var from = Fx.PosOf(Rect);
            transform.SetParent(Fx.I.overlay, false);
            Rect.Center(from.x, from.y, W, H);
            transform.localScale = new Vector3(0.85f, 0.85f, 1);
            group.blocksRaycasts = false; // let the drop see what is under the pointer
            onDragStart?.Invoke(this);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!dragging) return;
            var p = Fx.ScreenToOverlay(e.position);
            Rect.anchoredPosition = new Vector2(p.x, -p.y);
            onDragMove?.Invoke(this, p, e.position);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!dragging) return;
            dragging = false;
            group.blocksRaycasts = true;
            onDrop?.Invoke(this, Fx.ScreenToOverlay(e.position), e.position);
        }

        // Puts a dragged card back in the hand at its home position.
        public void ReturnToHand()
        {
            if (!handParent) return;
            transform.SetParent(handParent, false);
            Rect.anchorMin = Rect.anchorMax = new Vector2(0, 1);
            Rect.pivot = new Vector2(0.5f, 0.5f);
            Rect.sizeDelta = new Vector2(W, H);
            transform.localScale = Vector3.one;
            Rect.anchoredPosition = home;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.dragging) return;
            if (interactive && playable) onClick?.Invoke(this);
            else if (interactive) Fx.Shake(Rect, 6, 0.15f);
        }

        // Card flies to an overlay point (reference pixels from top-left) and fades: played or scrapped.
        public void FlyAway(Vector2 overlayTarget, float scale = 0.6f)
        {
            interactive = false;
            Tooltip.Hide();
            if (!Fx.I || !Fx.I.overlay) { Destroy(gameObject); return; }
            var from = Fx.PosOf(Rect);
            transform.SetParent(Fx.I.overlay, false);
            Rect.Center(from.x, from.y, W, H);
            Fx.Move(Rect, new Vector2(overlayTarget.x, -overlayTarget.y), 0.3f);
            Fx.Scale(transform, scale, 0.3f);
            Fx.Fade(group, 0, 0.35f, () => Destroy(gameObject));
        }
    }
}
