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
    // Two modes: career (profile save: owned parts, money, jobs) and "Treino livre" (every part unlocked).
    public class GameUI : MonoBehaviour
    {
        public Content content;
        public GarageSave garage;      // Treino livre builds
        public Career career;          // loaded career (null until Jogar)
        public bool careerMode;        // which garage/flow is active
        public RectTransform screens, overlay;
        public Battle battle;
        public string screen;          // "title", "about", "history", "garage", "opponents", "map", "job", "workshop", "pit" or "battle"
        public string careerHub = "map"; // career screen the garage returns to
        public string lastEnemy;       // for the rematch button
        public Action<Battle> afterBattle; // set for career fights: their result goes back to the career
        public bool logBattles = true; // tests turn the log files off
        public string careerPath;      // tests use a temp file
        BattleLogger logger;

        public static string SavePath => Path.Combine(Application.persistentDataPath, "garage.json");
        public static string CareerPath => Path.Combine(Application.persistentDataPath, "career.json");
        // Editor: <projeto>/Logs/Battles. Build: persistentDataPath/Battles.
        public static string LogDir => Path.GetFullPath(Path.Combine(Application.isEditor ? Path.Combine(Application.dataPath, "..", "Logs") : Application.persistentDataPath, "Battles"));

        public GarageSave Garage => careerMode ? career.garage : garage;

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
            careerPath ??= CareerPath;

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

        // Leaves any battle and clears the screen.
        RectTransform Screen(string name)
        {
            logger?.Dispose(); // writes "Interrompida" if a battle was abandoned
            logger = null;
            battle = null;
            afterBattle = null;
            Tooltip.Hide();
            Theme.ClearChildren(screens);
            screen = name;
            return Theme.Node(screens, name).Fill();
        }

        public void ShowTitle(string page = "title") => new TitleScreen(this, Screen(page), page).Build();

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
                case "about": case "history": case "map": ShowTitle(); break;
                case "job": case "workshop": case "pit": ShowMap(); break;
                case "garage": if (careerMode) ShowCareer(careerHub); else ShowTitle(); break;
                case "opponents": ShowGarage(); break;
            }
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) Back();
        }

        // ---- Modes ----
        public bool HasCareer => File.Exists(careerPath);
        public Career LoadedCareer => career ??= Career.Load(careerPath, content);

        public void PlayCareer()
        {
            career ??= Career.Load(careerPath, content) ?? Career.New(content);
            careerMode = true;
            SaveCareer();
            ShowMap();
        }

        public void NewCareer()
        {
            career = Career.New(content);
            careerMode = true;
            SaveCareer();
            ShowMap();
        }

        public void FreePlay()
        {
            careerMode = false;
            ShowGarage();
        }

        public void SaveCareer() { if (career != null) career.Save(careerPath); }
        public void SaveGarage() { if (careerMode) SaveCareer(); else garage.Save(SavePath); }

        // ---- Screens ----
        public MapScreen map; // current map (tests drive its dialogue)
        public void ShowMap() { careerHub = "map"; map = new MapScreen(this, Screen("map")); map.Build(); }
        public void ShowJob() => ShowCareer("job");

        // Career places: "map", "job" (Celeste's job), "workshop" (Oficina) or "pit" (Fosso ranking).
        public void ShowCareer(string page)
        {
            if (page == "map") { ShowMap(); return; }
            careerHub = page;
            new JobScreen(this, Screen(page), page).Build();
        }

        public void ShowOpponents()
        {
            if (GarageScreen.ContinueBlocker(content, Garage.Current, careerMode ? career : null) != null) { ShowGarage(); return; }
            new OpponentScreen(this, Screen("opponents")).Build();
        }

        public void ShowGarage() => new GarageScreen(this, Screen("garage")).Build();

        // Treino livre fight.
        public void StartBattle(RobotDef build, string enemyId) => Run(new Battle(content, Environment.TickCount, build.Clone(), enemyId), enemyId, null);

        // Career job fight: damage carries over, the result goes back to the career.
        public void StartJobFight()
        {
            var enemy = career.Stage(content).enemy;
            var b = career.StartBattle(content, Environment.TickCount, enemy);
            Run(b, enemy, done =>
            {
                career.FinishJobFight(content, done, new System.Random(Environment.TickCount));
                SaveCareer();
                ShowJob();
            });
        }

        // Fosso ranking fight: losing costs only the ranking position.
        public void StartPitFight()
        {
            var enemy = career.PitStage(content).enemy;
            var b = career.StartBattle(content, Environment.TickCount, enemy);
            Run(b, enemy, done =>
            {
                career.FinishPitFight(content, done);
                SaveCareer();
                ShowCareer("pit");
            });
        }

        void Run(Battle b, string enemyId, Action<Battle> after)
        {
            var root = Screen("battle");
            battle = b;
            logger = logBattles ? new BattleLogger(battle, LogDir) : null;
            lastEnemy = enemyId;
            afterBattle = after;
            new BattleScreen(this, root, battle).Build();
        }

        void OnDestroy() => logger?.Dispose();
    }
}
