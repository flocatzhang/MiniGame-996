using System;
using OfficeHell.View;
using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    /// <summary>Required prefab paths plus optional card-icon lookup.</summary>
    public static class UIPrefabCatalog
    {
        public const string MainMenuPath = "Prefabs/UIMainMenu";
        public const string HudPath = "Prefabs/UIHud";
        public const string OffWorkPath = "Prefabs/UIOffWork";
        public const string CardPanelPath = "Prefabs/UICardPanel";
        public const string CardItemPath = "Prefabs/UICardItem";
        public const string ResultPath = "Prefabs/UIResult";
        public const string CardIconRoot = "Icons/Cards/";

        public static T InstantiateRequired<T>(string path, Transform parent) where T : Component
        {
            T prefab = Resources.Load<T>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException("Required UI prefab is missing from Resources: " + path);
            }

            T instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            ApplyRuntimeFont(instance.transform);
            return instance;
        }

        public static Sprite CardIcon(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            return Resources.Load<Sprite>(CardIconRoot + key);
        }

        public static void ApplyRuntimeFont(Transform root)
        {
            Text[] labels = root.GetComponentsInChildren<Text>(true);
            Font font = FontProvider.Font;
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i].font = font;
            }
        }
    }
}
