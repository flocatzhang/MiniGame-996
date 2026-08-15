using OfficeHell.View;
using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    /// <summary>
    /// Builds UGUI hierarchies in code. Sprite-backed images still avoid prefab and scene merge
    /// conflicts while allowing the art catalog to replace individual generated elements.
    /// </summary>
    public static class UIFactory
    {
        public static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform CreatePanel(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            Stretch(rt);
            return rt;
        }

        public static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            Image img = go.AddComponent<Image>();
            img.sprite = PrimitiveFactory.Pixel;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image CreateSpriteImage(
            Transform parent,
            string name,
            Sprite sprite,
            Color color,
            bool preserveAspect)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        public static Text CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            Color color,
            TextAnchor anchor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            Text text = go.AddComponent<Text>();
            text.font = FontProvider.Font;
            text.fontSize = fontSize;
            text.color = color;
            text.text = content;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string label,
            int fontSize,
            Vector2 size,
            Color background,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;

            Image img = go.AddComponent<Image>();
            img.sprite = PrimitiveFactory.Pixel;
            img.color = background;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;

            if (onClick != null)
            {
                btn.onClick.AddListener(onClick);
            }

            Text text = CreateText(go.transform, "Label", label, fontSize, Color.white, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return btn;
        }

        /// <summary>Background plus a horizontally filled foreground. Returns the fill image.</summary>
        public static Image CreateBar(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            Color background,
            Color fill,
            out Image backgroundImage)
        {
            GameObject holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            RectTransform rt = holder.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;

            backgroundImage = CreateImage(holder.transform, "Bg", background);
            Stretch(backgroundImage.rectTransform);

            Image fillImage = CreateImage(holder.transform, "Fill", fill);
            Stretch(fillImage.rectTransform);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 1f;
            return fillImage;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static RectTransform Anchor(
            RectTransform rt,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
            return rt;
        }

        public static Text AnchoredText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            Color color,
            TextAnchor alignment,
            Vector2 anchor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            Text text = CreateText(parent, name, content, fontSize, color, alignment);
            Anchor(text.rectTransform, anchor, anchoredPosition, size);
            return text;
        }
    }
}
