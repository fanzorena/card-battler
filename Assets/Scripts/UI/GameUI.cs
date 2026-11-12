using System;
using System.IO;
using CardBattler.Combat;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CardBattler.UI
{
    // App root: canvas, event system, background, overlay and screen switching. Spawns itself in any scene.
    public class GameUI : MonoBehaviour
    {
        public Content content;
        public GarageSave garage;
        public RectTransform screens, overlay;
        public Battle battle;
        public string screen;          // "title", "about", "history", "garage", "opponents" or "battle"
        public string lastEnemy;       // for the rematch button
        public bool logBattles = true; // tests turn the log files off
        BattleLogger logger;
        GarageScreen garageScreen;
        BattleScreen battleScreen;

        public static string SavePath => Path.Combine(Application.persistentDataPath, "garage.json");
        // Editor: <projeto>/Logs/Battles. Build: persistentDataPath/Battles.
        public static string LogDir => Path.GetFullPath(Path.Combine(Application.isEditor ? Path.Combine(Application.dataPath, "..", "Logs") : Application.persistentDataPath, "Battles"));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameUI>() == null) new GameObject("GameUI").AddComponent<GameUI>();
        }

        void Start() => Init();

        public void Init()
        {
            content = Content.Load();
            Theme.Texts = content;
            garage = GarageSave.Load(SavePath, content);

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Theme.Reference;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var root = (RectTransform)transform;
            Background(root);
            screens = Theme.Node(root, "Screens").Fill();
            overlay = Theme.Node(root, "Overlay").Fill();
            gameObject.AddComponent<Fx>().overlay = overlay;
            Fx.I = GetComponent<Fx>();
            Tooltip.Init(overlay);
            ShowTitle();
        }

        static void Background(RectTransform root)
        {
            Theme.Solid(root, "Bg", Theme.Bg).rectTransform.Fill();
            Theme.Box(root, "Glow", Theme.Alpha(Theme.Hex("2A1C4A"), 0.55f), Theme.Circle).rectTransform.Center(960, 540, 2600, 1500);
            for (int x = 0; x <= 1920; x += 80) Theme.Solid(root, "GridV", Theme.Alpha(Theme.Line, 0.18f)).rectTransform.Place(x, 0, 1, 1080);
            for (int y = 0; y <= 1080; y += 80) Theme.Solid(root, "GridH", Theme.Alpha(Theme.Line, 0.18f)).rectTransform.Place(0, y, 1920, 1);
        }

        void Clear()
        {
            Tooltip.Hide();
            Theme.ClearChildren(screens);
            garageScreen = null; battleScreen = null;
        }

        public void ShowTitle(string page = "title")
        {
            logger?.Dispose();
            logger = null;
            battle = null;
            Clear();
            screen = page;
            new TitleScreen(this, Theme.Node(screens, "Title").Fill(), page).Build();
        }

        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ESC: quits from the title, otherwise goes back one screen. In a battle it does nothing ("Desistir" handles that).
        public void Back()
        {
            switch (screen)
            {
                case "title": Quit(); break;
                case "about": case "history": case "garage": ShowTitle(); break;
                case "opponents": ShowGarage(); break;
            }
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Back();
        }

        public void ShowOpponents()
        {
            if (GarageScreen.ContinueBlocker(content, garage.Current) != null) { ShowGarage(); return; }
            logger?.Dispose();
            logger = null;
            battle = null;
            Clear();
            screen = "opponents";
            new OpponentScreen(this, Theme.Node(screens, "Opponents").Fill()).Build();
        }

        public void ShowGarage()
        {
            logger?.Dispose(); // writes "Interrompida" if a battle was abandoned
            logger = null;
            battle = null;
            Clear();
            screen = "garage";
            garageScreen = new GarageScreen(this, Theme.Node(screens, "Garage").Fill());
            garageScreen.Build();
        }

        public void StartBattle(RobotDef build, string enemyId)
        {
            logger?.Dispose();
            battle = new Battle(content, Environment.TickCount, build.Clone(), enemyId);
            logger = logBattles ? new BattleLogger(battle, LogDir) : null;
            lastEnemy = enemyId;
            Clear();
            screen = "battle";
            battleScreen = new BattleScreen(this, Theme.Node(screens, "Battle").Fill(), battle);
            battleScreen.Build();
        }

        void OnDestroy() => logger?.Dispose();
    }
}
