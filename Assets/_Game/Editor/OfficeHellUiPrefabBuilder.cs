using System;
using OfficeHell.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.EditorTools
{
    /// <summary>Creates only absent starter prefabs so later Inspector edits are never overwritten.</summary>
    public static class OfficeHellUiPrefabBuilder
    {
        const string UiRoot = "Assets/_Game/UI";
        const string ArtFolder = UiRoot + "/Art";
        const string ResourcesFolder = UiRoot + "/Resources";
        const string PrefabFolder = ResourcesFolder + "/Prefabs";
        const string MainArtPath = ArtFolder + "/MainMenuBackgroundNoButton.png";
        const string MainMenuPrefabPath = PrefabFolder + "/UIMainMenu.prefab";
        const string HudPrefabPath = PrefabFolder + "/UIHud.prefab";
        const string OffWorkPrefabPath = PrefabFolder + "/UIOffWork.prefab";
        const string CardPanelPrefabPath = PrefabFolder + "/UICardPanel.prefab";
        const string CardItemPrefabPath = PrefabFolder + "/UICardItem.prefab";
        const string ResultPrefabPath = PrefabFolder + "/UIResult.prefab";

        static readonly Color Ink = new Color(0.08f, 0.09f, 0.14f, 1f);
        static readonly Color Paper = new Color(0.96f, 0.94f, 0.9f, 1f);
        static Font _font;

        [MenuItem("Office Hell/Create Missing UI Prefabs", false, 24)]
        public static void CreateMissing()
        {
            EnsureFolders();
            ConfigureMainTexture();
            CreateIfMissing(CardItemPrefabPath, BuildCardItem);
            CreateIfMissing(MainMenuPrefabPath, BuildMainMenu);
            CreateIfMissing(HudPrefabPath, BuildHud);
            CreateIfMissing(OffWorkPrefabPath, BuildOffWork);
            CreateIfMissing(CardPanelPrefabPath, BuildCardPanel);
            CreateIfMissing(ResultPrefabPath, BuildResult);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Debug.Log("[UI Prefabs] Missing prefabs created; existing prefab assets were left untouched.");
        }

        [MenuItem("Office Hell/Validate UI Prefabs", false, 25)]
        public static void Validate()
        {
            UIMainMenuView menu = Required<UIMainMenuView>(MainMenuPrefabPath);
            Require(menu.Background != null && menu.StartButton != null && menu.StartButtonImage != null &&
                    menu.StartButtonLabel != null && menu.StartButton.transform.parent == menu.transform,
                "UIMainMenu serialized references are incomplete.");

            UIHudView hud = Required<UIHudView>(HudPrefabPath);
            Require(hud.Portrait != null && hud.RankText != null && hud.SanFill != null && hud.SanText != null &&
                    hud.ExpFill != null && hud.ExpText != null && hud.CoinText != null && hud.KillText != null &&
                    hud.SkillRoot != null && hud.SkillBackground != null && hud.SkillIcon != null &&
                    hud.SkillFill != null && hud.SkillText != null && hud.WorkClockText != null &&
                    hud.StageText != null && hud.KpiFill != null && hud.KpiText != null && hud.BannerText != null &&
                    hud.WeaponSlots != null && hud.WeaponSlots.Length == 6 &&
                    hud.ArmorSlots != null && hud.ArmorSlots.Length == 3 && hud.BossRoot != null &&
                    hud.BossName != null && hud.BossFill != null && hud.BossPips != null && hud.BossPips.Length == 3,
                "UIHud serialized references or slot counts are incomplete.");
            Require(hud.SkillRoot.transform.parent == hud.transform &&
                    hud.SkillFill.type == UnityEngine.UI.Image.Type.Filled &&
                    hud.SkillFill.fillMethod == UnityEngine.UI.Image.FillMethod.Horizontal,
                "UIHud must expose a root-level horizontal slack-skill progress bar.");
            for (int i = 0; i < hud.WeaponSlots.Length; i++)
            {
                UIHudView.WeaponSlotReferences slot = hud.WeaponSlots[i];
                Require(slot != null && slot.Background != null && slot.CooldownFill != null &&
                        slot.Icon != null && slot.Label != null,
                    "UIHud weapon slot " + i + " has incomplete references.");
            }
            for (int i = 0; i < hud.ArmorSlots.Length; i++)
            {
                UIHudView.ArmorSlotReferences slot = hud.ArmorSlots[i];
                Require(slot != null && slot.Background != null && slot.Icon != null && slot.Label != null,
                    "UIHud armor slot " + i + " has incomplete references.");
            }

            UIOffWorkView offWork = Required<UIOffWorkView>(OffWorkPrefabPath);
            Require(offWork.Dimmer != null && offWork.SkipButton != null && offWork.BossPortrait != null &&
                    offWork.DayTitle != null && offWork.Speech != null && offWork.Summary != null &&
                    offWork.NextDay != null && offWork.Hint != null,
                "UIOffWork serialized references are incomplete.");

            UICardView card = Required<UICardView>(CardItemPrefabPath);
            Require(card.Button != null && card.Frame != null && card.Border != null && card.Accent != null && card.Footer != null &&
                    card.IconPlate != null && card.Icon != null && card.IconFallback != null && card.Kind != null &&
                    card.Title != null && card.Primary != null && card.Description != null && card.FooterText != null &&
                    card.KeyHint != null && card.RecommendBadge != null && card.NewBadge != null &&
                    card.DesignAccents != null && card.DesignAccents.Length == 16,
                "UICardItem serialized references are incomplete.");

            UICardPanelView panel = Required<UICardPanelView>(CardPanelPrefabPath);
            Require(panel.Dimmer != null && panel.Title != null && panel.CardContainer != null && panel.CardPrefab != null,
                "UICardPanel serialized references are incomplete.");
            Require(AssetDatabase.GetAssetPath(panel.CardPrefab) == CardItemPrefabPath,
                "UICardPanel must reference UICardItem.prefab.");

            UIResultView result = Required<UIResultView>(ResultPrefabPath);
            Require(result.Dimmer != null && result.Outcome != null && result.Stamp != null &&
                    result.SalaryGroup != null && result.Salary != null && result.WorkGroup != null &&
                    result.WorkLabels != null && result.WorkLabels.Length == 3 && result.WorkValues != null &&
                    result.WorkValues.Length == 3 && result.LootGroup != null && result.BestQuality != null &&
                    result.Rank != null && result.San != null && result.Loadout != null && result.Comment != null &&
                    result.KpiGroup != null && result.KpiLabel != null && result.KpiFill != null &&
                    result.ButtonsGroup != null && result.RestartButton != null && result.MenuButton != null,
                "UIResult serialized references are incomplete.");

            GameObject instance = PrefabUtility.InstantiatePrefab(card.gameObject) as GameObject;
            Require(instance != null && instance.GetComponent<UICardView>() != null,
                "UICardItem could not be instantiated.");
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);

            Debug.Log("[UI Prefabs] Validation passed: 6 prefabs, complete view bindings and fixed slot counts.");
        }

        public static void BuildMissingBatch()
        {
            try
            {
                CreateMissing();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        public static void ValidateBatch()
        {
            try
            {
                Validate();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        static void CreateIfMissing(string path, Func<GameObject> factory)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.LogWarning("[UI Prefabs] Refusing to overwrite existing asset: " + path);
                return;
            }

            GameObject root = factory();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("[UI Prefabs] Created " + path);
        }

        static GameObject BuildMainMenu()
        {
            GameObject root = Root("UIMainMenu");
            UIMainMenuView view = root.AddComponent<UIMainMenuView>();

            GameObject cover = UiObject("BackgroundCover", root.transform);
            RectTransform coverRect = cover.GetComponent<RectTransform>();
            SetCenter(coverRect, Vector2.zero, new Vector2(1920f, 1080f));
            AspectRatioFitter fitter = cover.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 1803f / 902f;

            RawImage background = cover.AddComponent<RawImage>();
            background.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MainArtPath);
            background.uvRect = new Rect(0f, 0f, 1f, 1f);
            background.raycastTarget = false;

            Image buttonImage = Image("StartButton", root.transform, new Color(1f, 0.78f, 0.04f, 1f));
            SetBottom(buttonImage.rectTransform, new Vector2(0f, 78f), new Vector2(520f, 128f), new Vector2(0.5f, 0f));
            Outline buttonOutline = buttonImage.gameObject.AddComponent<Outline>();
            buttonOutline.effectColor = new Color(0.04f, 0.05f, 0.08f, 0.95f);
            buttonOutline.effectDistance = new Vector2(6f, -6f);

            Button button = buttonImage.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.93f, 0.62f, 1f);
            colors.pressedColor = new Color(0.9f, 0.78f, 0.32f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            Text buttonLabel = Label("Label", buttonImage.transform, "打卡上班", 54, Ink, TextAnchor.MiddleCenter);
            Stretch(buttonLabel.rectTransform, 24f, 24f, 10f, 10f);
            buttonLabel.fontStyle = FontStyle.Bold;

            view.Background = background;
            view.StartButton = button;
            view.StartButtonImage = buttonImage;
            view.StartButtonLabel = buttonLabel;
            return root;
        }

        static GameObject BuildHud()
        {
            GameObject root = Root("UIHud");
            UIHudView view = root.AddComponent<UIHudView>();

            Image statusPanel = Image("CharacterStatus", root.transform, new Color(0.055f, 0.085f, 0.14f, 0.94f));
            SetTop(statusPanel.rectTransform, new Vector2(32f, -28f), new Vector2(620f, 190f), new Vector2(0f, 1f));
            statusPanel.raycastTarget = false;
            Outline statusOutline = statusPanel.gameObject.AddComponent<Outline>();
            statusOutline.effectColor = new Color(0.01f, 0.02f, 0.04f, 0.95f);
            statusOutline.effectDistance = new Vector2(5f, -5f);

            Image portraitFrame = Image("PortraitFrame", statusPanel.transform, new Color(0.12f, 0.19f, 0.3f, 1f));
            SetTop(portraitFrame.rectTransform, new Vector2(16f, -16f), new Vector2(120f, 156f), new Vector2(0f, 1f));
            portraitFrame.raycastTarget = false;
            Outline portraitOutline = portraitFrame.gameObject.AddComponent<Outline>();
            portraitOutline.effectColor = new Color(0.7f, 0.82f, 0.95f, 0.9f);
            portraitOutline.effectDistance = new Vector2(3f, -3f);
            Image portrait = Image("Portrait", portraitFrame.transform, new Color(0.25f, 0.35f, 0.52f, 1f));
            Stretch(portrait.rectTransform, 6f, 6f, 6f, 6f);
            portrait.raycastTarget = false;
            portrait.preserveAspect = true;

            Text rank = Label("Rank", statusPanel.transform, "实习生  Lv.1", 24, Color.white, TextAnchor.MiddleLeft);
            SetTop(rank.rectTransform, new Vector2(150f, -12f), new Vector2(245f, 34f), new Vector2(0f, 1f));
            rank.fontStyle = FontStyle.Bold;

            Image expBackground;
            Image expFill = Bar("ExpBar", statusPanel.transform,
                new Color(0.02f, 0.03f, 0.05f, 0.9f), new Color(0.96f, 0.78f, 0.18f, 1f), out expBackground);
            SetTop(expBackground.rectTransform, new Vector2(150f, -49f), new Vector2(245f, 17f), new Vector2(0f, 1f));
            Text expText = Label("ExpText", expBackground.transform, "0 / 12", 14, Color.white, TextAnchor.MiddleCenter);
            Stretch(expText.rectTransform, 0f, 0f, 0f, 0f);

            Image coinBlock = Image("CoinBlock", statusPanel.transform, new Color(0.16f, 0.21f, 0.31f, 1f));
            SetTop(coinBlock.rectTransform, new Vector2(407f, -12f), new Vector2(91f, 53f), new Vector2(0f, 1f));
            coinBlock.raycastTarget = false;
            Text coinCaption = Label("Caption", coinBlock.transform, "工资", 13,
                new Color(0.95f, 0.78f, 0.25f), TextAnchor.UpperCenter);
            SetTopStretch(coinCaption.rectTransform, 3f, 20f, 0f);
            Text coinText = Label("Value", coinBlock.transform, "0", 21, Color.white, TextAnchor.LowerCenter);
            SetBottomStretch(coinText.rectTransform, 3f, 30f, 0f);
            coinText.fontStyle = FontStyle.Bold;

            Image killBlock = Image("KillBlock", statusPanel.transform, new Color(0.16f, 0.21f, 0.31f, 1f));
            SetTop(killBlock.rectTransform, new Vector2(510f, -12f), new Vector2(91f, 53f), new Vector2(0f, 1f));
            killBlock.raycastTarget = false;
            Text killCaption = Label("Caption", killBlock.transform, "击败", 13,
                new Color(0.9f, 0.91f, 0.95f), TextAnchor.UpperCenter);
            SetTopStretch(killCaption.rectTransform, 3f, 20f, 0f);
            Text killText = Label("Value", killBlock.transform, "0", 21, Color.white, TextAnchor.LowerCenter);
            SetBottomStretch(killText.rectTransform, 3f, 30f, 0f);
            killText.fontStyle = FontStyle.Bold;

            Image sanBackground;
            Image sanFill = Bar("SanBar", statusPanel.transform,
                new Color(0.02f, 0.03f, 0.05f, 0.9f), new Color(0.88f, 0.17f, 0.21f, 1f), out sanBackground);
            SetTop(sanBackground.rectTransform, new Vector2(150f, -78f), new Vector2(451f, 35f), new Vector2(0f, 1f));
            Text sanCaption = Label("Caption", sanBackground.transform, "SAN", 16, Color.white, TextAnchor.MiddleLeft);
            Stretch(sanCaption.rectTransform, 10f, 330f, 0f, 0f);
            Text sanText = Label("Value", sanBackground.transform, "99 / 99", 18, Color.white, TextAnchor.MiddleRight);
            Stretch(sanText.rectTransform, 110f, 12f, 0f, 0f);
            sanText.fontStyle = FontStyle.Bold;

            Image skillPanel = Image("SlackSkillStatus", root.transform, new Color(0.055f, 0.085f, 0.14f, 0.95f));
            SetTop(skillPanel.rectTransform, new Vector2(32f, -230f), new Vector2(620f, 58f), new Vector2(0f, 1f));
            skillPanel.raycastTarget = false;
            Outline skillOutline = skillPanel.gameObject.AddComponent<Outline>();
            skillOutline.effectColor = new Color(0.01f, 0.02f, 0.04f, 0.95f);
            skillOutline.effectDistance = new Vector2(3f, -3f);

            Image skillIcon = Image("SkillIcon", skillPanel.transform, new Color(0.15f, 0.76f, 0.5f, 1f));
            SetTop(skillIcon.rectTransform, new Vector2(8f, -6f), new Vector2(46f, 46f), new Vector2(0f, 1f));
            skillIcon.raycastTarget = false;
            Text skillCaption = Label("Caption", skillPanel.transform, "摸鱼技能", 17,
                new Color(0.78f, 1f, 0.88f), TextAnchor.MiddleCenter);
            SetTop(skillCaption.rectTransform, new Vector2(58f, -7f), new Vector2(86f, 44f), new Vector2(0f, 1f));
            skillCaption.fontStyle = FontStyle.Bold;

            Image skillBackground;
            Image skillFill = Bar("SkillBar", skillPanel.transform,
                new Color(0.02f, 0.03f, 0.05f, 0.9f), new Color(0.15f, 0.76f, 0.5f, 1f), out skillBackground);
            SetTop(skillBackground.rectTransform, new Vector2(150f, -14f), new Vector2(451f, 30f), new Vector2(0f, 1f));
            Text skillText = Label("SkillText", skillBackground.transform, "摸鱼 · 就绪 100%", 17, Color.white, TextAnchor.MiddleCenter);
            Stretch(skillText.rectTransform, 6f, 6f, 0f, 0f);
            skillText.fontStyle = FontStyle.Bold;

            Image clockPanel = Image("BattleClock", root.transform, new Color(0.72f, 0.88f, 0.63f, 0.96f));
            SetTop(clockPanel.rectTransform, new Vector2(0f, -26f), new Vector2(350f, 122f), new Vector2(0.5f, 1f));
            clockPanel.raycastTarget = false;
            Outline clockOutline = clockPanel.gameObject.AddComponent<Outline>();
            clockOutline.effectColor = new Color(0.03f, 0.08f, 0.13f, 1f);
            clockOutline.effectDistance = new Vector2(5f, -5f);
            Text workClock = Label("WorkClock", clockPanel.transform, "09:00", 54, Ink, TextAnchor.MiddleCenter);
            SetTop(workClock.rectTransform, new Vector2(0f, -5f), new Vector2(330f, 72f), new Vector2(0.5f, 1f));
            workClock.fontStyle = FontStyle.Bold;
            Image stagePlate = Image("StagePlate", clockPanel.transform, new Color(1f, 0.82f, 0.2f, 1f));
            SetBottomStretch(stagePlate.rectTransform, -18f, 43f, 26f);
            stagePlate.raycastTarget = false;
            Text stage = Label("Stage", stagePlate.transform, "周一 · 上午", 22, Ink, TextAnchor.MiddleCenter);
            Stretch(stage.rectTransform, 4f, 4f, 0f, 0f);
            stage.fontStyle = FontStyle.Bold;

            Image kpiPanel = Image("KpiPanel", root.transform, new Color(0.055f, 0.085f, 0.14f, 0.94f));
            SetTop(kpiPanel.rectTransform, new Vector2(-32f, -38f), new Vector2(430f, 68f), new Vector2(1f, 1f));
            kpiPanel.raycastTarget = false;
            Outline kpiOutline = kpiPanel.gameObject.AddComponent<Outline>();
            kpiOutline.effectColor = new Color(0.01f, 0.02f, 0.04f, 0.95f);
            kpiOutline.effectDistance = new Vector2(4f, -4f);
            Image kpiBackground;
            Image kpiFill = Bar("KpiBar", kpiPanel.transform,
                new Color(0.02f, 0.03f, 0.05f, 0.9f), new Color(0.08f, 0.58f, 0.96f, 1f), out kpiBackground);
            Stretch(kpiBackground.rectTransform, 16f, 16f, 14f, 14f);
            Text kpiText = Label("KpiText", kpiBackground.transform, "KPI 完成度  0%", 20, Color.white,
                TextAnchor.MiddleCenter);
            Stretch(kpiText.rectTransform, 4f, 4f, 0f, 0f);
            kpiText.fontStyle = FontStyle.Bold;

            GameObject weaponContainer = UiObject("WeaponSlots", root.transform);
            RectTransform weaponContainerRect = weaponContainer.GetComponent<RectTransform>();
            SetBottom(weaponContainerRect, new Vector2(0f, 24f), new Vector2(780f, 104f), new Vector2(0.5f, 0f));
            UIHudView.WeaponSlotReferences[] weapons = new UIHudView.WeaponSlotReferences[6];
            for (int i = 0; i < weapons.Length; i++)
            {
                Image background = Image("Weapon" + (i + 1), weaponContainer.transform,
                    new Color(0.09f, 0.14f, 0.22f, 0.96f));
                SetTop(background.rectTransform, new Vector2(19f + i * 126f, 0f), new Vector2(112f, 94f), new Vector2(0f, 1f));
                background.raycastTarget = false;
                Outline slotOutline = background.gameObject.AddComponent<Outline>();
                slotOutline.effectColor = new Color(0.52f, 0.66f, 0.82f, 0.9f);
                slotOutline.effectDistance = new Vector2(3f, -3f);

                Image icon = Image("Icon", background.transform, new Color(0.18f, 0.22f, 0.29f, 1f));
                SetTop(icon.rectTransform, new Vector2(16f, -10f), new Vector2(80f, 56f), new Vector2(0f, 1f));
                icon.raycastTarget = false;
                Image cooldown = Image("Cooldown", background.transform, new Color(0.1f, 0.65f, 0.95f, 0.42f));
                Stretch(cooldown.rectTransform, 0f, 0f, 0f, 0f);
                cooldown.type = UnityEngine.UI.Image.Type.Filled;
                cooldown.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
                cooldown.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Bottom;
                cooldown.fillAmount = 0f;
                cooldown.raycastTarget = false;
                Text label = Label("Label", background.transform, "空", 16, Color.white, TextAnchor.MiddleCenter);
                SetBottomStretch(label.rectTransform, 3f, 25f, 4f);
                label.fontStyle = FontStyle.Bold;

                weapons[i] = new UIHudView.WeaponSlotReferences
                {
                    Background = background,
                    CooldownFill = cooldown,
                    Icon = icon,
                    Label = label,
                };
            }

            GameObject armorContainer = UiObject("ArmorSlots", root.transform);
            RectTransform armorContainerRect = armorContainer.GetComponent<RectTransform>();
            SetBottom(armorContainerRect, new Vector2(-32f, 24f), new Vector2(396f, 104f), new Vector2(1f, 0f));
            UIHudView.ArmorSlotReferences[] armors = new UIHudView.ArmorSlotReferences[3];
            for (int i = 0; i < armors.Length; i++)
            {
                Image background = Image("Armor" + (i + 1), armorContainer.transform,
                    new Color(0.09f, 0.14f, 0.22f, 0.96f));
                SetTop(background.rectTransform, new Vector2(16f + i * 126f, 0f), new Vector2(112f, 94f), new Vector2(0f, 1f));
                background.raycastTarget = false;
                Outline slotOutline = background.gameObject.AddComponent<Outline>();
                slotOutline.effectColor = new Color(0.52f, 0.66f, 0.82f, 0.9f);
                slotOutline.effectDistance = new Vector2(3f, -3f);
                Image icon = Image("Icon", background.transform, new Color(0.18f, 0.22f, 0.29f, 1f));
                SetTop(icon.rectTransform, new Vector2(16f, -10f), new Vector2(80f, 56f), new Vector2(0f, 1f));
                icon.raycastTarget = false;
                Text label = Label("Label", background.transform, i == 0 ? "头" : i == 1 ? "身" : "脚", 16,
                    Color.white, TextAnchor.MiddleCenter);
                SetBottomStretch(label.rectTransform, 3f, 25f, 4f);
                label.fontStyle = FontStyle.Bold;
                armors[i] = new UIHudView.ArmorSlotReferences
                {
                    Background = background,
                    Icon = icon,
                    Label = label,
                };
            }

            GameObject bossRoot = UiObject("BossBar", root.transform);
            RectTransform bossRect = bossRoot.GetComponent<RectTransform>();
            SetBottom(bossRect, new Vector2(0f, 150f), new Vector2(900f, 66f), new Vector2(0.5f, 0f));
            Text bossName = Label("BossName", bossRoot.transform, "领导 · 第 1 阶段", 24,
                new Color(1f, 0.72f, 0.9f), TextAnchor.MiddleCenter);
            SetTopStretch(bossName.rectTransform, 0f, 30f, 0f);
            Image bossBackground;
            Image bossFill = Bar("BossHp", bossRoot.transform,
                new Color(0.02f, 0.03f, 0.05f, 0.9f), new Color(0.95f, 0.35f, 0.72f, 1f), out bossBackground);
            SetBottomStretch(bossBackground.rectTransform, 0f, 25f, 0f);
            Image[] bossPips = new Image[3];
            for (int i = 0; i < bossPips.Length; i++)
            {
                bossPips[i] = Image("Pip" + (i + 1), bossRoot.transform, new Color(1f, 0.55f, 0.85f, 1f));
                SetBottom(bossPips[i].rectTransform, new Vector2(-475f + i * 22f, 4f), new Vector2(16f, 16f),
                    new Vector2(0.5f, 0f));
                bossPips[i].raycastTarget = false;
            }
            bossRoot.SetActive(false);

            Text banner = Label("DayBanner", root.transform, "周一 · 怎么周末又结束了，不想上班", 64,
                new Color(1f, 0.94f, 0.72f), TextAnchor.MiddleCenter);
            SetCenter(banner.rectTransform, new Vector2(0f, 80f), new Vector2(1300f, 120f));
            banner.fontStyle = FontStyle.Bold;

            view.Portrait = portrait;
            view.RankText = rank;
            view.SanFill = sanFill;
            view.SanText = sanText;
            view.ExpFill = expFill;
            view.ExpText = expText;
            view.CoinText = coinText;
            view.KillText = killText;
            view.SkillRoot = skillPanel.gameObject;
            view.SkillBackground = skillBackground;
            view.SkillIcon = skillIcon;
            view.SkillFill = skillFill;
            view.SkillText = skillText;
            view.WorkClockText = workClock;
            view.StageText = stage;
            view.KpiFill = kpiFill;
            view.KpiText = kpiText;
            view.WeaponSlots = weapons;
            view.ArmorSlots = armors;
            view.BossRoot = bossRoot;
            view.BossName = bossName;
            view.BossFill = bossFill;
            view.BossPips = bossPips;
            view.BannerText = banner;
            return root;
        }

        static GameObject BuildOffWork()
        {
            GameObject root = Root("UIOffWork");
            UIOffWorkView view = root.AddComponent<UIOffWorkView>();

            Image dimmer = Image("Dimmer", root.transform, new Color(0.015f, 0.025f, 0.045f, 0.76f));
            Stretch(dimmer.rectTransform, 0f, 0f, 0f, 0f);
            dimmer.raycastTarget = true;
            Button skip = dimmer.gameObject.AddComponent<Button>();
            skip.targetGraphic = dimmer;
            skip.transition = Selectable.Transition.None;

            Text dayTitle = Label("DayTitle", root.transform, "周一 · 怎么周末又结束了，不想上班 · 下班", 40,
                Color.white, TextAnchor.MiddleLeft);
            SetTop(dayTitle.rectTransform, new Vector2(64f, -50f), new Vector2(700f, 68f), new Vector2(0f, 1f));
            dayTitle.fontStyle = FontStyle.Bold;

            Image summaryPanel = Image("DailySummary", root.transform, new Color(0.055f, 0.085f, 0.14f, 0.92f));
            SetBottom(summaryPanel.rectTransform, new Vector2(64f, 92f), new Vector2(770f, 210f), new Vector2(0f, 0f));
            summaryPanel.raycastTarget = false;
            Outline summaryOutline = summaryPanel.gameObject.AddComponent<Outline>();
            summaryOutline.effectColor = new Color(0.01f, 0.02f, 0.04f, 0.95f);
            summaryOutline.effectDistance = new Vector2(5f, -5f);
            Text summary = Label("Summary", summaryPanel.transform,
                "今日处理  0 项    累计击败  0 项\n职位  实习生 Lv.1    SAN  99 / 99", 25,
                new Color(0.9f, 0.92f, 0.96f), TextAnchor.UpperLeft);
            Stretch(summary.rectTransform, 28f, 28f, 64f, 20f);
            summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            Text nextDay = Label("NextDay", summaryPanel.transform,
                "明天 周二：线上怎么又出 BUG 了 · 50 秒 · HP x1.20  DMG x1.15", 21,
                new Color(1f, 0.8f, 0.28f), TextAnchor.MiddleLeft);
            SetBottomStretch(nextDay.rectTransform, 16f, 40f, 28f);

            Image bubble = Image("SpeechBubble", root.transform, new Color(0.97f, 0.95f, 0.9f, 1f));
            SetBottom(bubble.rectTransform, new Vector2(-390f, 250f), new Vector2(680f, 250f), new Vector2(1f, 0f));
            bubble.raycastTarget = false;
            Outline bubbleOutline = bubble.gameObject.AddComponent<Outline>();
            bubbleOutline.effectColor = new Color(0.03f, 0.04f, 0.07f, 1f);
            bubbleOutline.effectDistance = new Vector2(6f, -6f);
            Text speech = Label("Speech", bubble.transform,
                "KPI 还差 <color=#E62C2C>94%</color>！\n下班了？？", 46, Ink, TextAnchor.MiddleCenter);
            Stretch(speech.rectTransform, 32f, 32f, 24f, 24f);
            speech.fontStyle = FontStyle.Bold;
            speech.supportRichText = true;

            Image bossPortrait = Image("BossPortrait", root.transform, new Color(0.2f, 0.12f, 0.28f, 0.88f));
            SetBottom(bossPortrait.rectTransform, new Vector2(-18f, 0f), new Vector2(400f, 470f), new Vector2(1f, 0f));
            bossPortrait.raycastTarget = false;
            bossPortrait.preserveAspect = true;

            Text hint = Label("Hint", root.transform, "点击任意位置继续", 22,
                new Color(0.68f, 0.72f, 0.8f), TextAnchor.MiddleCenter);
            SetBottom(hint.rectTransform, new Vector2(0f, 34f), new Vector2(700f, 40f), new Vector2(0.5f, 0f));

            view.Dimmer = dimmer;
            view.SkipButton = skip;
            view.BossPortrait = bossPortrait;
            view.DayTitle = dayTitle;
            view.Speech = speech;
            view.Summary = summary;
            view.NextDay = nextDay;
            view.Hint = hint;
            return root;
        }

        static GameObject BuildCardItem()
        {
            GameObject root = UiObject("UICardItem", null);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(350f, 520f);

            Image frame = root.AddComponent<Image>();
            frame.color = Paper;
            frame.raycastTarget = true;
            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(0.03f, 0.04f, 0.07f, 0.94f);
            outline.effectDistance = new Vector2(5f, -5f);
            Button button = root.AddComponent<Button>();
            button.targetGraphic = frame;
            LayoutElement layout = root.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = 350f;
            layout.minHeight = layout.preferredHeight = 520f;
            layout.flexibleWidth = layout.flexibleHeight = 0f;

            Image accent = Image("Accent", root.transform, new Color(0.45f, 0.5f, 0.57f));
            SetTopStretch(accent.rectTransform, 0f, 9f, 0f);

            Text kind = Label("Kind", root.transform, "数值卡", 22, Ink, TextAnchor.MiddleLeft);
            SetTop(kind.rectTransform, new Vector2(24f, -34f), new Vector2(180f, 34f), new Vector2(0f, 1f));

            GameObject recommend = Badge(root.transform, "RecommendBadge", "推荐", new Color(1f, 0.83f, 0.1f),
                new Vector2(118f, -36f));
            GameObject isNew = Badge(root.transform, "NewBadge", "NEW", new Color(0.88f, 0.18f, 0.14f),
                new Vector2(118f, -36f));
            recommend.SetActive(false);
            isNew.SetActive(false);

            Image iconPlate = Image("IconPlate", root.transform, new Color(0.88f, 0.9f, 0.94f));
            SetTop(iconPlate.rectTransform, new Vector2(0f, -132f), new Vector2(144f, 122f), new Vector2(0.5f, 1f));
            Outline iconOutline = iconPlate.gameObject.AddComponent<Outline>();
            iconOutline.effectColor = new Color(0.1f, 0.12f, 0.18f, 0.35f);
            iconOutline.effectDistance = new Vector2(2f, -2f);

            Image icon = Image("Icon", iconPlate.transform, Color.white);
            Stretch(icon.rectTransform, 12f, 12f, 12f, 12f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Text fallback = Label("IconFallback", iconPlate.transform, "ATK", 34, Ink, TextAnchor.MiddleCenter);
            Stretch(fallback.rectTransform, 4f, 4f, 4f, 4f);

            Text title = Label("Title", root.transform, "卷王", 38, Ink, TextAnchor.MiddleLeft);
            SetTop(title.rectTransform, new Vector2(24f, -218f), new Vector2(302f, 52f), new Vector2(0f, 1f));
            title.fontStyle = FontStyle.Bold;

            Image rule = Image("Rule", root.transform, new Color(0.12f, 0.14f, 0.2f, 0.28f));
            SetTop(rule.rectTransform, new Vector2(24f, -257f), new Vector2(302f, 2f), new Vector2(0f, 1f));

            Text primary = Label("Primary", root.transform, "攻击力 +5", 27, Ink, TextAnchor.UpperLeft);
            SetTop(primary.rectTransform, new Vector2(24f, -286f), new Vector2(302f, 82f), new Vector2(0f, 1f));
            primary.fontStyle = FontStyle.Bold;
            primary.horizontalOverflow = HorizontalWrapMode.Wrap;
            primary.verticalOverflow = VerticalWrapMode.Truncate;

            Text description = Label("Description", root.transform, "基础属性永久提升", 22,
                new Color(0.22f, 0.23f, 0.28f), TextAnchor.UpperLeft);
            SetTop(description.rectTransform, new Vector2(24f, -356f), new Vector2(302f, 84f), new Vector2(0f, 1f));
            description.horizontalOverflow = HorizontalWrapMode.Wrap;

            Text keyHint = Label("KeyHint", root.transform, "按 1", 20,
                new Color(0.3f, 0.32f, 0.38f), TextAnchor.MiddleRight);
            SetBottom(keyHint.rectTransform, new Vector2(-24f, 55f), new Vector2(100f, 34f), new Vector2(1f, 0f));

            Image footer = Image("Footer", root.transform, new Color(0.45f, 0.5f, 0.57f));
            SetBottomStretch(footer.rectTransform, 0f, 46f, 0f);
            Text footerText = Label("FooterText", footer.transform, "基础成长", 25, Ink, TextAnchor.MiddleCenter);
            Stretch(footerText.rectTransform, 0f, 0f, 0f, 0f);
            footerText.fontStyle = FontStyle.Bold;

            UICardView view = root.AddComponent<UICardView>();
            view.Button = button;
            view.Frame = frame;
            view.Border = outline;
            view.Accent = accent;
            view.Footer = footer;
            view.IconPlate = iconPlate;
            view.Icon = icon;
            view.IconFallback = fallback;
            view.Kind = kind;
            view.Title = title;
            view.Primary = primary;
            view.Description = description;
            view.FooterText = footerText;
            view.KeyHint = keyHint;
            view.RecommendBadge = recommend;
            view.NewBadge = isNew;
            view.DesignAccents = new UICardView.CardAccentEntry[]
            {
                CardAccent("c_atk", 0.9f, 0.27f, 0.2f),
                CardAccent("c_atk_pct", 1f, 0.48f, 0.14f),
                CardAccent("c_haste", 0.12f, 0.63f, 0.9f),
                CardAccent("c_crit", 0.94f, 0.68f, 0.12f),
                CardAccent("c_critdmg", 0.68f, 0.3f, 0.82f),
                CardAccent("c_def", 0.25f, 0.43f, 0.78f),
                CardAccent("c_dodge", 0.16f, 0.66f, 0.43f),
                CardAccent("c_san", 0.78f, 0.25f, 0.5f),
                CardAccent("c_speed", 0.08f, 0.67f, 0.68f),
                CardAccent("c_luck", 0.94f, 0.62f, 0.08f),
                CardAccent("c_magnet", 0.16f, 0.52f, 0.82f),
                CardAccent("s_deep", 0.18f, 0.62f, 0.92f),
                CardAccent("s_paid", 0.18f, 0.7f, 0.4f),
                CardAccent("s_reverse", 0.65f, 0.28f, 0.82f),
                CardAccent("s_extra", 0.92f, 0.3f, 0.3f),
                CardAccent("s_mass", 0.95f, 0.5f, 0.12f),
            };
            return root;
        }

        static UICardView.CardAccentEntry CardAccent(string key, float r, float g, float b)
        {
            return new UICardView.CardAccentEntry(key, new Color(r, g, b, 1f));
        }

        static GameObject BuildCardPanel()
        {
            GameObject root = Root("UICardPanel");
            UICardPanelView view = root.AddComponent<UICardPanelView>();
            Image dimmer = Image("Dimmer", root.transform, new Color(0.02f, 0.035f, 0.06f, 0.72f));
            Stretch(dimmer.rectTransform, 0f, 0f, 0f, 0f);
            dimmer.raycastTarget = true;

            Image banner = Image("TitleBanner", root.transform, new Color(1f, 0.77f, 0.08f, 1f));
            SetTop(banner.rectTransform, new Vector2(96f, -102f), new Vector2(600f, 82f), new Vector2(0f, 1f));
            Outline bannerOutline = banner.gameObject.AddComponent<Outline>();
            bannerOutline.effectColor = new Color(0.05f, 0.07f, 0.11f, 0.9f);
            bannerOutline.effectDistance = new Vector2(5f, -5f);
            Text title = Label("Title", banner.transform, "选择你的奖励", 42, Ink, TextAnchor.MiddleCenter);
            Stretch(title.rectTransform, 20f, 20f, 4f, 4f);
            title.fontStyle = FontStyle.Bold;

            GameObject cards = UiObject("CardContainer", root.transform);
            RectTransform cardRect = cards.GetComponent<RectTransform>();
            SetCenter(cardRect, new Vector2(0f, -36f), new Vector2(1114f, 520f));
            HorizontalLayoutGroup layout = cards.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 32f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            Text hint = Label("Hint", root.transform, "鼠标点击或按数字键 1–3", 24,
                new Color(0.88f, 0.9f, 0.95f), TextAnchor.MiddleCenter);
            SetBottom(hint.rectTransform, new Vector2(0f, 54f), new Vector2(800f, 38f), new Vector2(0.5f, 0f));

            view.Dimmer = dimmer;
            view.Title = title;
            view.CardContainer = cardRect;
            view.CardPrefab = AssetDatabase.LoadAssetAtPath<UICardView>(CardItemPrefabPath);
            return root;
        }

        static GameObject BuildResult()
        {
            GameObject root = Root("UIResult");
            UIResultView view = root.AddComponent<UIResultView>();
            Image dimmer = Image("Dimmer", root.transform, new Color(0.015f, 0.025f, 0.045f, 0.76f));
            Stretch(dimmer.rectTransform, 0f, 0f, 0f, 0f);
            dimmer.raycastTarget = true;

            Image paper = Image("SalaryPaper", root.transform, Paper);
            SetCenter(paper.rectTransform, new Vector2(0f, 34f), new Vector2(860f, 650f));
            Outline paperOutline = paper.gameObject.AddComponent<Outline>();
            paperOutline.effectColor = new Color(0.03f, 0.04f, 0.07f, 0.85f);
            paperOutline.effectDistance = new Vector2(6f, -6f);

            Image outcomeBanner = Image("OutcomeBanner", root.transform, new Color(0.72f, 0.09f, 0.12f, 1f));
            SetCenter(outcomeBanner.rectTransform, new Vector2(-250f, 378f), new Vector2(440f, 86f));
            Outline outcomeOutline = outcomeBanner.gameObject.AddComponent<Outline>();
            outcomeOutline.effectColor = new Color(0.05f, 0.03f, 0.04f, 1f);
            outcomeOutline.effectDistance = new Vector2(5f, -5f);
            Text outcome = Label("Outcome", outcomeBanner.transform, "未达标", 47, Color.white, TextAnchor.MiddleCenter);
            Stretch(outcome.rectTransform, 16f, 16f, 4f, 4f);
            outcome.fontStyle = FontStyle.Bold;

            Text stamp = Label("Stamp", paper.transform, "第 6 天 · 18:00", 19,
                new Color(0.28f, 0.27f, 0.26f), TextAnchor.MiddleRight);
            SetTop(stamp.rectTransform, new Vector2(-50f, -22f), new Vector2(360f, 32f), new Vector2(1f, 1f));

            GameObject salaryGroup = UiObject("SalaryGroup", paper.transform);
            RectTransform salaryRect = salaryGroup.GetComponent<RectTransform>();
            SetTop(salaryRect, new Vector2(0f, -62f), new Vector2(760f, 126f), new Vector2(0.5f, 1f));
            Text salaryCaption = Label("Caption", salaryGroup.transform, "累计工资", 20, Ink, TextAnchor.MiddleCenter);
            SetTop(salaryCaption.rectTransform, new Vector2(0f, -12f), new Vector2(500f, 30f), new Vector2(0.5f, 1f));
            Text salary = Label("Salary", salaryGroup.transform, "¥9,996", 52,
                new Color(0.72f, 0.35f, 0.12f), TextAnchor.MiddleCenter);
            SetBottom(salary.rectTransform, new Vector2(0f, 38f), new Vector2(620f, 72f), new Vector2(0.5f, 0f));
            salary.fontStyle = FontStyle.Bold;

            Image topRule = Image("TopRule", paper.transform, new Color(0.15f, 0.13f, 0.12f, 0.2f));
            SetTop(topRule.rectTransform, new Vector2(50f, -191f), new Vector2(760f, 2f), new Vector2(0f, 1f));

            GameObject workGroup = UiObject("WorkGroup", paper.transform);
            RectTransform workRect = workGroup.GetComponent<RectTransform>();
            SetTop(workRect, new Vector2(0f, -216f), new Vector2(710f, 150f), new Vector2(0.5f, 1f));
            Text workCaption = Label("Caption", workGroup.transform, "工作明细", 22, Ink, TextAnchor.MiddleLeft);
            SetTop(workCaption.rectTransform, new Vector2(0f, 0f), new Vector2(300f, 30f), new Vector2(0f, 1f));
            workCaption.fontStyle = FontStyle.Bold;
            Text[] workLabels = new Text[3];
            Text[] workValues = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                float y = -40f - i * 34f;
                workLabels[i] = Label("WorkLabel" + (i + 1), workGroup.transform, "处理邮件", 21, Ink, TextAnchor.MiddleLeft);
                SetTop(workLabels[i].rectTransform, new Vector2(20f, y), new Vector2(400f, 30f), new Vector2(0f, 1f));
                workValues[i] = Label("WorkValue" + (i + 1), workGroup.transform, "+ 0 封", 21, Ink, TextAnchor.MiddleRight);
                SetTop(workValues[i].rectTransform, new Vector2(-20f, y), new Vector2(260f, 30f), new Vector2(1f, 1f));
            }

            Image middleRule = Image("MiddleRule", paper.transform, new Color(0.15f, 0.13f, 0.12f, 0.2f));
            SetTop(middleRule.rectTransform, new Vector2(50f, -378f), new Vector2(760f, 2f), new Vector2(0f, 1f));

            GameObject lootGroup = UiObject("LootGroup", paper.transform);
            RectTransform lootRect = lootGroup.GetComponent<RectTransform>();
            SetTop(lootRect, new Vector2(0f, -397f), new Vector2(710f, 152f), new Vector2(0.5f, 1f));
            Text best = ResultLine(lootGroup.transform, "BestQuality", "最高品质  橙色", 0f);
            Text rank = ResultLine(lootGroup.transform, "Rank", "最终职位  实习生", -31f);
            Text san = ResultLine(lootGroup.transform, "San", "剩余 SAN  32 / 32", -62f);
            Text loadout = ResultLine(lootGroup.transform, "Loadout", "最终配置  武器 □□□□□□   防具 □□□", -93f);
            Text comment = Label("Comment", lootGroup.transform, "表现尚可，明年继续努力。", 20,
                new Color(0.46f, 0.17f, 0.14f), TextAnchor.MiddleCenter);
            SetTop(comment.rectTransform, new Vector2(0f, -127f), new Vector2(700f, 30f), new Vector2(0.5f, 1f));
            comment.fontStyle = FontStyle.Italic;

            GameObject kpiGroup = UiObject("KpiGroup", paper.transform);
            RectTransform kpiRect = kpiGroup.GetComponent<RectTransform>();
            SetBottom(kpiRect, new Vector2(0f, 42f), new Vector2(710f, 50f), new Vector2(0.5f, 0f));
            Text kpiLabel = Label("KpiLabel", kpiGroup.transform, "KPI 完成度  99%", 21, Ink, TextAnchor.MiddleLeft);
            SetCenter(kpiLabel.rectTransform, new Vector2(0f, 13f), new Vector2(710f, 28f));
            Image kpiBackground = Image("KpiBackground", kpiGroup.transform, new Color(0.16f, 0.14f, 0.12f, 0.18f));
            SetBottom(kpiBackground.rectTransform, new Vector2(355f, 1f), new Vector2(710f, 15f), new Vector2(0.5f, 0f));
            Image kpiFill = Image("KpiFill", kpiBackground.transform, new Color(0.91f, 0.62f, 0.05f, 1f));
            Stretch(kpiFill.rectTransform, 0f, 0f, 0f, 0f);
            kpiFill.type = UnityEngine.UI.Image.Type.Filled;
            kpiFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            kpiFill.fillOrigin = 0;
            kpiFill.fillAmount = 0.99f;

            GameObject buttonsGroup = UiObject("ButtonsGroup", root.transform);
            RectTransform buttonsRect = buttonsGroup.GetComponent<RectTransform>();
            SetCenter(buttonsRect, new Vector2(0f, -384f), new Vector2(770f, 82f));
            Button restart = ActionButton(buttonsGroup.transform, "RestartButton", "再来一次",
                new Color(0.88f, 0.28f, 0.22f), new Vector2(-205f, 0f));
            Button menu = ActionButton(buttonsGroup.transform, "MenuButton", "离职",
                new Color(0.65f, 0.86f, 0.63f), new Vector2(205f, 0f));

            view.Dimmer = dimmer;
            view.Outcome = outcome;
            view.Stamp = stamp;
            view.SalaryGroup = salaryGroup;
            view.Salary = salary;
            view.WorkGroup = workGroup;
            view.WorkLabels = workLabels;
            view.WorkValues = workValues;
            view.LootGroup = lootGroup;
            view.BestQuality = best;
            view.Rank = rank;
            view.San = san;
            view.Loadout = loadout;
            view.Comment = comment;
            view.KpiGroup = kpiGroup;
            view.KpiLabel = kpiLabel;
            view.KpiFill = kpiFill;
            view.ButtonsGroup = buttonsGroup;
            view.RestartButton = restart;
            view.MenuButton = menu;
            return root;
        }

        static Text ResultLine(Transform parent, string name, string value, float top)
        {
            Text text = Label(name, parent, value, 20, Ink, TextAnchor.MiddleLeft);
            SetTop(text.rectTransform, new Vector2(0f, top), new Vector2(710f, 29f), new Vector2(0f, 1f));
            return text;
        }

        static Button ActionButton(Transform parent, string name, string label, Color color, Vector2 position)
        {
            Image image = Image(name, parent, color);
            SetCenter(image.rectTransform, position, new Vector2(340f, 76f));
            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.03f, 0.04f, 0.07f, 0.85f);
            outline.effectDistance = new Vector2(4f, -4f);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text text = Label("Label", image.transform, label, 34, Ink, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 6f, 6f, 4f, 4f);
            text.fontStyle = FontStyle.Bold;
            return button;
        }

        static GameObject Badge(Transform parent, string name, string value, Color color, Vector2 topPosition)
        {
            Image image = Image(name, parent, color);
            SetTop(image.rectTransform, topPosition, new Vector2(96f, 38f), new Vector2(0.5f, 1f));
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            Text text = Label("Text", image.transform, value, 20, Ink, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, 2f, 2f, 2f, 2f);
            text.fontStyle = FontStyle.Bold;
            return image.gameObject;
        }

        static GameObject Root(string name)
        {
            GameObject root = UiObject(name, null);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return root;
        }

        static GameObject UiObject(string name, Transform parent)
        {
            GameObject value = new GameObject(name, typeof(RectTransform));
            if (parent != null) value.transform.SetParent(parent, false);
            return value;
        }

        static Image Image(string name, Transform parent, Color color)
        {
            GameObject value = UiObject(name, parent);
            Image image = value.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static Image Bar(string name, Transform parent, Color backgroundColor, Color fillColor, out Image background)
        {
            background = Image(name, parent, backgroundColor);
            background.raycastTarget = false;
            Image fill = Image("Fill", background.transform, fillColor);
            Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
            return fill;
        }

        static Text Label(string name, Transform parent, string value, int size, Color color, TextAnchor alignment)
        {
            GameObject holder = UiObject(name, parent);
            Text text = holder.AddComponent<Text>();
            text.font = Font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        static void SetCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void SetTop(RectTransform rect, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(pivot.x, 1f);
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void SetBottom(RectTransform rect, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(pivot.x, 0f);
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect, float left, float right, float bottom, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        static void SetTopStretch(RectTransform rect, float top, float height, float horizontalInset)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(horizontalInset, -top - height);
            rect.offsetMax = new Vector2(-horizontalInset, -top);
        }

        static void SetBottomStretch(RectTransform rect, float bottom, float height, float horizontalInset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(horizontalInset, bottom);
            rect.offsetMax = new Vector2(-horizontalInset, bottom + height);
        }

        static void EnsureFolders()
        {
            EnsureFolder("Assets/_Game", "UI");
            EnsureFolder(UiRoot, "Art");
            EnsureFolder(UiRoot, "Resources");
            EnsureFolder(ResourcesFolder, "Prefabs");
        }

        static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        static void ConfigureMainTexture()
        {
            TextureImporter importer = AssetImporter.GetAtPath(MainArtPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Main menu source image is missing: " + MainArtPath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }

        static T Required<T>(string path) where T : Component
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(asset != null, "Missing UI prefab: " + path);
            T component = asset.GetComponent<T>();
            Require(component != null, path + " has no " + typeof(T).Name + ".");
            return component;
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
