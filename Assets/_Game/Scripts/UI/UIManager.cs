using OfficeHell.Core;
using OfficeHell.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    /// <summary>
    /// Builds the canvases and maps GameState onto which panels are open.
    /// The state machine never calls a panel directly, it only publishes GameStateChanged.
    /// </summary>
    public sealed class UIManager
    {
        readonly UIContext _ctx;

        public Canvas MainCanvas { get; private set; }

        public Canvas WorldTextCanvas { get; private set; }

        public Image ScreenFlash { get; private set; }

        public UIMainMenuController MainMenu { get; private set; }

        public UIHudController Hud { get; private set; }

        public UIOffWorkController OffWork { get; private set; }

        public UICardController Cards { get; private set; }

        public UIResultController Result { get; private set; }

        public UIDebugController Debug { get; private set; }

        public UIManager(UIContext ctx)
        {
            _ctx = ctx;
        }

        public void Build(Transform uiRoot)
        {
            EnsureEventSystem(uiRoot);

            WorldTextCanvas = UIFactory.CreateCanvas("Canvas_WorldText", 50, uiRoot);
            MainCanvas = UIFactory.CreateCanvas("Canvas_Main", 100, uiRoot);

            ScreenFlash = UIFactory.CreateImage(MainCanvas.transform, "ScreenFlash", new Color(1f, 1f, 1f, 0f));
            UIFactory.Stretch(ScreenFlash.rectTransform);
            ScreenFlash.enabled = false;

            UIMainMenuView mainMenuView = UIPrefabCatalog.InstantiateRequired<UIMainMenuView>(
                UIPrefabCatalog.MainMenuPath, MainCanvas.transform);
            UIHudView hudView = UIPrefabCatalog.InstantiateRequired<UIHudView>(
                UIPrefabCatalog.HudPath, MainCanvas.transform);
            UIOffWorkView offWorkView = UIPrefabCatalog.InstantiateRequired<UIOffWorkView>(
                UIPrefabCatalog.OffWorkPath, MainCanvas.transform);
            UICardPanelView cardPanelView = UIPrefabCatalog.InstantiateRequired<UICardPanelView>(
                UIPrefabCatalog.CardPanelPath, MainCanvas.transform);
            UIResultView resultView = UIPrefabCatalog.InstantiateRequired<UIResultView>(
                UIPrefabCatalog.ResultPath, MainCanvas.transform);

            MainMenu = new UIMainMenuController(mainMenuView);
            Hud = new UIHudController(_ctx, hudView);
            OffWork = new UIOffWorkController(_ctx, offWorkView);
            Cards = new UICardController(_ctx, cardPanelView);
            Result = new UIResultController(_ctx, resultView);
            Debug = new UIDebugController(_ctx);

            MainMenu.UIInit(mainMenuView.RectTransform);
            Hud.UIInit(hudView.RectTransform);
            OffWork.UIInit(offWorkView.RectTransform);
            Cards.UIInit(cardPanelView.RectTransform);
            Result.UIInit(resultView.RectTransform);

            _ctx.Game.Bus.Register(EventID.GameStateChanged, OnGameStateChanged);
            _ctx.Game.Bus.Register(EventID.CardsOffered, OnCardsOffered);
            ApplyState(_ctx.Driver.Flow.State);
        }

        public void Dispose()
        {
            _ctx.Game.Bus.Unregister(EventID.GameStateChanged, OnGameStateChanged);
            _ctx.Game.Bus.Unregister(EventID.CardsOffered, OnCardsOffered);

            MainMenu.UIDestroy();
            Hud.UIDestroy();
            OffWork.UIDestroy();
            Cards.UIDestroy();
            Result.UIDestroy();
        }

        public void Tick(float unscaledDt)
        {
            MainMenu.UITick(unscaledDt);
            Hud.UITick(unscaledDt);
            OffWork.UITick(unscaledDt);
            Cards.UITick(unscaledDt);
            Result.UITick(unscaledDt);
        }

        void OnGameStateChanged(EvtArg arg)
        {
            ApplyState((GameState)arg.I0);
        }

        /// <summary>
        /// A second level up inside the same pause reopens the hand without leaving LevelUp, so the
        /// state change alone is not enough of a signal to rebind the cards.
        /// </summary>
        void OnCardsOffered(EvtArg arg)
        {
            if (Cards.IsOpen)
            {
                Cards.Refresh();
            }
        }

        void ApplyState(GameState state)
        {
            bool inRun = state == GameState.DayStart || state == GameState.Battle ||
                         state == GameState.LevelUp || state == GameState.OffWork ||
                         state == GameState.Result;

            Toggle(MainMenu, state == GameState.MainMenu);
            Toggle(Hud, inRun);
            Toggle(Cards, state == GameState.LevelUp);
            Toggle(OffWork, state == GameState.OffWork);
            Toggle(Result, state == GameState.Result);
        }

        static void Toggle(UIControllerBase controller, bool open)
        {
            if (open)
            {
                controller.UIOpen();
            }
            else
            {
                controller.UIClose();
            }
        }

        static void EnsureEventSystem(Transform parent)
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            GameObject go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
    }
}
