using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    /// <summary>Serialized references for the editable battle HUD prefab.</summary>
    public sealed class UIHudView : MonoBehaviour
    {
        [Serializable]
        public sealed class WeaponSlotReferences
        {
            public Image Background;
            public Image CooldownFill;
            public Image QualityLight;
            public Image Icon;
            public Text Label;
        }

        [Serializable]
        public sealed class ArmorSlotReferences
        {
            public Image Background;
            public Image QualityLight;
            public Image Icon;
            public Text Label;
        }

        public Image Portrait;
        public Text NameText;
        public Text RankText;
        public Image SanFill;
        public Text SanText;
        public Image ExpFill;
        public Text ExpText;
        public Text CoinText;
        public Text KillText;
        public GameObject SkillRoot;
        public Image SkillBackground;
        public Image SkillIcon;
        public Image SkillFill;
        public Text SkillText;

        public RectTransform StatusBar;
        public Image AttackSlowStatus;
        public Image InvincibleStatus;
        public Image ShieldStatus;
        public Image MoveSlowStatus;
        public Image TreatStatus;

        [FormerlySerializedAs("CountdownText")]
        public Text WorkClockText;
        public Text StageText;

        public Image KpiFill;
        public Text KpiText;

        public WeaponSlotReferences[] WeaponSlots;
        public ArmorSlotReferences[] ArmorSlots;
        public Sprite GreenLightSprite;
        public Sprite BlueLightSprite;
        public Sprite PurpleLightSprite;
        public Sprite OrangeLightSprite;

        public GameObject BossRoot;
        public Text BossName;
        public Image BossFill;
        public Image[] BossPips;

        public Text BannerText;

        public RectTransform RectTransform
        {
            get { return transform as RectTransform; }
        }
    }
}
