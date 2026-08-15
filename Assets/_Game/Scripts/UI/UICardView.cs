using System;
using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    /// <summary>Serialized references for one reusable reward card.</summary>
    public sealed class UICardView : MonoBehaviour
    {
        [Serializable]
        public sealed class CardAccentEntry
        {
            public string Key;
            public Color Color;

            public CardAccentEntry(string key, Color color)
            {
                Key = key;
                Color = color;
            }
        }

        public Button Button;
        public Image Frame;
        public Outline Border;
        public Image Accent;
        public Image Footer;
        public Image IconPlate;
        public Image Icon;
        public Text IconFallback;
        public Text Kind;
        public Text Title;
        public Text Primary;
        public Text Description;
        public Text FooterText;
        public Text KeyHint;
        public GameObject RecommendBadge;
        public GameObject NewBadge;
        public CardAccentEntry[] DesignAccents;

        public RectTransform RectTransform
        {
            get { return transform as RectTransform; }
        }
    }
}
