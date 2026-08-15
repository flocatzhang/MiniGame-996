using System;
using OfficeHell.View;
using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    /// <summary>
    /// Framed as a punch clock rather than as a game menu. The clock in the corner reads 08:59 and the
    /// run starts at 09:00, so the menu and the game sit on one continuous timeline.
    /// </summary>
    public sealed class UIMainMenuController : UIControllerBase
    {
        static readonly string[] Tips =
        {
            "据说摸鱼能回复理智。",
            "BUG 是修不完的，但可以变多。",
            "周报不会消失，只会积压。",
            "老油条不会伤害你，他只会拖慢你。",
            "咖啡在快撑不住的时候更容易出现。",
            "黄装和橙装不会自己飞过来，得走过去踩。",
            "六个武器槽，装同一把也算搭配。",
            "耳机现在是头部装备，不占武器槽。",
            "周六本该休息。",
            "KPI 最多到 99%。",
        };

        Text _tip;

        public Action OnStartClicked;
        public Action OnQuitClicked;

        protected override void OnUIInit()
        {
            Image bg = UIFactory.CreateImage(Root, "Bg", new Color(0.435f, 0.737f, 0.882f, 1f));
            UIFactory.Stretch(bg.rectTransform);

            Sprite logoSprite = ArtCatalog.Logo;
            if (logoSprite != null)
            {
                Image logo = UIFactory.CreateSpriteImage(Root, "Logo", logoSprite, Color.white, true);
                UIFactory.Anchor(
                    logo.rectTransform,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 300f),
                    new Vector2(700f, 420f));
            }
            else
            {
                UIFactory.AnchoredText(Root, "Title", "9 9 6", 150, new Color(0.95f, 0.93f, 0.88f),
                    TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1200f, 200f));
            }

            UIFactory.AnchoredText(Root, "Sub", "早九晚九 · 第六天", 34, new Color(0.12f, 0.16f, 0.23f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(1200f, 60f));

            Button start = UIFactory.CreateButton(Root, "BtnStart", "打 卡 上 班", 46, new Vector2(420f, 112f),
                new Color(0.85f, 0.28f, 0.24f, 1f), () =>
                {
                    if (OnStartClicked != null)
                    {
                        OnStartClicked();
                    }
                });

            UIFactory.Anchor(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -50f), new Vector2(420f, 112f));

            _tip = UIFactory.AnchoredText(Root, "Tip", Tips[0], 28, new Color(0.16f, 0.2f, 0.28f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(1400f, 46f));

            Button quit = UIFactory.CreateButton(Root, "BtnQuit", "离 职", 30, new Vector2(200f, 62f),
                new Color(0.18f, 0.18f, 0.22f, 1f), () =>
                {
                    if (OnQuitClicked != null)
                    {
                        OnQuitClicked();
                    }
                    else
                    {
                        Application.Quit();
                    }
                });

            UIFactory.Anchor(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -185f), new Vector2(200f, 62f));

            UIFactory.AnchoredText(Root, "Clock", "08:59  周一", 30, new Color(0.12f, 0.16f, 0.23f),
                TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(60f, 60f), new Vector2(500f, 44f));

            UIFactory.AnchoredText(Root, "Hint",
                "鼠标移动角色 · 攻击自动 · 空格摸鱼 · F1 调试面板 · F5 重载配置",
                22, new Color(0.12f, 0.16f, 0.23f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -260f), new Vector2(1600f, 44f));
        }

        protected override void OnUIOpen()
        {
            _tip.text = Tips[UnityEngine.Random.Range(0, Tips.Length)];
        }
    }
}
