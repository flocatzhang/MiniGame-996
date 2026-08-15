using UnityEngine;

namespace OfficeHell.UI
{
    /// <summary>
    /// Same lifecycle names as the main client ui framework so the shape carries over:
    /// Init once, Open and Close many times, Destroy at teardown.
    /// A controller may read the model and may write the view. Never the other way round.
    /// </summary>
    public abstract class UIControllerBase
    {
        protected RectTransform Root;

        public bool IsOpen { get; private set; }

        public bool IsInitialized
        {
            get { return Root != null; }
        }

        public void UIInit(Transform parent)
        {
            if (Root != null)
            {
                return;
            }

            UIInit(UIFactory.CreatePanel(parent, GetType().Name));
        }

        public void UIInit(RectTransform root)
        {
            if (Root != null)
            {
                return;
            }

            if (root == null)
            {
                Debug.LogError("[UI] cannot initialize " + GetType().Name + " with a null prefab root");
                return;
            }

            Root = root;
            Root.gameObject.SetActive(false);
            OnUIInit();
        }

        public void UIOpen()
        {
            if (Root == null || IsOpen)
            {
                return;
            }

            IsOpen = true;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            OnUIOpen();
        }

        public void UIClose()
        {
            if (Root == null || !IsOpen)
            {
                return;
            }

            IsOpen = false;
            OnUIClose();
            Root.gameObject.SetActive(false);
        }

        public void UIDestroy()
        {
            if (Root == null)
            {
                return;
            }

            OnUIDestroy();
            Object.Destroy(Root.gameObject);
            Root = null;
            IsOpen = false;
        }

        public void UITick(float unscaledDt)
        {
            if (IsOpen)
            {
                OnUITick(unscaledDt);
            }
        }

        protected abstract void OnUIInit();

        protected virtual void OnUIOpen()
        {
        }

        protected virtual void OnUIClose()
        {
        }

        protected virtual void OnUIDestroy()
        {
        }

        protected virtual void OnUITick(float unscaledDt)
        {
        }
    }
}
