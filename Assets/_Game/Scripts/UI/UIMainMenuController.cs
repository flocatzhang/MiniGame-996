using System;
using UnityEngine;

namespace OfficeHell.UI
{
    /// <summary>The prefab-backed opening screen with an independently editable start button.</summary>
    public sealed class UIMainMenuController : UIControllerBase
    {
        readonly UIMainMenuView _view;

        public Action OnStartClicked;

        public UIMainMenuController(UIMainMenuView view)
        {
            _view = view;
        }

        protected override void OnUIInit()
        {
            _view.StartButton.onClick.AddListener(StartGame);
        }

        protected override void OnUIDestroy()
        {
            _view.StartButton.onClick.RemoveListener(StartGame);
        }

        void StartGame()
        {
            if (OnStartClicked != null)
            {
                OnStartClicked();
            }
        }
    }
}
