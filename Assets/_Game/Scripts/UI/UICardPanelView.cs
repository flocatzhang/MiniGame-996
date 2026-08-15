using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    /// <summary>Serialized references for the three-card selection panel.</summary>
    public sealed class UICardPanelView : MonoBehaviour
    {
        public Image Dimmer;
        public Text Title;
        public RectTransform CardContainer;
        public UICardView CardPrefab;

        public RectTransform RectTransform
        {
            get { return transform as RectTransform; }
        }
    }
}
