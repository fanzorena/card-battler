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
        BattleLogger logger;
        GarageScreen garageScreen;
        BattleScreen battleScreen;

        public static string SavePath => Path.Combine(Application.persistentDataPath, "garage.json");
        // Editor: <projeto>/Logs/Battles. Build: persistentDataPath/Battles.
        static string LogDir => Path.GetFullPath(Path.Combine(Application.isEditor ? Path.Combine(Application.dataPath, "..", "Logs") : Application.persistentDataPath, "Battles"));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameUI>() == null) new GameObject("GameUI").AddComponent<GameUI>();
        }

        void Start()
        {
            content = Content.Load();
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
            ShowGarage();
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
            foreach (Transform t in screens) Destroy(t.gameObject);
            garageScreen = null; battleScreen = null;
        }

        public void ShowGarage()
        {
            logger?.Dispose(); // writes "Interrompida" if a battle was abandoned
            logger = null;
            battle = null;
            Clear();
            garageScreen = new GarageScreen(this, Theme.Node(screens, "Garage").Fill());
            garageScreen.Build();
        }

        public void StartBattle(RobotDef build, string enemyId)
        {
            logger?.Dispose();
            battle = new Battle(content, Environment.TickCount, build.Clone(), enemyId);
            logger = new BattleLogger(battle, LogDir);
            Clear();
            battleScreen = new BattleScreen(this, Theme.Node(screens, "Battle").Fill(), battle);
            battleScreen.Build();
        }

        void OnDestroy() => logger?.Dispose();
    }
}
