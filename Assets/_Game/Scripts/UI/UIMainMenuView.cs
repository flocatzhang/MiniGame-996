using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    /// <summary>Serialized references for the editable main-menu prefab.</summary>
    public sealed class UIMainMenuView : MonoBehaviour
    {
        public RawImage Background;
        public Button StartButton;
        [FormerlySerializedAs("StartHighlight")]
        public Image StartButtonImage;
        public Text StartButtonLabel;

        public RectTransform RectTransform
        {
            get { return transform as RectTransform; }
        }
    }
}
