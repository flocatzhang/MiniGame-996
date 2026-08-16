using System;
using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Systems;
using UnityEngine;

namespace OfficeHell.UI
{
    /// <summary>Rebinds three prefab card instances for every level-up hand.</summary>
    public sealed class UICardController : UIControllerBase
    {
        const int MaxCards = 3;

        readonly UIContext _ctx;
        readonly UICardPanelView _view;
        readonly UICardView[] _cards = new UICardView[MaxCards];
        readonly UnityEngine.Events.UnityAction[] _clickHandlers = new UnityEngine.Events.UnityAction[MaxCards];

        public Action<int> OnCardPicked;

        public UICardController(UIContext ctx, UICardPanelView view)
        {
            _ctx = ctx;
            _view = view;
        }

        protected override void OnUIInit()
        {
            for (int i = 0; i < MaxCards; i++)
            {
                int index = i;
                UICardView card = UnityEngine.Object.Instantiate(_view.CardPrefab, _view.CardContainer);
                card.name = "Card" + (i + 1);
                card.gameObject.SetActive(false);
                UIPrefabCatalog.ApplyRuntimeFont(card.transform);

                _clickHandlers[i] = () => Pick(index);
                card.Button.onClick.AddListener(_clickHandlers[i]);
                _cards[i] = card;
            }
        }

        protected override void OnUIDestroy()
        {
            for (int i = 0; i < MaxCards; i++)
            {
                if (_cards[i] != null && _clickHandlers[i] != null)
                {
                    _cards[i].Button.onClick.RemoveListener(_clickHandlers[i]);
                }
            }
        }

        protected override void OnUIOpen()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (Root == null)
            {
                return;
            }

            List<CardOffer> offers = _ctx.Driver.Cards.Offers;
            int count = Mathf.Min(offers.Count, MaxCards);
            int recommendedIndex = RecommendedEquipmentIndex(offers, count);
            int pending = _ctx.Game.Run.Player.PendingLevelUps;
            _view.Title.text = pending > 1
                ? "选择你的奖励（剩余 " + pending + " 次）"
                : "选择你的奖励";

            for (int i = 0; i < MaxCards; i++)
            {
                bool used = i < count;
                _cards[i].gameObject.SetActive(used);
                if (used)
                {
                    Fill(_cards[i], offers[i], i, i == recommendedIndex);
                }
            }
        }

        static int RecommendedEquipmentIndex(List<CardOffer> offers, int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            Quality sharedQuality = offers[0].Quality;
            bool allSameQuality = true;
            for (int i = 1; i < count; i++)
            {
                if (offers[i].Quality != sharedQuality)
                {
                    allSameQuality = false;
                    break;
                }
            }

            if (allSameQuality)
            {
                return -1;
            }

            int recommendedIndex = -1;
            int highestQuality = -1;

            for (int i = 0; i < count; i++)
            {
                CardOffer offer = offers[i];
                int quality = (int)offer.Quality;
                if (offer.Kind != CardKind.Equipment || quality <= highestQuality)
                {
                    continue;
                }

                recommendedIndex = i;
                highestQuality = quality;
            }

            return recommendedIndex;
        }

        void Fill(UICardView card, CardOffer offer, int index, bool recommended)
        {
            card.Kind.text = KindWord(offer.Kind);
            card.Title.text = offer.Title;
            card.Primary.text = Primary(offer);
            card.FooterText.text = Footer(offer);
            card.KeyHint.text = "按 " + (index + 1);

            // Identity stays on the authored accents while quality selects one of the four card-frame
            // sprites. Keeping the frame white preserves the delivered artwork instead of tinting it.
            Color identity = Identity(card, offer);
            Color quality = _ctx.Game.Cfg.QualityOf(offer.Quality).Color;

            card.Frame.sprite = QualityFrame(card, offer.Quality);
            card.Frame.color = Color.white;
            if (card.Border != null)
            {
                card.Border.effectColor = quality;
            }

            card.Accent.color = identity;
            card.Footer.color = quality;
            card.IconPlate.color = Color.Lerp(Color.white, identity, 0.18f);
            card.Title.color = Color.Lerp(new Color(0.09f, 0.1f, 0.16f), identity, 0.42f);
            card.Kind.color = Color.Lerp(new Color(0.18f, 0.2f, 0.25f), quality, 0.68f);

            string iconKey = offer.Kind == CardKind.Equipment ? offer.DefId : offer.Id;
            Sprite sprite = UIPrefabCatalog.CardIcon(iconKey);
            card.Icon.sprite = sprite;
            card.Icon.enabled = sprite != null;
            card.IconFallback.gameObject.SetActive(sprite == null);
            card.IconFallback.text = IconFallback(iconKey);

            card.RecommendBadge.SetActive(recommended);
        }

        /// <summary>
        /// The description already carries the rolled amount, because the value and the sentence
        /// describing it come out of one template. Appending the number again is how this line used
        /// to render 攻击力 +6 as "攻击力 +6 +6".
        /// </summary>
        static string Primary(CardOffer offer)
        {
            return offer.Desc;
        }

        Color Identity(UICardView card, CardOffer offer)
        {
            if (offer.Kind == CardKind.Equipment)
            {
                return _ctx.Game.Cfg.QualityOf(offer.Quality).Color;
            }

            if (card.DesignAccents != null)
            {
                for (int i = 0; i < card.DesignAccents.Length; i++)
                {
                    UICardView.CardAccentEntry entry = card.DesignAccents[i];
                    if (entry != null && entry.Key == offer.Id)
                    {
                        return entry.Color;
                    }
                }
            }

            return offer.Kind == CardKind.Skill
                ? new Color(0.55f, 1f, 0.75f, 1f)
                : new Color(0.55f, 0.85f, 1f, 1f);
        }

        static string KindWord(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Stat: return "数值卡";
                case CardKind.Skill: return "技能卡";
                default: return "装备卡";
            }
        }

        static string Footer(CardOffer offer)
        {
            switch (offer.Kind)
            {
                case CardKind.Stat: return "基础成长";
                case CardKind.Skill: return "强化摸鱼";
                default: return "高效输出";
            }
        }

        static Sprite QualityFrame(UICardView card, Quality quality)
        {
            switch (quality)
            {
                case Quality.Blue: return card.BlueFrameSprite;
                case Quality.Purple: return card.PurpleFrameSprite;
                case Quality.Orange: return card.OrangeFrameSprite;
                default: return card.GreenFrameSprite;
            }
        }

        static string QualityName(Quality quality)
        {
            switch (quality)
            {
                case Quality.Blue: return "蓝色";
                case Quality.Purple: return "紫色";
                case Quality.Orange: return "橙色";
                default: return "绿色";
            }
        }

        static string IconFallback(string key)
        {
            switch (key)
            {
                case "c_atk": return "ATK";
                case "c_atk_pct": return "%ATK";
                case "c_haste": return "SPD";
                case "c_crit": return "CRIT";
                case "c_critdmg": return "DMG";
                case "c_def": return "DEF";
                case "c_dodge": return "闪";
                case "c_san": return "SAN";
                case "c_speed": return "MOVE";
                case "c_luck": return "LUCK";
                case "c_magnet": return "PICK";
                case "s_deep": return "深";
                case "s_paid": return "休";
                case "s_reverse": return "反";
                case "s_extra": return "生";
                case "s_mass": return "群";
                case "stapler": return "钉";
                case "keyboard": return "键";
                case "badge": return "牌";
                case "headphone": return "耳";
                case "hoodie": return "衣";
                case "slippers": return "鞋";
                default: return "?";
            }
        }

        void Pick(int index)
        {
            if (OnCardPicked != null)
            {
                OnCardPicked(index);
            }
        }

        public int CardCount
        {
            get { return Mathf.Min(_ctx.Driver.Cards.Offers.Count, MaxCards); }
        }
    }
}
