using System;
using System.IO;
using CardBattler.Combat;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace CardBattler.UI
{
    // App root: UI Toolkit document, background, 1920x1080 stage (screens + overlay) and screen switching. Spawns itself in any scene.
    // Two modes: career (profile save: owned parts, money, jobs) and "Treino livre" (every part unlocked).
    public class GameUI : MonoBehaviour
    {
        public Content content;
        public GarageSave garage;      // Treino livre builds
        public Career career;          // loaded career (null until Jogar)
        public bool careerMode;        // which garage/flow is active
        public VisualElement Root, stage, screens, overlay;
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

            Root = new VisualElement { name = "App" };
            Root.AddToClassList("app");
            if (Theme.Sheet) Root.styleSheets.Add(Theme.Sheet);
            Root.style.unityFont = Theme.Font;
            Root.Fill();
            Background(Root);
            // Fixed 1920x1080 stage centered in the panel (PanelSettings: scale with screen size, expand).
            stage = Theme.El(Root, "Stage", "stage");
            screens = Theme.El(stage, "Screens").Fill();
            overlay = Theme.El(stage, "Overlay").Fill();
            var doc = gameObject.GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            if (!doc.panelSettings) doc.panelSettings = Resources.Load<PanelSettings>("UI/PanelSettings");
            doc.rootVisualElement?.Add(Root);
            gameObject.AddComponent<Fx>().overlay = overlay;
            Fx.I = GetComponent<Fx>();
            Tooltip.Init(overlay);
            ShowTitle();
        }

        static void Background(VisualElement root)
        {
            var bg = Theme.El(root, "Bg", "bg").Fill();
            Theme.El(bg, "Grid", "bg-grid").Fill();
            Theme.El(bg, "Glow", "bg-glow").Fill();
        }

        // Buttons on screen (tests and shortcuts find them by name).
        public List<Button> Buttons => Root.Query<Button>().ToList();
        public Button ButtonNamed(string name) => Root.Q<Button>(name);
        public VisualElement Named(string name) => Root.Q(name);

        // Leaves any battle and clears the screen.
        VisualElement Screen(string name)
        {
            logger?.Dispose(); // writes "Interrompida" if a battle was abandoned
            logger = null;
            battle = null;
            afterBattle = null;
            Tooltip.Hide();
            screens.Clear();
            overlay.Query(className: "float").ForEach(e => e.RemoveFromHierarchy());
            screen = name;
            var root = Theme.El(screens, name, "screen").Fill();
            Fx.Enter(root, 0, 16);
            return root;
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
                case "job": case "workshop": case "pit": case "ferro": case "circuit": ShowMap(); break;
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
        public void StartBattle(RobotDef build, string enemyId) => Run(new Battle(content, Environment.TickCount, build.Clone(), enemyId, false, content.Robot(enemyId).terrain), enemyId, null);

        // Career job fight: damage carries over, the result goes back to the career.
        public void StartJobFight()
        {
            var enemy = career.Stage(content).enemy;
            var b = career.StartBattle(content, Environment.TickCount, enemy, Career.ArenaFor(career.Stage(content), Career.Arena));
            Run(b, enemy, done =>
            {
                career.FinishJobFight(content, done, new System.Random(Environment.TickCount));
                SaveCareer();
                ShowJob();
            });
        }

        // Fosso ranking fight: losing costs only the ranking position.
        public void StartPitFight() => StartLadderFight("pit");

        // Ranking fight (Fosso or Circuito): losing costs only the ranking position.
        public void StartLadderFight(string ladder)
        {
            var enemy = career.LadderStage(content, ladder).enemy;
            var b = career.StartBattle(content, Environment.TickCount, enemy, Career.ArenaFor(career.LadderStage(content, ladder), Career.ArenaOf(ladder)));
            Run(b, enemy, done =>
            {
                career.FinishLadderFight(content, done, ladder);
                SaveCareer();
                ShowCareer(ladder);
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
