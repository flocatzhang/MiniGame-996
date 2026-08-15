using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    /// <summary>Serialized references for the editable settlement prefab.</summary>
    public sealed class UIResultView : MonoBehaviour
    {
        public Image Dimmer;
        public Text Outcome;
        public Text Stamp;

        public GameObject SalaryGroup;
        public Text Salary;

        public GameObject WorkGroup;
        public Text[] WorkLabels;
        public Text[] WorkValues;

        public GameObject LootGroup;
        public Text BestQuality;
        public Text Rank;
        public Text San;
        public Text Loadout;
        public Text Comment;

        public GameObject KpiGroup;
        public Text KpiLabel;
        public Image KpiFill;

        public GameObject ButtonsGroup;
        public Button RestartButton;
        public Button MenuButton;

        public RectTransform RectTransform
        {
            get { return transform as RectTransform; }
        }
    }
}
