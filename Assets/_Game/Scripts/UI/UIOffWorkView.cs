using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    /// <summary>Serialized references for the editable between-days overlay prefab.</summary>
    public sealed class UIOffWorkView : MonoBehaviour
    {
        public Image Dimmer;
        public Button SkipButton;
        public Image BossPortrait;
        public Text DayTitle;
        public Text Speech;
        public Text Summary;
        public Text NextDay;
        public Text Hint;

        public RectTransform RectTransform
        {
            get { return transform as RectTransform; }
        }
    }
}
