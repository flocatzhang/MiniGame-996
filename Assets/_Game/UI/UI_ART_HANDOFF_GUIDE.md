# Office Hell UI Prefab 美术接入说明

> 面向 UI 美术、技术美术和后续接手开发。本文档对应当前工程中的 **6 个可编辑 UI Prefab**，说明每个节点的用途、应该挂什么图片，以及哪些内容会在运行时被代码更新。

## 1. 本次改动概览

原先由 Controller 在运行时临时创建的界面，已经改为真正的 Unity Prefab。UI 可以直接在 Inspector 中调整层级、锚点、尺寸、颜色、图片、字号和间距，运行时不会重新生成并覆盖这些布局。

本次总交付数量是 **6 个 Prefab，不是 4 个**：第一阶段的主界面、卡牌面板、卡牌单体、结算页共 4 个，后续新增战斗 HUD 和下班过场 2 个。全部位于：

`Assets/_Game/UI/Resources/Prefabs/`

| Prefab | 用途 |
| --- | --- |
| `UIMainMenu.prefab` | 游戏主界面和“打卡上班”按钮 |
| `UIHud.prefab` | 周一到周六战斗中的人物状态、09:00→21:00 工作时钟、摸鱼充能条、KPI、武器槽和装备槽 |
| `UIOffWork.prefab` | 每天战斗结束后的老板对话过场 |
| `UICardPanel.prefab` | 升级三选一的全屏遮罩、标题和三卡容器 |
| `UICardItem.prefab` | 三选一中的单张可复用卡牌 |
| `UIResult.prefab` | 最终结算的工资、工作统计、装备摘要和按钮 |

配套的 `UIMainMenuView`、`UIHudView`、`UIOffWorkView`、`UICardPanelView`、`UICardView`、`UIResultView` 保存 Inspector 引用和少量 UI 样式数据；Controller 负责把游戏数据写进这些引用，不再负责搭建 UI 层级。

## 2. UI 美术先看这里

### 2.1 哪些内容可以直接修改

以下修改通常不需要改代码：

- 调整 `RectTransform` 的锚点、位置、尺寸和间距。
- 给现有 `Image` 或 `RawImage` 替换图片。
- 调整字号、字体样式、文本颜色、对齐方式、描边和阴影。
- 调整面板、按钮、槽位和进度条的配色。
- 给节点增加纯装饰子节点，例如角标、螺丝、胶带、底纹和光效。
- 调整 `Button` 的 Normal、Highlighted、Pressed、Selected 颜色。

### 2.2 哪些操作需要特别小心

- 不要删除 Prefab 根节点上的 `...View` 组件。
- 不要让 View 组件中的字段变成 `None`。这些字段是代码寻找文字、按钮、进度条和图标的入口。
- 节点可以改名或移动，但改完必须确认根节点 View 组件仍然引用正确对象。
- `WeaponSlots` 必须保持 6 个，`ArmorSlots` 必须保持 3 个，`WorkLabels/WorkValues` 必须各保持 3 个且顺序一致。
- `CardContainer` 中不要手工摆放 3 张固定卡。运行时会从 `UICardItem.prefab` 实例化并复用 3 张卡。
- 不要把 UGUI `Text` 擅自替换成 TextMeshPro。本项目当前继续使用 UGUI 和 `FontProvider`。
- 不要改变卡牌、武器、装备图标的资源目录或文件 key；如果要改 key，需要程序一起修改映射。
- 新增或删除动态槽位、动态统计行、按钮行为时，需要程序配合。

### 2.3 运行时会覆盖的属性

有些属性在 Prefab 中可以预览，但进入游戏后会由数据覆盖：

- 所有动态文字，例如工资、SAN、工作时钟、KPI、卡牌名称和结算明细。
- SAN、经验、摸鱼 CD、KPI、Boss 血量等进度条的 `fillAmount`。摸鱼条从 0% 向 100% 充能，100% 表示技能就绪。
- 卡牌框、品质边框、强调条、页脚、图标底板和标题的“卡牌设计 key / 装备品质”颜色。
- 卡牌的“推荐”“NEW”显示状态。
- 卡牌、武器和装备的动态图标。
- HUD 人物头像、下班过场老板立绘。
- 过场暗幕透明度、结算内容的分阶段显隐。

因此，美术可以设置合理的预览值，但最终运行效果应在 Play Mode 中确认。

## 3. 图片接入规则

### 3.1 框体、纸张和按钮

框体类图片建议设置：

- `Texture Type`: `Sprite (2D and UI)`
- `Mesh Type`: `Full Rect`
- 需要缩放的边框设置 Sprite Editor 的 `Border`，节点的 `Image Type` 使用 `Sliced`
- 保留透明通道，避免把动态文字烘焙在图片里

适合直接挂到 Prefab `Image` 的图片包括：卡框、状态框、头像框、纸张、标题条、按钮、槽位框、气泡、进度条背景和装饰图。这类图片文件名没有代码限制，可按美术规范组织，例如放在：

`Assets/_Game/UI/Art/`

### 3.2 全屏背景

主界面背景由 `RawImage` 显示，当前使用裁掉黑边并移除按钮后的 `MainMenuBackgroundNoButton.png`。按钮视觉禁止再烘焙进背景。替换时建议：

- 保持接近 16:9 的构图。
- 关闭 Mip Maps，Wrap Mode 使用 `Clamp`。
- 保留 `BackgroundCover` 上的 Cover/Envelope 布局，让 16:9 屏幕无黑边。
- 背景图只承载场景、人物和 Logo；“打卡上班”必须由独立 `StartButton` 节点显示和交互。

### 3.3 动态卡牌、武器和装备图标

会随游戏数据变化的图标不能只拖一张固定图到 Prefab。请把图片放入：

`Assets/_Game/UI/Resources/Icons/Cards/`

文件名必须与下表 key 完全一致，导入类型必须为 `Sprite (2D and UI)`。代码会按 `Resources/Icons/Cards/<key>` 自动加载；缺图时，卡牌会显示唯一文字缩写，HUD 槽位会保留槽位和名称但不显示图标。

| 类型 | key | 内容 |
| --- | --- | --- |
| 数值卡 | `c_atk` | 攻击力 |
| 数值卡 | `c_atk_pct` | 百分比攻击 |
| 数值卡 | `c_haste` | 攻速/冷却 |
| 数值卡 | `c_crit` | 暴击率 |
| 数值卡 | `c_critdmg` | 暴击伤害 |
| 数值卡 | `c_def` | 防御 |
| 数值卡 | `c_dodge` | 闪避 |
| 数值卡 | `c_san` | SAN/生命 |
| 数值卡 | `c_speed` | 移动速度 |
| 数值卡 | `c_luck` | 幸运 |
| 数值卡 | `c_magnet` | 拾取范围 |
| 技能卡 | `s_deep` | 深度摸鱼 |
| 技能卡 | `s_paid` | 带薪休息 |
| 技能卡 | `s_reverse` | 反向加班 |
| 技能卡 | `s_extra` | 额外技能 |
| 技能卡 | `s_mass` | 群体技能 |
| 武器/装备 | `stapler` | 订书机 |
| 武器/装备 | `keyboard` | 键盘 |
| 武器/装备 | `badge` | 工牌 |
| 武器/装备 | `headphone` | 耳机 |
| 武器/装备 | `hoodie` | 卫衣 |
| 武器/装备 | `slippers` | 拖鞋 |

卡牌 `Icon`、HUD 的 `Weapon1~6/Icon` 和 `Armor1~3/Icon` 都使用这套资源。不要在 6 个武器槽或 3 个装备槽上分别挂死某件装备的正式图。

## 4. Prefab 逐节点说明

### 4.1 UIMainMenu.prefab

用途：启动画面。只保留“打卡上班”入口；主菜单按 Esc 仍可退出。

```text
UIMainMenu
├─ BackgroundCover
└─ StartButton
   └─ Label
```

| 节点 | 组件/作用 | 应挂图片 | 注意事项 |
| --- | --- | --- | --- |
| `UIMainMenu` | 根节点，挂 `UIMainMenuView` | 不挂图 | View 的 `Background`、`StartButton`、`StartButtonImage`、`StartButtonLabel` 必须完整 |
| `BackgroundCover` | 独立 `RawImage`，只负责全屏场景背景 | `MainMenuBackgroundNoButton.png` 或其他不含按钮的背景 Texture | 与按钮是根节点下的两个同级对象；保持 Cover 铺满，允许边缘少量裁切 |
| `StartButton` | 独立 `Image + Outline + Button` | 正式“打卡上班”按钮 Sprite；没有正式图时显示黄色占位底板 | 可单独移动、缩放、换图和设置九宫格；Button 的 Target Graphic 必须指向自身 Image |
| `StartButton/Label` | 按钮文字 `Text` | 不挂图 | 默认“打卡上班”；如果正式按钮 Sprite 已经包含文字，可关闭此 Text，但不要删除 View 引用 |

`StartButton` 目前以底部居中的固定锚点布局，不再跟随 `BackgroundCover` 的 Cover 裁切。UI 可以直接调整其 RectTransform，并在 Button 的 Normal、Highlighted、Pressed 状态设置颜色或不同 Sprite。背景素材中不得再次包含按钮，否则会出现两个按钮重叠。

### 4.2 UIHud.prefab

用途：战斗中的正式 HUD。左上为人物基础状态，其下方是独立摸鱼技能充能条；中上为 09:00→21:00 工作时钟和星期/时段，右上为 KPI，下方为武器槽，右下为装备槽。一次游戏固定表达 996 的周一到周六，不使用“第一关、第二关”概念；时钟显示一天内的办公室时间投影，不是剩余战斗秒数。

```text
UIHud
├─ CharacterStatus
│  ├─ PortraitFrame / Portrait
│  ├─ Rank
│  ├─ ExpBar / Fill / ExpText
│  ├─ SanBar / Fill / Caption / Value
│  ├─ CoinBlock / Caption / Value
│  └─ KillBlock / Caption / Value
├─ SlackSkillStatus
│  ├─ SkillIcon
│  ├─ Caption
│  └─ SkillBar / Fill / SkillText
├─ BattleClock
│  ├─ WorkClock
│  └─ StagePlate / Stage
├─ KpiPanel
│  └─ KpiBar / Fill / KpiText
├─ WeaponSlots
│  └─ Weapon1~Weapon6 / Icon / Label / Cooldown
├─ ArmorSlots
│  └─ Armor1~Armor3 / Icon / Label
├─ BossBar
│  ├─ BossName
│  ├─ BossHp / Fill
│  └─ Pip1~Pip3
└─ DayBanner
```

#### 人物状态区

| 节点 | 组件/作用 | 应挂图片 | 运行时行为 |
| --- | --- | --- | --- |
| `CharacterStatus` | 左上整体底框 `Image` | 人物状态主框，可做不规则漫画边框 | 位置和尺寸来自 Prefab |
| `PortraitFrame` | 头像外框 `Image` | 人物头像框、拍立得框 | 静态装饰 |
| `Portrait` | 人物头像 `Image` | 只需预览占位 | 运行时使用玩家美术帧替换，建议开启 Preserve Aspect |
| `Rank` | 职位/等级文字 | 不挂图 | 显示当前职位和等级 |
| `ExpBar` | 经验条背景 `Image` | 经验槽底图 | 静态背景 |
| `ExpBar/Fill` | 经验条前景 `Image` | 可平铺/拉伸的经验填充图 | `fillAmount` 动态变化 |
| `ExpBar/ExpText` | 经验数字 | 不挂图 | 动态显示当前/升级所需经验 |
| `SanBar` | SAN 条背景 `Image` | 血条/SAN 槽底图 | 本游戏以 SAN 作为人物生命值 |
| `SanBar/Fill` | SAN 条前景 `Image` | 红色或主题色填充图 | `fillAmount` 和颜色动态变化 |
| `SanBar/Caption` | “SAN”标签 | 不挂图 | 静态文字，可调整风格 |
| `SanBar/Value` | SAN 数值 | 不挂图 | 动态显示当前/最大 SAN |
| `CoinBlock` | 工资/金币块底框 `Image` | 金币、工资条或纸钞底框 | 当前没有独立金币系统，显示本局按进度折算的累计工资 |
| `CoinBlock/Caption` | 工资标签 | 不挂图 | 静态文字 |
| `CoinBlock/Value` | 工资数值 | 不挂图 | 动态变化 |
| `KillBlock` | 击杀统计底框 `Image` | 骷髅/击杀数底框 | 静态框体 |
| `KillBlock/Caption` | 击杀标签 | 不挂图 | 静态文字 |
| `KillBlock/Value` | 击杀数 | 不挂图 | 动态变化 |
| `SlackSkillStatus` | 独立摸鱼技能状态框 `Image` | 技能条整体底框，可做成参考图中的鱼形长条 | 是 `UIHud` 根节点的直接子节点，可独立移动、缩放、换图，不依赖人物状态框 |
| `SlackSkillStatus/SkillIcon` | 摸鱼技能图标 `Image` | 鱼、咖啡或摸鱼技能正式图标 | Prefab 中只放占位色块；美术可直接替换 Sprite |
| `SlackSkillStatus/Caption` | “摸鱼技能”静态标签 | 不挂图 | 可保留、改样式，或在正式图自带文字时关闭显示，但不要删除其他 View 引用 |
| `SlackSkillStatus/SkillBar` | 摸鱼 CD 背景 `Image` | 技能冷却槽底图 | 静态背景，`UIHudView.SkillBackground` 指向这里 |
| `SlackSkillStatus/SkillBar/Fill` | 摸鱼 CD 填充 `Image` | 冷却/充能填充图 | 必须保持 `Filled + Horizontal + Left`；从 0% 向 100% 增长 |
| `SlackSkillStatus/SkillBar/SkillText` | 摸鱼 CD 文字 | 不挂图 | 冷却时显示“充能 50% · 3.0s”，满条时显示“就绪 100%” |

如需在 `CoinBlock` 或 `KillBlock` 中增加单独的金币、纸箱、骷髅小图标，可直接新增装饰 `Image` 子节点；只要不删除现有 Caption/Value 引用，就不需要代码支持。

#### 工作时钟与 KPI

| 节点 | 组件/作用 | 应挂图片 | 运行时行为 |
| --- | --- | --- | --- |
| `BattleClock` | 中上工作时钟主框 `Image` | 时钟主体框、电子表框 | 静态框体 |
| `BattleClock/WorkClock` | 办公室时间文字 | 不挂图 | 按当天工作进度从 `09:00` 正向推进到 `21:00`，使用 `WorkClockModel` 的分钟吸附逻辑；不是倒计时 |
| `StagePlate` | 星期/时段标签底板 `Image` | 便签、工牌或黄色标签 | 静态框体；节点名为兼容已有 View 保留，不代表关卡 |
| `StagePlate/Stage` | 星期与时段文字 | 不挂图 | 动态显示“周一 · 上午”“周三 · 午休”“周六 · 加班”等，禁止改成“第 N 关” |
| `KpiPanel` | 右上 KPI 主框 `Image` | KPI 长条外框、夹板装饰 | 静态框体 |
| `KpiBar` | KPI 条背景 `Image` | 只包含边框和空槽的 KPI 槽底图，不要把蓝色进度画进底图 | 始终显示，负责让未完成区域清晰可见 |
| `KpiBar/Fill` | 独立的 KPI 条前景 `Image` | 可水平裁切的纯色、渐变或纹理填充图 | 必须保持 `Filled + Horizontal + Left`，`fillAmount` 从 0 到当前 KPI；未挂正式 Sprite 时程序会补纯色 Sprite，保证进度仍可见 |
| `KpiBar/KpiText` | KPI 百分比 | 不挂图 | 动态显示百分比 |
| `DayBanner` | 每日开始提示文字 | 不挂图 | 出现后自动隐藏；可加不参与数据的装饰父框，但不要解除 View 引用 |

#### 武器和装备槽

| 节点 | 组件/作用 | 应挂图片 | 运行时行为 |
| --- | --- | --- | --- |
| `WeaponSlots` | 6 个武器槽的布局容器 | 不挂图 | 可调整整体位置和间距，数量保持 6 |
| `Weapon1~Weapon6` | 单个武器槽背景 `Image` | 武器槽框、胶带边框 | 槽位底色会按状态变化 |
| `Weapon*/Icon` | 武器图标 `Image` | 不在 Prefab 固定挂正式装备图 | 按装备 `Def.Id` 从 `Resources/Icons/Cards` 自动加载 |
| `Weapon*/Label` | 武器名称/等级 | 不挂图 | 动态变化 |
| `Weapon*/Cooldown` | 冷却遮罩 `Image` | 半透明暗色或放射形遮罩 | 作为 Filled Image 动态显示冷却 |
| `ArmorSlots` | 3 个装备槽的布局容器 | 不挂图 | 可调整整体位置和间距，数量保持 3 |
| `Armor1~Armor3` | 单个装备槽背景 `Image` | 装备/被动槽框 | 槽位底色会按状态变化 |
| `Armor*/Icon` | 装备图标 `Image` | 不在 Prefab 固定挂正式装备图 | 按装备 `Def.Id` 自动加载 |

进入新一局时，`Weapon1~Weapon3` 会依次显示绿色订书机、绿色键盘、绿色工牌；`Weapon4~Weapon6` 保持空槽。三件图标与绿色品质底色都由运行时代码填写，美术不要把它们直接画死在 Prefab 背景中。
| `Armor*/Label` | 装备名称/等级 | 不挂图 | 动态变化 |

#### Boss 区

| 节点 | 组件/作用 | 应挂图片 | 运行时行为 |
| --- | --- | --- | --- |
| `BossBar` | Boss 状态整体容器 | 可在该节点 `Image` 挂 Boss 条大框 | 只有 Boss 存在时显示 |
| `BossName` | Boss 名称 | 不挂图 | 动态变化 |
| `BossHp` | Boss 血条背景 `Image` | Boss 血槽底图 | 静态背景 |
| `BossHp/Fill` | Boss 血量前景 `Image` | 红色/危险色填充图 | `fillAmount` 动态变化 |
| `Pip1~Pip3` | Boss 阶段/护盾提示 `Image` | 小圆点、骷髅、警报灯等 | 显隐或颜色由运行时状态控制 |

`UIHudView` 的 Inspector 中需要保持：头像、职位、`Skill Root / Skill Background / Skill Icon / Skill Fill / Skill Text`、其他各条 Fill/Text、6 个 `WeaponSlots`、3 个 `ArmorSlots`、Boss 区和 DayBanner 引用完整。数组顺序就是屏幕槽位顺序。`Skill Root` 必须指向根节点下的 `SlackSkillStatus`，这样美术移动人物框时不会误把摸鱼条一起改掉。

### 4.3 UIOffWork.prefab

用途：每天战斗结束后的过场。保留战斗画面，在上方加暗幕，右下显示老板和气泡，并支持点击或空格跳过。

```text
UIOffWork
├─ Dimmer
├─ DayTitle
├─ DailySummary
│  ├─ Summary
│  └─ NextDay
├─ SpeechBubble
│  └─ Speech
├─ BossPortrait
└─ Hint
```

| 节点 | 组件/作用 | 应挂图片 | 运行时行为 |
| --- | --- | --- | --- |
| `UIOffWork` | 根节点，挂 `UIOffWorkView` | 不挂图 | 所有引用必须完整 |
| `Dimmer` | 全屏暗幕 `Image + Button` | 通常只用纯黑色，无需 Sprite；也可用轻微纸张纹理 | 透明度分阶段变化；该节点同时是整屏点击跳过按钮 |
| `DayTitle` | “第 N 天结束”等标题 | 不挂图 | 动态变化 |
| `DailySummary` | 当日总结底框 `Image` | 便签、报告纸、下班统计框 | 静态框体 |
| `DailySummary/Summary` | 当日击杀、工资等摘要 | 不挂图 | 动态变化 |
| `DailySummary/NextDay` | 次日预告 | 不挂图 | 动态变化 |
| `SpeechBubble` | 老板对白气泡 `Image` | 建议使用可九宫格拉伸的漫画气泡 | 静态框体，文本长度会变化 |
| `SpeechBubble/Speech` | 老板对白 | 不挂图 | 动态文字，KPI 数字可能带富文本颜色 |
| `BossPortrait` | 老板立绘 `Image` | Prefab 中可放构图占位 | 运行时使用老板美术帧替换，建议 Preserve Aspect |
| `Hint` | “点击或空格继续”提示 | 不挂图 | 到允许跳过的阶段才显示 |

注意：`Dimmer` 上的 Button 是交互入口。新增大面积装饰图片时，建议关闭装饰图的 `Raycast Target`，避免挡住点击。

### 4.4 UICardPanel.prefab

用途：升级三选一的整体弹层。它只负责遮罩、标题和卡牌容器，单张卡的外观在 `UICardItem.prefab` 中调整。

```text
UICardPanel
├─ Dimmer
├─ TitleBanner
│  └─ Title
├─ CardContainer
└─ Hint
```

| 节点 | 组件/作用 | 应挂图片 | 运行时行为 |
| --- | --- | --- | --- |
| `UICardPanel` | 根节点，挂 `UICardPanelView` | 不挂图 | View 中还要引用 `UICardItem.prefab` |
| `Dimmer` | 全屏半透明遮罩 `Image` | 通常纯色即可，也可加轻微纹理 | 阻挡战斗层点击 |
| `TitleBanner` | 标题条 `Image` | “选择你的奖励”横幅、喇叭纸条或标题框 | 静态框体 |
| `TitleBanner/Title` | 标题文字 | 不挂图 | 会显示待处理升级数量等动态内容 |
| `CardContainer` | 三张卡的布局容器 | 不挂图 | 运行时只实例化并复用 3 张 `UICardItem`；不要手工放固定卡牌 |
| `Hint` | “按 1/2/3 选择”提示 | 不挂图 | 静态操作提示 |

当前卡牌目标尺寸约为 `350 × 520`，间距约 `32`。可以在容器和卡牌 Prefab 中微调，但要同时检查 1920×1080 与 1280×720。

### 4.5 UICardItem.prefab

用途：三选一中的单张卡。该 Prefab 会被重复实例化 3 次。

```text
UICardItem
├─ Accent
├─ Kind
├─ Title
├─ IconPlate
│  ├─ Icon
│  └─ IconFallback
├─ Primary
├─ Rule
├─ Description
├─ RecommendBadge / Text
├─ NewBadge / Text
├─ Footer / FooterText
└─ KeyHint
```

| 节点 | 组件/作用 | 应挂图片 | 运行时行为 |
| --- | --- | --- | --- |
| `UICardItem` | 卡片根框 `Image + Button`，挂 `UICardView` | 可九宫格拉伸的卡牌主框 | 数值卡和技能卡按各自卡牌 key 使用独立设计色；装备卡严格使用白/蓝/黄/橙品质色 |
| 根节点 `Outline` | 卡片品质/设计边框 | 不单独挂图；作用于根 Frame | 使用完整强调色，保证品质色不会因浅色背景混合而看不清；由 View 的 `Border` 字段引用 |
| `Accent` | 顶部/侧边强调色 `Image` | 色条、胶带、发光边 | 装备取实际品质色；其他卡读取 `UICardView > Design Accents` 中对应 key 的颜色 |
| `Kind` | 类别文字 | 不挂图 | 动态显示“数值卡/装备卡/技能卡” |
| `Title` | 卡牌标题 | 不挂图 | 动态变化，颜色可能随类别变化 |
| `IconPlate` | 图标底板 `Image` | 圆形、方形或贴纸式图标底框 | 颜色可能动态变化 |
| `IconPlate/Icon` | 正式卡牌图标 `Image` | 不直接挂固定图；使用资源 key | 运行时从 `Resources/Icons/Cards` 自动加载 |
| `IconPlate/IconFallback` | 缺图缩写 | 不挂图 | 找不到 Sprite 时显示唯一缩写；有图时隐藏 |
| `Primary` | 主要数值文字 | 不挂图 | 例如“攻击力 +5” |
| `Rule` | 内容分隔线 `Image` | 1~2 像素线条或手绘划线 | 静态装饰，可拉伸 |
| `Description` | 说明文字 | 不挂图 | 动态变化，需留出两到三行空间 |
| `RecommendBadge` | “推荐”徽章容器 | 黄色贴纸、图钉标签 | 装备卡按规则显示，其他卡隐藏 |
| `RecommendBadge/Text` | 推荐文字 | 不挂图 | 保留在徽章内 |
| `NewBadge` | “NEW”徽章容器 | 红色印章、斜贴标签 | 技能卡显示，其他卡隐藏 |
| `NewBadge/Text` | NEW 文字 | 不挂图 | 保留在徽章内 |
| `Footer` | 卡片页脚 `Image` | 页脚色块、标签条 | 与边框使用同一设计色/品质色 |
| `Footer/FooterText` | 页脚说明 | 不挂图 | 装备卡明确显示“蓝色品质”等品质名称；其他卡显示成长方向 |
| `KeyHint` | 数字键提示 | 可只用文字，也可给其新增按键帽底图 | 动态显示 1、2、3 |

卡片按钮的点击区域在根节点。新增装饰层时关闭不需要交互的 `Raycast Target`，避免挡住根 Button。

`UICardView` Inspector 中的 `Design Accents` 是 16 张非装备卡的独立配色表，`Key` 与第 3.3 节资源 key 一致。UI 可以直接修改每一项的 Color，不需要改代码。装备卡不读取这张表，而是使用 `Assets/StreamingAssets/Config/Loot.xml` 中白、蓝、黄、橙四档正式品质色，避免同一品质在掉落物、HUD 和升级卡牌中出现不同颜色。

### 4.6 UIResult.prefab

用途：最终结算。背景保留战斗画面，上层暗幕，中央工资纸张按阶段揭示内容，底部提供“再来一次”和“离职/返回菜单”。

```text
UIResult
├─ Dimmer
├─ OutcomeBanner / Outcome
├─ SalaryPaper
│  ├─ Stamp
│  ├─ TopRule
│  ├─ SalaryGroup / Caption / Salary
│  ├─ MiddleRule
│  ├─ WorkGroup
│  │  ├─ Caption
│  │  ├─ WorkLabel1~3
│  │  └─ WorkValue1~3
│  ├─ LootGroup
│  │  ├─ BestQuality
│  │  ├─ Rank
│  │  ├─ San
│  │  ├─ Loadout
│  │  └─ Comment
│  └─ KpiGroup
│     ├─ KpiLabel
│     └─ KpiBackground / KpiFill
└─ ButtonsGroup
   ├─ RestartButton / Label
   └─ MenuButton / Label
```

| 节点 | 组件/作用 | 应挂图片 | 运行时行为 |
| --- | --- | --- | --- |
| `UIResult` | 根节点，挂 `UIResultView` | 不挂图 | View 引用必须完整 |
| `Dimmer` | 全屏暗幕 `Image` | 纯色或轻纹理 | 保留战斗画面但压暗背景 |
| `OutcomeBanner` | 结果横幅 `Image` | 红色警报条、印章横幅 | 框体静态 |
| `OutcomeBanner/Outcome` | 结果文字 | 不挂图 | 通关/超时显示“未达标”，失败显示“已离职” |
| `SalaryPaper` | 中央结算纸张 `Image` | 建议可九宫格拉伸的工资单、打印纸或文件纸 | 所有明细的承载底图 |
| `Stamp` | 盖章文字 | 可保留文字，也可新增静态印章底图 | 结果状态动态变化 |
| `TopRule`、`MiddleRule` | 分隔线 `Image` | 手绘线、打印线、虚线 | 静态装饰 |
| `SalaryGroup` | 工资区域容器 | 不挂图 | 按结算动画分阶段显示 |
| `SalaryGroup/Caption` | “累计工资”标题 | 不挂图 | 静态文字 |
| `SalaryGroup/Salary` | 工资大数字 | 不挂图 | 使用现有工资公式，完整通关最高 `¥9,999` |
| `WorkGroup` | 工作统计区域 | 不挂图 | 分阶段显示 |
| `WorkGroup/Caption` | 统计标题 | 不挂图 | 静态文字 |
| `WorkLabel1~3` | 工作项目名称 | 不挂图 | 显示数量前三项；同类工作会合并 |
| `WorkValue1~3` | 工作项目数值 | 不挂图 | 与对应 Label 一一匹配 |
| `LootGroup` | 综合结果区域 | 不挂图 | 分阶段显示 |
| `BestQuality` | 最高掉落品质 | 不挂图 | 文字和颜色动态变化 |
| `Rank` | 最终职位 | 不挂图 | 动态变化 |
| `San` | 剩余 SAN | 不挂图 | 动态变化 |
| `Loadout` | 最终装备槽摘要 | 不挂图 | 动态变化，需留足横向/多行空间 |
| `Comment` | 结算评语 | 不挂图 | 动态变化 |
| `KpiGroup` | KPI 区域容器 | 不挂图 | 分阶段显示并播放爬升动画 |
| `KpiLabel` | KPI 百分比文字 | 不挂图 | 动态变化，动画最多爬升到 99% |
| `KpiBackground` | KPI 条背景 `Image` | KPI 槽底图 | 静态背景 |
| `KpiBackground/KpiFill` | KPI 条前景 `Image` | 黄色/主题色填充图 | `fillAmount` 动态变化 |
| `ButtonsGroup` | 两个结算按钮容器 | 不挂图 | 动画结束后显示 |
| `RestartButton` | “再来一次” `Image + Button` | 可九宫格拉伸的红色/强调按钮 | 点击重新开始 |
| `RestartButton/Label` | 按钮文字 | 不挂图 | 静态文案 |
| `MenuButton` | “离职/返回菜单” `Image + Button` | 可九宫格拉伸的绿色/次级按钮 | 点击返回主界面 |
| `MenuButton/Label` | 按钮文字 | 不挂图 | 静态文案 |

结算动画通过 `SalaryGroup`、`WorkGroup`、`LootGroup`、`KpiGroup` 和 `ButtonsGroup` 整组显隐实现。不要把需要提前隐藏的文字移出对应 Group。

## 5. View 组件引用检查表

修改层级后，请在每个 Prefab 根节点的 Inspector 检查以下字段没有丢失：

| View | 必须检查的引用 |
| --- | --- |
| `UIMainMenuView` | Background、Start Button、Start Button Image、Start Button Label |
| `UIHudView` | Portrait、Rank、SAN/EXP/KPI Fill 与 Text、Coin、Kill、Skill Root/Background/Icon/Fill/Text、Work Clock、Stage、6 个 Weapon Slot、3 个 Armor Slot、Boss Root/Name/Fill/Pips、Banner |
| `UIOffWorkView` | Dimmer、Skip Button、Boss Portrait、Day Title、Speech、Summary、Next Day、Hint |
| `UICardPanelView` | Dimmer、Title、Card Container、Card Prefab |
| `UICardView` | Button、Frame、Border、Accent、Footer、Icon Plate、Icon、Icon Fallback、全部文字、Recommend Badge、New Badge、16 项 Design Accents |
| `UIResultView` | Dimmer、Outcome、Stamp、五个分组、Salary、3 对 Work Label/Value、品质/职位/SAN/装备/评语、KPI、两个按钮 |

节点改名本身不会让引用失效；删除再新建同名节点会让原引用失效，必须重新拖拽绑定。

## 6. 字体说明

当前项目不使用 TextMeshPro。运行时会通过 `FontProvider` 为传统 UGUI `Text` 统一应用支持中文的字体，所以只在单个 Prefab 的 Text 上更换 Font，进入 Play 后可能被统一字体覆盖。

UI 可以自由调整每个 Text 的：

- Font Size
- Font Style
- Alignment
- Line Spacing
- Color
- Outline / Shadow

如需正式替换全项目字体，请让程序同步修改 `FontProvider`，不要逐节点硬换。

## 7. 推荐美术资源清单

以下是建议准备的正式图片，不要求必须使用这些文件名；只有第 3.3 节的 22 个动态图标 key 必须固定命名。

- 通用：面板框、纸张底、标题条、主按钮、次按钮、分隔线、进度条底/填充、槽位框、推荐贴纸、NEW 印章。
- HUD：人物状态框、头像框、经验条、SAN 条、工资块、击杀块、独立摸鱼 CD 条、时钟框、星期/时段便签、KPI 框、武器槽框、装备槽框、Boss 血条和阶段点。
- 下班过场：全屏暗幕纹理、每日总结框、漫画气泡；老板正式立绘仍由角色美术资源提供。
- 卡牌：可染色卡框、清晰的品质边框、图标底板、页脚、推荐贴纸、NEW 印章，以及 22 张动态图标。数值卡和技能卡不再整类共用一种颜色，由 `Design Accents` 分别配置。
- 结算：结果横幅、工资纸张、盖章装饰、KPI 条、“再来一次”和“返回菜单”按钮。

## 8. 分辨率与交互验收

每次完成一轮 Prefab 调整后，至少检查：

1. 1920×1080：所有界面与参考构图一致，无重叠、无遮挡。
2. 1280×720：背景无黑边，卡牌、工资纸和按钮不出屏。
3. 主界面：点击黄色区域可以开始；悬停和按压反馈位置准确；Esc 可以退出。
4. HUD：中间时钟从 09:00 向 21:00 正向推进，不出现 00:40 等剩余秒数；标签按周一到周六显示星期和时段，不出现“第 N 关”；独立摸鱼条能显示 0%→100% 和剩余秒数；6 个武器槽、3 个装备槽完整。
5. 三选一：鼠标点击和数字键 1/2/3 对应同一张卡；连续升级仍只有 3 张卡。
6. 下班过场：老板、气泡、总结可读；点击暗幕或按空格可继续。
7. 结算：分别查看未达标、超时和已离职；工资、前三项统计、品质、职位、SAN、装备和 KPI 均不溢出。
8. 新增装饰图片不会挡住主菜单按钮、卡牌按钮、过场整屏按钮和结算按钮。

## 9. Prefab 生成命令

初始 Prefab 由 Unity 菜单命令生成：

`Office Hell > Create Missing UI Prefabs`

该命令只创建缺失资源。如果同名 Prefab 已存在，会拒绝覆盖，避免后续美术修改被生成脚本抹掉。因此正常美术工作应直接编辑现有 Prefab，不需要反复运行该命令。

## 10. 当前美术边界

- 主界面参考图已经裁掉黑边，并另存为不含按钮的背景素材；按钮由 Prefab 中独立的 `StartButton` 承载。
- HUD、升级页、下班过场和结算页目前主要是可替换的色块/文字占位，参考图用于布局和风格方向，不是已切好的正式 UI 图。
- 系统暂时没有独立金币、存档或局外经济。HUD 的“金币/工资”区域显示本局按进度折算的累计工资，不应在美术文案中承诺可消费金币。
- SAN 即当前生命值；摸鱼条表示技能冷却/充能，空条为刚释放、满条为就绪；KPI 是当天工作目标完成度。全局进程是周一到周六，不是六个通用关卡。

如果美术希望改变动态内容的数量、增加新的游戏字段、把所有 Text 升级为 TMP，或修改图标 key/目录，请在改 Prefab 前先与程序确认。
