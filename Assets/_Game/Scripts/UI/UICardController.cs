using System;
using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    /// <summary>
    /// The level up hand. With the shop gone this is the only screen where the player chooses anything,
    /// so the cards are built once and rebound per offer: rebuilding the hierarchy on every level up
    /// would allocate during the one moment the game is guaranteed to be paused and watched closely.
    /// </summary>
    public sealed class UICardController : UIControllerBase
    {
        const int MaxCards = 4;

        readonly UIContext _ctx;

        Text _title;
        readonly RectTransform[] _card = new RectTransform[MaxCards];
        readonly Image[] _cardBg = new Image[MaxCards];
        readonly Image[] _cardStripe = new Image[MaxCards];
        readonly Text[] _cardKind = new Text[MaxCards];
        readonly Text[] _cardTitle = new Text[MaxCards];
        readonly Text[] _cardDesc = new Text[MaxCards];
        readonly Text[] _cardKey = new Text[MaxCards];

        public Action<int> OnCardPicked;

        public UICardController(UIContext ctx)
        {
            _ctx = ctx;
        }

        protected override void OnUIInit()
        {
            Image bg = UIFactory.CreateImage(Root, "Bg", new Color(0.03f, 0.04f, 0.06f, 0.82f));
            UIFactory.Stretch(bg.rectTransform);
            bg.raycastTarget = true;

            _title = UIFactory.AnchoredText(Root, "Title", "升职加薪 · 选一个", 66, new Color(1f, 0.9f, 0.62f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1400f, 100f));

            const float cardWidth = 380f;
            const float cardHeight = 460f;

            for (int i = 0; i < MaxCards; i++)
            {
                int index = i;

                Button btn = UIFactory.CreateButton(Root, "Card" + i, string.Empty, 1,
                    new Vector2(cardWidth, cardHeight), new Color(0.12f, 0.13f, 0.17f, 0.98f), () => Pick(index));

                _card[i] = btn.GetComponent<RectTransform>();
                _cardBg[i] = btn.GetComponent<Image>();

                // Placement depends on how many cards the hand actually has, so it is done at open time.
                UIFactory.Anchor(_card[i], new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cardWidth, cardHeight));

                _cardStripe[i] = UIFactory.CreateImage(_card[i], "Stripe", Color.white);
                UIFactory.Anchor(_cardStripe[i].rectTransform, new Vector2(0.5f, 1f),
                    new Vector2(0f, -5f), new Vector2(cardWidth, 10f));

                _cardKind[i] = UIFactory.AnchoredText(_card[i], "Kind", "", 26, new Color(0.62f, 0.66f, 0.74f),
                    TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(cardWidth - 40f, 40f));

                _cardTitle[i] = UIFactory.AnchoredText(_card[i], "Title", "", 40, Color.white,
                    TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(cardWidth - 40f, 100f));

                _cardDesc[i] = UIFactory.AnchoredText(_card[i], "Desc", "", 26, new Color(0.78f, 0.82f, 0.88f),
                    TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(cardWidth - 60f, 200f));

                _cardDesc[i].horizontalOverflow = HorizontalWrapMode.Wrap;

                _cardKey[i] = UIFactory.AnchoredText(_card[i], "Key", "", 30, new Color(0.5f, 0.54f, 0.62f),
                    TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(cardWidth, 40f));
            }
        }

        protected override void OnUIOpen()
        {
            Refresh();
        }

        /// <summary>
        /// Called again after each pick, since one level up can be immediately followed by another and
        /// the same panel stays open across both hands.
        /// </summary>
        public void Refresh()
        {
            if (Root == null)
            {
                return;
            }

            List<CardOffer> offers = _ctx.Driver.Cards.Offers;
            int count = Mathf.Min(offers.Count, MaxCards);

            int pending = _ctx.Game.Run.Player.PendingLevelUps;
            _title.text = pending > 1
                ? "升职加薪 · 选一个  (还有 " + (pending - 1) + " 次)"
                : "升职加薪 · 选一个";

            float width = _card[0].sizeDelta.x;
            const float gap = 36f;
            float total = count * width + Mathf.Max(0, count - 1) * gap;
            float startX = -total * 0.5f + width * 0.5f;

            for (int i = 0; i < MaxCards; i++)
            {
                bool used = i < count;
                _card[i].gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                _card[i].anchoredPosition = new Vector2(startX + i * (width + gap), -20f);
                Fill(i, offers[i]);
            }
        }

        void Fill(int i, CardOffer offer)
        {
            _cardKind[i].text = KindWord(offer.Kind);
            _cardTitle[i].text = offer.Title;
            _cardDesc[i].text = Desc(offer);
            _cardKey[i].text = "按 " + (i + 1);

            Color accent = Accent(offer);
            _cardStripe[i].color = accent;
            _cardTitle[i].color = accent;
            _cardBg[i].color = Color.Lerp(new Color(0.12f, 0.13f, 0.17f, 0.98f), accent, 0.14f);
        }

        /// <summary>
        /// Stat cards carry no authored description, so the number is rendered here rather than being
        /// duplicated into every row of the card table.
        /// </summary>
        static string Desc(CardOffer offer)
        {
            if (offer.Kind != CardKind.Stat)
            {
                return offer.Desc;
            }

            string sign = offer.Value >= 0f ? "+" : string.Empty;
            string amount = offer.Percent
                ? sign + offer.Value.ToString("0.#") + "%"
                : sign + offer.Value.ToString("0.##");

            return offer.Desc.Length > 0 ? offer.Desc + "\n\n" + amount : amount;
        }

        Color Accent(CardOffer offer)
        {
            switch (offer.Kind)
            {
                case CardKind.Stat: return new Color(0.55f, 0.85f, 1f);
                case CardKind.Skill: return new Color(0.55f, 1f, 0.75f);
                default: return _ctx.Game.Cfg.QualityOf(offer.Quality).Color;
            }
        }

        static string KindWord(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Stat: return "· 加薪 ·";
                case CardKind.Skill: return "· 摸鱼技巧 ·";
                default: return "· 发装备 ·";
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
