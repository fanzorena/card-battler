using UnityEngine;
using UnityEngine.UIElements;

namespace CardBattler.UI
{
    // Procedural character portraits: stylized silhouettes built from the Theme shapes (original placeholder art).
    // Unknown names fall back to the initial in a ring.
    public static class Portraits
    {
        public static bool Has(string who) => Key(who) != null;

        static string Key(string who) => who switch
        {
            "Celeste" => "celeste",
            "Kiro" or "Kiro Sato" or "Kiro Sato (rival)" or "Kiro Sato (revanche)" => "kiro",
            "Glitch" => "glitch",
            "Mandíbula" or "Mandíbula, Campeão dos Fossos" => "mandibula",
            "Apostador velho" => "apostador",
            "Mecânica" => "mecanica",
            "Locutor" => "locutor",
            "Duda" => "duda",
            _ => null,
        };

        public static Color ColorOf(string who) => Key(who) switch
        {
            "celeste" => Theme.Hex("FFC24A"),
            "kiro" => Theme.Hex("4AD6FF"),
            "mandibula" => Theme.Hex("FF4A4A"),
            "apostador" => Theme.Hex("C8A27A"),
            "mecanica" => Theme.Hex("4AFFB4"),
            "locutor" => Theme.Hex("FF5AD8"),
            "duda" => Theme.Hex("C8A27A"),
            "glitch" => Theme.Hex("B45AFF"),
            _ => Theme.Warn,
        };

        // Draws the portrait as a size x size circle (position it with Place/At).
        public static VisualElement Create(VisualElement parent, string who, float size)
        {
            var c = ColorOf(who);
            var root = Theme.Box(parent, "Portrait:" + who, Theme.Mix(Theme.Bg, c, 0.18f), Shape.Circle).Size(size, size);
            root.style.overflow = Overflow.Hidden; // shapes are clipped to the circle
            var art = root;
            Color sil = Theme.Hex("10131C"), rim = Theme.Alpha(c, 0.9f);

            // S: shape centered at (x, y) in 0..1 of the square, size (w, h) in 0..1.
            VisualElement S(float x, float y, float w, float h, Color col, Shape sp = Shape.Rounded, float rot = 0) =>
                Theme.Box(art, "s", col, sp).Center(size * x, size * y, size * w, size * h).Rotate(rot);
            // Silhouette with a rim light: the same shape slightly bigger in the accent color behind it.
            void R(float x, float y, float w, float h, Shape sp = Shape.Rounded, float rot = 0)
            {
                S(x + 0.012f, y - 0.01f, w + 0.03f, h + 0.03f, rim, sp, rot);
                S(x, y, w, h, sil, sp, rot);
            }

            switch (Key(who))
            {
                case "celeste": // bun, long hair, cyber eye, earring, bar apron collar
                    R(0.5f, 0.9f, 0.86f, 0.42f);                       // shoulders
                    R(0.5f, 0.42f, 0.56f, 0.62f, Shape.Circle);         // hair mass
                    R(0.5f, 0.14f, 0.24f, 0.22f, Shape.Circle);         // bun
                    R(0.52f, 0.47f, 0.36f, 0.44f, Shape.Circle);        // face
                    S(0.6f, 0.45f, 0.12f, 0.035f, c);                   // cyber eye
                    S(0.36f, 0.6f, 0.07f, 0.07f, c, Shape.Ring);        // earring
                    S(0.5f, 0.8f, 0.2f, 0.06f, Theme.Alpha(c, 0.6f));   // collar
                    break;
                case "kiro": // spiky hair, visor, high corporate collar
                    for (int i = 0; i < 4; i++) R(0.32f + i * 0.12f, 0.2f, 0.09f, 0.3f, rot: -30 + i * 20);
                    R(0.5f, 0.92f, 0.8f, 0.36f);
                    R(0.5f, 0.74f, 0.3f, 0.2f);                         // collar
                    R(0.5f, 0.46f, 0.38f, 0.46f, Shape.Circle);
                    S(0.5f, 0.43f, 0.42f, 0.07f, c);                    // visor
                    S(0.5f, 0.43f, 0.42f, 0.02f, Color.white);
                    break;
                case "mandibula": // robot: blocky head, huge jaw with teeth, one red eye, massive shoulders
                    R(0.5f, 0.95f, 1.0f, 0.4f);
                    R(0.14f, 0.78f, 0.3f, 0.3f, Shape.Circle); R(0.86f, 0.78f, 0.3f, 0.3f, Shape.Circle);
                    R(0.5f, 0.36f, 0.5f, 0.36f);                        // head
                    R(0.5f, 0.62f, 0.62f, 0.24f);                       // jaw
                    for (int i = 0; i < 5; i++) S(0.3f + i * 0.1f, 0.56f, 0.04f, 0.06f, Theme.Alpha(c, 0.8f));
                    S(0.6f, 0.33f, 0.12f, 0.12f, c, Shape.Circle);      // eye
                    S(0.6f, 0.33f, 0.2f, 0.2f, Theme.Alpha(c, 0.3f), Shape.Circle);
                    break;
                case "apostador": // wide-brim hat, hunched, cigarette ember
                    R(0.46f, 0.92f, 0.86f, 0.4f, rot: 6);
                    R(0.48f, 0.5f, 0.36f, 0.42f, Shape.Circle);
                    R(0.48f, 0.27f, 0.62f, 0.07f);                      // brim
                    R(0.48f, 0.17f, 0.34f, 0.18f);                      // crown
                    S(0.48f, 0.22f, 0.34f, 0.03f, Theme.Alpha(c, 0.7f)); // hat band
                    S(0.7f, 0.6f, 0.16f, 0.025f, Theme.Hex("E8E1D0"));  // cigarette
                    S(0.79f, 0.6f, 0.035f, 0.035f, Theme.Hex("FF6A3D"), Shape.Circle);
                    S(0.84f, 0.52f, 0.08f, 0.08f, Theme.Alpha(Theme.Ink, 0.15f), Shape.Circle); // smoke
                    break;
                case "mecanica": // cap, goggles on the forehead, overalls strap
                    R(0.5f, 0.92f, 0.84f, 0.38f);
                    R(0.5f, 0.48f, 0.38f, 0.46f, Shape.Circle);
                    R(0.5f, 0.27f, 0.42f, 0.16f);                       // cap
                    R(0.68f, 0.32f, 0.22f, 0.05f);                      // visor
                    S(0.42f, 0.36f, 0.12f, 0.12f, c, Shape.Ring); S(0.58f, 0.36f, 0.12f, 0.12f, c, Shape.Ring);
                    S(0.36f, 0.86f, 0.05f, 0.3f, Theme.Alpha(c, 0.6f), rot: 10);
                    break;
                case "locutor": // slick hair, microphone
                    R(0.5f, 0.92f, 0.8f, 0.36f);
                    R(0.5f, 0.45f, 0.36f, 0.46f, Shape.Circle);
                    R(0.44f, 0.26f, 0.4f, 0.14f, rot: -8);              // slicked hair
                    S(0.74f, 0.66f, 0.03f, 0.22f, Theme.Hex("8A8FA3"), rot: -20);
                    S(0.7f, 0.55f, 0.1f, 0.1f, c, Shape.Circle);        // mic
                    break;
                case "glitch": // hood, glowing goggles, glitch bars across the frame
                    R(0.5f, 0.92f, 0.86f, 0.38f);
                    R(0.5f, 0.46f, 0.6f, 0.66f, Shape.Circle);          // hood
                    R(0.5f, 0.52f, 0.34f, 0.4f, Shape.Circle);          // face
                    S(0.42f, 0.46f, 0.13f, 0.08f, c); S(0.58f, 0.46f, 0.13f, 0.08f, c); // goggles
                    S(0.5f, 0.46f, 0.36f, 0.14f, Theme.Alpha(c, 0.25f));
                    S(0.3f, 0.3f, 0.3f, 0.025f, Theme.Alpha(Theme.Hex("4AD6FF"), 0.8f));
                    S(0.72f, 0.66f, 0.26f, 0.02f, Theme.Alpha(c, 0.8f));
                    S(0.36f, 0.78f, 0.18f, 0.02f, Theme.Alpha(Theme.Hex("4AD6FF"), 0.6f));
                    break;
                case "duda": // round glasses, beard, beanie, scrap-yard vest
                    R(0.5f, 0.92f, 0.9f, 0.4f);
                    R(0.5f, 0.5f, 0.4f, 0.46f, Shape.Circle);
                    R(0.5f, 0.66f, 0.34f, 0.22f, Shape.Circle);         // beard
                    R(0.5f, 0.28f, 0.42f, 0.2f, Shape.Circle);          // beanie
                    S(0.42f, 0.46f, 0.12f, 0.12f, c, Shape.Ring); S(0.58f, 0.46f, 0.12f, 0.12f, c, Shape.Ring);
                    S(0.5f, 0.46f, 0.05f, 0.015f, c);
                    S(0.3f, 0.86f, 0.06f, 0.28f, Theme.Alpha(c, 0.5f)); S(0.7f, 0.86f, 0.06f, 0.28f, Theme.Alpha(c, 0.5f));
                    break;
                default:
                    Theme.Label(art, string.IsNullOrEmpty(who) ? "?" : who.Substring(0, 1), Mathf.RoundToInt(size * 0.45f), c, TextAnchor.MiddleCenter, FontStyle.Bold).Fill();
                    break;
            }
            Theme.Box(root, "Ring", c, Shape.Ring).Fill();
            return root;
        }
    }
}
