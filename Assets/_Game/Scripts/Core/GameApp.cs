using OfficeHell.Config;
using OfficeHell.Model;
using OfficeHell.Systems;
using OfficeHell.UI;
using OfficeHell.View;
using UnityEngine;

namespace OfficeHell.Core
{
    /// <summary>
    /// Composition root and the single MonoBehaviour that drives the game.
    /// Update is the only tick in the project: no entity has its own Update, no coroutine drives
    /// gameplay, and Time.timeScale is never written.
    /// The whole world is built in code, so the scene can be an empty one and there is nothing to
    /// wire in the inspector and nothing to merge.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }

        public ConfigManager Config { get; private set; }

        public GameContext Ctx { get; private set; }

        public GameLoopDriver Driver { get; private set; }

        public UIManager Ui { get; private set; }

        public SoakRunner Soak { get; private set; }

        PoolService _pool;
        ViewBinder _binder;
        JuiceService _juice;
        AudioService _audio;
        DamageTextService _damageText;
        InputProvider _input;
        Camera _camera;
        Transform _cameraRig;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoBoot()
        {
            if (Instance != null)
            {
                return;
            }

            GameObject go = new GameObject("[GameApp]");
            go.AddComponent<GameApp>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Everything runs off GameClock, so the engine clock stays untouched for the whole session.
            Time.timeScale = 1f;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            BuildScene();
            BuildServices();
            Ui.MainMenu.OnStartClicked = OnStartClicked;
            Ui.OffWork.OnSkipClicked = OnOffWorkSkip;
            Ui.Cards.OnCardPicked = OnCardPicked;
            Ui.Result.OnRestartClicked = OnRestart;
            Ui.Result.OnMenuClicked = OnBackToMenu;

            float soakSeconds;
            if (SoakRunner.WantsSoak(out soakSeconds))
            {
                Soak = gameObject.AddComponent<SoakRunner>();
                Soak.Bind(this, _input, soakSeconds);
            }
        }

        void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            if (Ui != null)
            {
                Ui.Dispose();
            }

            if (Driver != null)
            {
                Driver.Dispose();
            }

            if (_binder != null)
            {
                _binder.Dispose();
            }

            if (_juice != null)
            {
                _juice.Dispose();
            }

            if (_audio != null)
            {
                _audio.Dispose();
            }

            if (_damageText != null)
            {
                _damageText.Dispose();
            }

            Instance = null;
        }

        // ---------- construction ----------

        void BuildScene()
        {
            GameObject rig = new GameObject("CameraRig");
            rig.transform.SetParent(transform, false);
            rig.transform.localPosition = new Vector3(0f, 0f, -10f);
            _cameraRig = rig.transform;

            // The rig carries the follow position, the camera carries only the shake offset,
            // which is why the camera sits at a zero local position.
            GameObject camGo = new GameObject("MainCamera");
            camGo.transform.SetParent(_cameraRig, false);
            camGo.transform.localPosition = Vector3.zero;
            camGo.tag = "MainCamera";

            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.13f, 0.14f, 0.17f, 1f);
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();
        }

        void BuildServices()
        {
            Config = new ConfigManager(new XmlConfigSource("Config"));
            Config.Load();

            Transform worldRoot = NewChild("World");
            Transform fxRoot = NewChild("Fx");
            Transform audioRoot = NewChild("Audio");
            Transform uiRoot = NewChild("Ui");

            _pool = new PoolService(NewChild("Pool"));

            Ctx = new GameContext();
            Ctx.Cfg = Config;
            Ctx.Run = new RunModel();
            Ctx.Bus = new EventBus();
            Ctx.Grid = new SpatialGrid();
            Ctx.Run.ResetRun(Config);

            Driver = new GameLoopDriver(Ctx);
            Driver.Camera.Bind(_camera, _cameraRig);

            _binder = new ViewBinder(Ctx, _pool, worldRoot);
            _juice = new JuiceService(Config, Ctx.Bus, _pool, fxRoot);
            _audio = new AudioService(Config, Ctx, audioRoot);

            UIContext uiCtx = new UIContext();
            uiCtx.Game = Ctx;
            uiCtx.Driver = Driver;
            uiCtx.Audio = _audio;
            uiCtx.Juice = _juice;

            Ui = new UIManager(uiCtx);
            Ui.Build(uiRoot);

            _damageText = new DamageTextService(Ctx.Bus, Config, Ui.WorldTextCanvas.transform);
            _damageText.Bind(_camera);

            _juice.Bind(_camera.transform, Ui.ScreenFlash, Ui.PieOverlay);

            _input = gameObject.AddComponent<InputProvider>();
            _input.Bind(Driver.Input, _camera);

            BuildFloor(worldRoot);
        }

        /// <summary>The office art is presentation only. Gameplay still uses the rectangular arena.</summary>
        void BuildFloor(Transform parent)
        {
            ArenaDef arena = Config.Arena;
            Sprite map = ArtCatalog.Map;
            if (map == null)
            {
                BuildGreyboxFloor(parent, arena);
                return;
            }

            GameObject go = new GameObject("Floor");
            go.transform.SetParent(parent, false);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = map;
            renderer.color = Color.white;
            renderer.sortingOrder = -120;

            float sourceHeight = Mathf.Max(0.01f, map.bounds.size.y);
            float scale = arena.HalfHeight * 2f / sourceHeight;
            go.transform.localScale = Vector3.one * scale;
        }

        /// <summary>Missing art stays playable and conspicuously falls back to the original grid.</summary>
        void BuildGreyboxFloor(Transform parent, ArenaDef arena)
        {
            GameObject go = new GameObject("Floor_GreyboxFallback");
            go.transform.SetParent(parent, false);

            const float cell = 2f;
            int cols = Mathf.CeilToInt(arena.HalfWidth / cell);
            int rows = Mathf.CeilToInt(arena.HalfHeight / cell);

            for (int y = -rows; y < rows; y++)
            {
                for (int x = -cols; x < cols; x++)
                {
                    if (((x + y) & 1) != 0)
                    {
                        continue;
                    }

                    GameObject tile = new GameObject("t");
                    tile.transform.SetParent(go.transform, false);
                    tile.transform.localPosition = new Vector3((x + 0.5f) * cell, (y + 0.5f) * cell, 0f);
                    tile.transform.localScale = Vector3.one * cell;

                    SpriteRenderer sr = tile.AddComponent<SpriteRenderer>();
                    sr.sprite = PrimitiveFactory.Get(ViewShape.Quad);
                    sr.color = new Color(0.17f, 0.18f, 0.22f, 1f);
                    sr.sortingOrder = -100;
                }
            }

            GameObject border = new GameObject("Border");
            border.transform.SetParent(go.transform, false);
            SpriteRenderer bsr = border.AddComponent<SpriteRenderer>();
            bsr.sprite = PrimitiveFactory.Get(ViewShape.Quad);
            bsr.color = new Color(0.10f, 0.10f, 0.13f, 1f);
            bsr.sortingOrder = -110;
            border.transform.localScale = new Vector3(arena.HalfWidth * 2f + 1.5f, arena.HalfHeight * 2f + 1.5f, 1f);
        }

        Transform NewChild(string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        // ---------- per frame ----------

        void Update()
        {
            HandleHotkeys();

            // The only bridge from engine time to logic time. GameClock.Scale belongs to the flow
            // machine and GameClock.FxScale belongs to JuiceService, this call just advances it.
            GameClock.Tick(Time.unscaledDeltaTime);
            Driver.Tick();
        }

        void LateUpdate()
        {
            float unscaled = Time.unscaledDeltaTime;
            _binder.Sync(unscaled);
            _juice.Tick(unscaled);
            _audio.Tick(unscaled);
            _damageText.Tick(unscaled);
            Ui.Tick(unscaled);
        }

        void OnGUI()
        {
            Ui.Debug.DrawGui();
        }

        void HandleHotkeys()
        {
            if (Input.GetKeyDown(KeyCode.F1))
            {
                Ui.Debug.Visible = !Ui.Debug.Visible;
            }

            if (Input.GetKeyDown(KeyCode.F5))
            {
                ReloadConfig();
            }

            if (Input.GetKeyDown(KeyCode.R) && Driver.Flow.State != GameState.MainMenu)
            {
                OnRestart();
            }

            if (Driver.Flow.State == GameState.LevelUp)
            {
                HandleCardKeys();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Driver.Flow.State == GameState.MainMenu)
                {
                    Application.Quit();
                }
                else
                {
                    OnBackToMenu();
                }
            }
        }

        /// <summary>
        /// Number keys for the card hand. Reaching for the mouse mid run to pick a card breaks the one
        /// input the whole game is built on, so the keyboard path is not optional.
        /// </summary>
        void HandleCardKeys()
        {
            int count = Ui.Cards.CardCount;
            for (int i = 0; i < count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    OnCardPicked(i);
                    return;
                }
            }
        }

        // ---------- commands ----------

        /// <summary>
        /// Reparses the xml and re-resolves every live reference. Highest value tool in the whole
        /// validation phase: pacing numbers can be tuned without leaving play mode.
        /// </summary>
        public void ReloadConfig()
        {
            Config.Load();
            Ctx.Run.RebindDefs(Config);
            Driver.Spawn.OnDayBegin();
            Driver.Camera.Bind(_camera, _cameraRig);
            Ctx.Bus.Dispatch(EventID.ConfigReloaded);
        }

        void OnStartClicked()
        {
            StartRun();
        }

        void OnOffWorkSkip()
        {
            if (!Driver.Flow.CanSkipOffWork)
            {
                return;
            }

            _audio.Play(AudioService.SfxUiClick);
            Driver.Flow.SkipOffWork();
        }

        void OnCardPicked(int index)
        {
            Driver.Cards.Pick(index);
            Driver.Flow.ResolveLevelUp();
        }

        void OnRestart()
        {
            StartRun();
        }

        void OnBackToMenu()
        {
            _audio.Play(AudioService.SfxUiClick);
            ResetWorld();
            Driver.Flow.GoMainMenu();
        }

        /// <summary>Zero frame restart: no scene reload, the run model and the view pools are recycled.</summary>
        public void StartRun()
        {
            ResetWorld();
            Driver.Flow.StartRun();
            EquipStartingWeapon();
            Driver.Camera.SnapToPlayer();
            Driver.ForceRebuildGrid();
        }

        void ResetWorld()
        {
            Ctx.Run.ResetRun(Config);
            _binder.RecycleAll();
            Ctx.Grid.Clear();
            GameClock.Reset();
        }

        void EquipStartingWeapon()
        {
            WeaponDef def = Config.Weapon("stapler");
            if (def == null && Config.WeaponOrder.Count > 0)
            {
                def = Config.Weapon(Config.WeaponOrder[0]);
            }

            if (def != null)
            {
                Ctx.Run.Player.Equip(0, def, Quality.White);
            }
        }
    }
}
