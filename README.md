# 办公室地狱 · 可玩验证版

Unity **2022.3.47f1** 独立工程。玩法、美术与正式音频均已接入，同时保留 XML 热重载，供策划直接调参验证节奏。

工程根目录 **`E:\OfficeHell`**，与 Free Fire 主工程完全分离，不在 `FFPeforce` 的 Perforce 工作区内。

本文件面向**人类使用者**：怎么打开、怎么改配置、怎么构建、踩过哪些坑。
架构与设计逻辑的完整说明在 **[`AGENTS.md`](AGENTS.md)**，那份是给接手的 AI 代理看的，也是数值与模块清单的唯一准。

---

## 1. 打开与运行

editor 是 **重打包的定制版**（`E:\Unity2022.3.47f1`），**Unity Hub 识别不到**——它的 `Unity.exe` 在 `Data\` 下而非 Hub 期望的 `Editor\` 下。用下面任一种打开：

- **启动器**：双击 `E:\Unity2022.3.47f1\Unity2022.3.47.exe`，在文件夹选择框里选 `E:\OfficeHell`
- **命令行**：`E:\Unity2022.3.47f1\Data\Unity.exe -projectPath E:\OfficeHell`

首次在新机器上执行 `Data\Unity.exe` 会弹系统验证框，点通过即可（见 editor 目录下 `如何使用unity package 2022.pdf` 第 5 条）。

打开后：

1. 自动执行 `Office Hell/Setup Project`：写入 16:9 横屏 PlayerSettings、创建并登记 `Assets/_Game/Scenes/Main.unity`。
2. 直接 Play 即可。**任意空场景都能跑** —— `GameApp` 由 `[RuntimeInitializeOnLoadMethod]` 自举，相机、地板、Canvas、面板全部代码构建。

首次打开会重建 `Library/`，约 20 秒。`Library/` `Build/` `Logs/` `UserSettings/` `Tools/Verify/bin|obj` 全部可删可再生，换机或迁移目录时直接丢掉。

工程仍保持**零 prefab、零自定义材质、零 ScriptableObject、空场景自举**。地图、Logo、角色帧与画饼位于 `Assets/_Game/Art/`；23 个正式音频派生资源位于 `Assets/_Game/Audio/`。角色缺图时回退 `PrimitiveFactory`，音频缺失时回退 `Synth`，单个坏资源不会阻断运行。

### 包依赖：内置管线，非 URP

这套定制 editor **重构了 PackageManager**：没有 registry，包必须内嵌在 `Packages/` 下。计划原定 **URP 2D**，实测 URP 14.0.11 的 5 个传递依赖只凑得到 4 个：

| 依赖 | 本地情况 |
| --- | --- |
| `com.unity.render-pipelines.core@14.0.11` | editor `BuiltInPackages` 内有 |
| `com.unity.shadergraph@14.0.11` | editor `BuiltInPackages` 内有 |
| `com.unity.render-pipelines.universal-config@14.0.9` | editor `BuiltInPackages` 内有 |
| `com.unity.mathematics@1.2.1` | 全局包缓存内有 |
| **`com.unity.burst@1.8.9`** | **只有 `1.3.4`，差 5 个大版本，不能凑** |

`burst` 带原生二进制且 URP 的 2D renderer 依赖其新版 job API，降版不可行，因此改为**内置渲染管线**。

粗模全部表现只用 `Sprites/Default` 与 UGUI，**两条管线下像素输出一致**，无功能损失。要接 URP 只需补一个 `com.unity.burst@1.8.9` 放进 `Packages/`，其余四个从上表位置拷进去即可，**代码零改动**。

`com.unity.ugui` 已按主工程做法**内嵌**到 `Packages/com.unity.ugui/`，并删除其 `Tests/`：该测试程序集依赖 `com.unity.test-framework`（NUnit），同样取不到，留着会产生 60+ 条 `CS0246` 阻塞全工程编译。

## 2. 操作

| 输入 | 行为 |
| --- | --- |
| 鼠标移动 | 角色跟随鼠标 |
| WASD / 方向键 | 备用移动，优先级高于鼠标 |
| 攻击 | 全自动，无需输入 |
| 摸鱼技能 | CD 到自动释放 |
| 鼠标点击 / 数字键 `1`-`3` | 升级时选卡。键盘通道不是可选项：中途去摸鼠标会打断全局唯一的操作方式 |
| `F1` | 调试面板 |
| `F5` | 重载 XML（不用退出 Play） |
| `R` | 0 帧重开 |
| `Esc` | 返回主界面 / 主界面下退出 |

## 3. 配置

全部配置在 `Assets/StreamingAssets/Config/`，策划可直接改，构建后 exe 旁边同样可改。

| 文件 | 内容 |
| --- | --- |
| `Days.xml` | 六天节奏、`Scaling` 成长公式、`Clock` 显示档位、`SpawnBand` 刷怪环带 |
| `Enemies.xml` | 9 种敌人数值 + `behavior` / `behaviorParam` |
| `Weapons.xml` | 3 把武器（投射 / 落点 AOE / 环绕）× 4 品质档位 + 品质系数 |
| `Player.xml` | 玩家属性、摸鱼技能、相机、场地、成长曲线、咖啡 |
| `Loot.xml` | 品质权重与表现、保底、词缀、防具底材、武器/防具掉率拆分 |
| `Cards.xml` | 升级三选一权重、按天装备品质、卡池 |
| `Views.xml` | 形象：Sprite 帧组、显示高度、动画帧率，以及缺图时的颜色 × 尺寸 × 形状 |
| `Audio.xml` | 正式 Clip、逻辑总线、节流/并发、Synth 回退、4 段 BGM 与 ducking 参数 |

改完按 `F5`，Console 会打出一份解析报告：条目数量 + 全部问题清单。**解析失败不抛异常**，坏行降级为默认值继续跑。

每个 XML 顶部与关键节点都写了取舍理由，改数值前先读注释：`Days.xml` 的六天总时长 420 秒是经验曲线、掉落经济、保底计时三者共同的解算基准，重分配可以，改总和要连带重算。

### 校验会主动报的问题

- `enemyId` / `viewId` / `sfxId` / 卡片 `passive` 引用不存在
- 敌人速度 ≥ 玩家移速（压迫感必须来自被包围，不能来自被追上）
- 椭圆环带没有完整包住相机矩形，即 `(halfW/semiX)² + (halfH/semiY)² > 1`（会导致怪在四个对角线角落于视野内凭空出现）
- 环带最大触及范围超出场地（上下扇区每次放置都失败，方向权重会悄悄失效）
- `orange` 权重不为 0（橙装只能走脚本通道与保底，否则节奏不可控）
- 武器高品质档位数值回退
- `weaponShare + armorShare != 100`，或防具没覆盖头/身/脚三个槽位
- 职级名数量与 `maxLevel` 不符、`kpiCap != 99`、`maxSan != 99`、`stepPickupRadius >= pickupRadius`
- 天索引重复、`Fixed.atSecond` 超过当天时长、`budgetPct` 合计不为 100、权重总和为 0

### 正式音频资源

`VoiceTest/` 是交付源，不参与运行时加载。可重复执行下面的命令，把固定的 23 个已转换文件生成到 Unity 运行时目录：

```powershell
python Tools/Audio/prepare_audio.py
```

脚本会校验 14 个普通 SFX、4 个定稿掉落音、1 个低 SAN 循环和 4 段 BGM 是否齐全；只在派生目录中把邮件死亡裁到 0.30 秒并做 0.05 秒淡出，BUG 分裂保持 0.48 秒。`OfficeHellAudioImporter` 自动应用以下导入规则：

- SFX：PCM、Decompress On Load、单声道、预加载。
- Drop：Vorbis 80、Decompress On Load、立体声、预加载；黄色源文件保留 11025Hz（Unity Vorbis 运行时报告 11000Hz），其余三档为 48000Hz。
- 低 SAN Loop：Vorbis 70、Compressed In Memory、单声道。
- BGM：Vorbis 65、Streaming、立体声、后台加载。

`AudioService` 用 24 个 SFX Source、2 个交叉淡化 BGM Source 和 1 个低 SAN Loop Source 实现 SFX/UI/BGM 三条逻辑总线。四档掉落使用独立立体声 Clip，音量按 `0.50 / 0.46 / 1.00 / 0.90` 建立由轻到重的阶梯；普通 SFX 留 3dB 余量，掉落单项取回 3dB。卡片登场也使用独立正式 Clip。所有正式 Clip 都保留 Synth 参数作为加载失败回退。

## 4. 架构

完整版见 [`AGENTS.md`](AGENTS.md)，这里只列目录形状。

```
Assets/_Game/Scripts/
  Core/     GameApp  GameClock  EventBus  EventID  PoolService  SpatialGrid  KvBag  Rng
            InputProvider  SoakRunner（无人值守跑测，见第 7 节）
  Config/   ConfigManager  ConfigValidator  IConfigSource/XmlConfigSource  XmlRead  Defs
  Model/    StatSheet  StatModifier  CombatFormula  RunModel  PlayerModel  Entities  WorkClockModel
  Systems/  GameLoopDriver  GameFlowFsm  GameContext
            InputSystem  MovementSystem  CameraSystem
            SpawnSystem  SpawnBand（椭圆环带 24 扇区）
            EnemyAiSystem  EnemyBehaviors(7 种)
            WeaponSystem  WeaponBehaviors(2 种发射 + OrbitSystem 独占环绕)
            SlamSystem  OrbitSystem  TelegraphSystem
            ProjectileSystem  ProjectileFactory  CombatSystem  LootSystem
            ProgressionSystem  CardSystem  SkillSystem  ArmorSystem
  View/     ViewBinder  EntityView  PrimitiveFactory  FontProvider  WorkClockView
            JuiceService  DamageTextService  AudioService  Synth  ArtCatalog
  UI/       UIManager  UIFactory  UIControllerBase  UIContext
            UIMainMenuController  UIHudController  UICardController
            UIOffWorkController  UIResultController  UIDebugController
  Editor/   OfficeHellSetup  OfficeHellDisciplineCheck
            OfficeHellSelfTest  OfficeHellPlayModeCheck  OfficeHellBuild
```

### 三条不可破的规则

1. **Model 不引用 View，View 不写 Model，Controller 是唯一双向层。** 实体与表现通过 `ViewBinder` 的 `id → EntityView` 字典单向绑定。
2. **`Systems/` 与 `Model/` 内禁止出现 `Time.deltaTime` / `Time.timeScale`。** 由菜单 `Office Hell/Run Discipline Check` 机制化检查，不靠纪律。
3. **`Time.timeScale` 全程恒为 1。** 逻辑时间是 `GameClock`，三个通道各有唯一写入者：`Scale`（流程状态机）、`FxScale`（JuiceService 顿帧）、`DebugScale`（调试面板）。顿帧只冻结逻辑，UI 与特效继续播。

### 唯一 Tick 点

`GameApp.Update` → `GameLoopDriver.Tick()`，顺序固定：

```
Flow → Camera → [dt 闸门] → Input → Movement → Spawn → EnemyAi → Weapons → Slams
     → Orbits → Projectiles → Telegraphs → Combat → Loot → Progression → Skill → Armor
     → Compact + SpatialGrid.Rebuild
```

实体移除只发生在帧尾 `Compact()`，紧接着重建 `SpatialGrid`。所以查询返回的裸下标在整帧内都有效，死亡实体只打 `IsDead` 标记，查询方跳过即可。

无 Collider、无 Rigidbody，全部命中判定走 `SpatialGrid` + 距离平方。

## 5. 关键设计决定

| 位置 | 决定 | 原因 |
| --- | --- | --- |
| `WorkClockView` | 时钟是当天进度的只读投影，30 分钟档位跳变 | 反向依赖会造成两个真相源；周一 40 秒逐分钟显示每秒跳 18 分钟，糊成一团 |
| `CombatSystem.ResolveContact` | 无敌帧触发那一帧取所有接触敌人的 `max(contactDamage)` | 共享无敌帧会让被 20 只围住和被 1 只贴着完全一样，压迫感归零 |
| `PlayerModel._aura[3]` | 光环按通道取 `Max`，永不 `+=` | 五个 PPT 叠在一起会把玩家减速到 0 |
| `SpawnBand` | 取点失败回退到最近可用扇区，而非跳过本次生成 | 「刷怪失败就算了」等于教玩家贴墙可以让怪停下来，这个洞一局内就会被发现 |
| `SpawnBand` | 椭圆而非圆环，且轴长按包住相机矩形的不等式解 | 圆环会让左右两侧比上下远 5 个单位，玩家读作侧面的怪来得慢 |
| `JuiceService.RequestHitStop` | 内置 `0.25s` 节流 + 只接受 `>= Elite` 优先级 | 后期每秒十几次击杀，逐次顿帧会退化成幻灯片 |
| `LootSystem` | 橙装权重 0，只走 `<Fixed guaranteeDrop>` 与保底计时器 | 0.5% 概率要么十分钟不出，要么一天三件，都不可设计 |
| `Loot.xml` | 黄橙 `autoMagnet="false"`，踩取半径 0.6 | 走过去捡的那两秒就是期待感本身，做成自动吸取等于删掉高潮 |
| `LootSystem.OnEnemyKilled` | 橙装掉落瞬间归零保底计时器 | 漏写重置会连爆橙装，是这条规则最常见的实现事故 |
| `LootSystem.Decline` | 打不过身上装备的掉落折算 +3 经验 | 否则后期地面铺满无效物品，掉落反馈退化成噪音 |
| `CardSystem` | 装备卡直接塞给玩家，不掉在地上 | 全局唯一的决策不能因为没走过去而丢失 |
| `Coffee` | SAN 低于 1/3 时掉率翻倍 | 唯一一处对玩家撒谎的数值，且必须不可察觉：读作运气好，不能读作施舍 |
| `ProjectileLauncher` | 无目标时不消耗 CD，`0.12s` 后重试 | 否则空场地会白烧一轮攻速 |
| `WeaponKind` | 只有三条攻击代码路径，品质差异一律是参数 | 第四条路径意味着第四套 bug、第四套表现、第四套平衡 |
| `ConfigManager` | 全部解析非抛异常，问题聚合成一份报告 | XML 丢掉了类型检查，坏行必须能继续跑 |
| `Scaling` | 成长走全局公式，天行只在破例时覆盖（仅周六） | 6 行手填 `hpScale` 必错，且改公式要改 6 处 |
| `Day.TotalSpawn` | 由 `density * duration` 推导，不手写 | 改天长度自动得到匹配的怪量，否则每次调时长都要重算 |

## 6. 手动验收清单

- 主界面单按钮 → 进入战斗，HUD 时钟从 `09:00` 走到 `21:00`
- 当天时长到 → `下班了` 过场 3 秒 → 下一天 → 强度倍率上升
- 升级弹三选一卡片面板，游戏暂停，数字键 1-3 或鼠标均可选
- 击杀爆咖啡自动吸；`F1` 强制刷黄橙需踩到才捡，带光柱与常驻名字
- `F1` 勾无敌、拖时间缩放、跳到任意天（含直接跳周六）、强制刷武器/防具/咖啡、加升级卡、任意装配 6 把武器与 3 件防具
- 被一群怪围住时伤害明显高于单只贴身
- 周六老板三条血条逐条破，破条时 2 秒无敌 + 涌小怪
- 结算页三种结局文案不同，KPI 条爬到 99% 卡住
- `F5` 改 XML 立即生效，Console 打出解析报告
- `R` 死亡重开 0 帧，无残留实体

## 7. 自动校验

四道闸门全部可命令行执行，全部返回退出码，可直接接 CI。

> **编辑器开着项目时，批处理模式必须跑在副本上。** Unity 以 `UnityLockfile` 独占项目，同一目录再起一个 batchmode 实例会直接中止，且**退出码为空、日志不写**，看起来像崩溃而不是像被拒绝。
>
> ```powershell
> robocopy Assets ..\OfficeHellCI\Assets /MIR /NFL /NDL /NJH /NJS /NP
> ```
>
> `E:\OfficeHellCI` 是一次性副本，11 MB，删掉只是下次重新导入。下文命令里的 `<dir>` 填副本路径。

### 7.1 离线编译（约 2 秒，不启动 Unity）

引用已安装编辑器的引擎程序集 + 内嵌 ugui 源码，纯 `dotnet` 编译：

```bash
dotnet build Tools/Verify/Verify.csproj
```

`Tools/Verify/` 不在 `Assets/` 下，不参与 Unity 编译，也不进构建。

### 7.2 分层纪律检查（约 8 秒）

扫描 `Systems/` 与 `Model/` 是否出现 `Time.deltaTime` / `Time.timeScale` / `Time.time` 等。剥注释、按整词匹配，所以文档里写这些 API 不会误报。

```bash
Unity.exe -batchmode -nographics -projectPath <dir> -logFile discipline.log \
  -executeMethod OfficeHell.EditorTools.OfficeHellDisciplineCheck.RunBatch
```

菜单入口：`Office Hell/Run Discipline Check`。

### 7.3 无头逻辑自测（约 6 秒，覆盖 Config + Model + Systems，含完整六天）

`Systems/` 与 `Model/` 内没有一个 MonoBehaviour，所以整局可以在编辑器里纯逻辑跑完，不构建场景表现也不实际播放音频；自测同时校验美术与 23 个 AudioClip 的资源、时长和四类导入设置。**节奏回归从 10 分钟试玩变成 6 秒检查**，这是本工程投入产出比最高的一项。改完任何配置数值都应该跑一遍。

```bash
Unity.exe -batchmode -quit -nographics -projectPath <dir> -logFile selftest.log \
  -executeMethod OfficeHell.EditorTools.OfficeHellSelfTest.RunBatch
```

菜单入口：`Office Hell/Run Headless Self Test`。当前输出：

```
config: enemies 9, days 6, weapons 3, views 24, cards 16, issues 0
stapler damage at atk 10: white 12.0, orange 25.2
exp to reach CEO: 1103
authored combat time 420s pays 9996
aura channels: same channel takes the max, different channels coexist
clock projection monotonic and snapped to 30 minutes
ring sampling area uniform, outer half ratio 0.503
spawn band: closest point 8.62 units, side ratio 0.77 over 2400 points
day 1 (周一) closed at 40.0s: kills 33, kpi 5%, weapons 2, armour 2, 高级专员 Lv.3
day 2 (周二) closed at 90.0s: kills 115, kpi 17%, weapons 4, armour 2, 主管 Lv.4
day 3 (周三) closed at 150.1s: kills 226, kpi 35%, weapons 6, armour 3, 经理 Lv.5
day 4 (周四) closed at 220.0s: kills 379, kpi 59%, weapons 6, armour 3, 总监 Lv.7
day 5 (周五) closed at 300.0s: kills 594, kpi 92%, weapons 6, armour 3, 高级总监 Lv.8
day 6 (周六) closed at 420.0s: kills 681, kpi 99%, weapons 6, armour 3, CEO Lv.9
first level up at 7.9s, last rank on day 6, kpi peaked at 99%
run summary: 25605 frames, 6 days, peak alive 30, kills 681, cards 8, CEO Lv.9, hpScale 5.00, ended as Clear
contact damage: one weak mob 6.0, weak plus strong 12.0
spawn grace: 0.5s inside 3.0 units
BUG split: 1 death produced 2 children worth 0 exp
pity timer forced a legendary and reset to 0
auto equip: fills empty, replaces the worst, converts downgrades to exp
all three weapon kinds produced their effect
boss: 3 bars of 9999 hp, bar break advanced to phase 2, timeout ended as ClearTimeout
restart cleared entities, counters, stats, passives and equipment slots
```

断言的都是**会静默退化的规则**，不是能一眼看出来的东西：

| 检查 | 挡住的事故 |
| --- | --- |
| 环形采样外圈占比 ≈ 0.5 | 写成 `Lerp(minR, maxR, rand)` 导致内圈密度偏高 |
| 环带最近点不侵入相机 | 敌人在视野内凭空出现，尤其是四个对角线角落 |
| 光环同通道取最大值 | 写成 `+=`，五个 PPT 把玩家减速到 0 |
| 时钟单调且按 30 分钟对齐 | 时钟反向驱动玩法，出现两个真相源 |
| 一弱一强接触伤害 6 → 12 | 取首个命中而非本帧最高，被围住和被单只贴身完全一样 |
| 出生 3 单位内 0.5s 不结算接触伤害 | 怪贴脸生成直接扣血，玩家读作被偷袭 |
| BUG 分裂子代 0 经验 | 分裂会把经验曲线冲垮，提前满级 |
| 保底触发后计时器归零 | 连爆橙装 |
| 自动装备补空位 / 换最差 / 折算经验 | 换成换第一格，橙装被白装顶掉 |
| 三类武器各自产生效果 | 只有投射物真跑，另两类接口形状是猜的 |
| 破血条进下一阶段且 2 秒无敌 | 溢出伤害连破两条，三阶段塌成一阶段 |
| 首次升级在 10 秒内 | 开局十几秒毫无反馈，试玩者直接退出 |
| 最后一个职级落在最后一天 | 提前满级则末尾两天没有任何成长 |
| KPI 撞到 99 上限 | 上限没被触发，全局笑点读不出来 |
| 重开后实体/计数/属性/被动/装备槽全清 | 残留状态，重开路径是这类 bug 最密集处 |

### 7.4 Play 模式无人值守跑测（约 70 秒，覆盖 View + UI + Audio）

7.3 不构建任何表现层，所以补一道真 Play 模式：自动开局、自动跳过下班过场、自动选第一张卡、死亡自动重开、合成鼠标输入去追掉落，全程统计 `LogType.Error` 与异常，**有任何一条即失败**。

```bash
Unity.exe -batchmode -nographics -projectPath <dir> -officehell-soak 70 -logFile playmode.log \
  -executeMethod OfficeHell.EditorTools.OfficeHellPlayModeCheck.RunBatch
```

菜单入口：`Office Hell/Run Play Mode Check`（先加命令行参数才有预算）。当前输出：

```
[Soak] started, budget 70s
[Soak] finished: days closed 1, day 2, kills 94, cards 3, level 4, weapons 6, alive 8, errors 0
[PlayModeCheck] PASSED
```

选卡固定取第一张而不是随机：随机会让失败无法从日志复现，而跑测的意义就是失败可以重放。

`-officehell-soak <秒>` 同样对构建产物生效（`SoakRunner` 读的是进程命令行），可用于打包后的冒烟测试。

### 7.5 Windows 构建：必须用 Release，不能用 Development

菜单 `Office Hell/Build Windows Player (Release)`，或：

```bash
Unity.exe -batchmode -projectPath <dir> -logFile build.log \
  -executeMethod OfficeHell.EditorTools.OfficeHellBuild.BuildWindows
```

产出 `Build/Windows/OfficeHell.exe`。分享时必须发送整个 `Build/Windows/` 目录；正式音频已嵌入数据包，对方不需要 `VoiceTest/`、Unity 或工程源码。

**Development 构建在这套定制 editor 上必崩**，崩在引擎读自己序列化数据的阶段，早于任何游戏代码：

```
The file '.../OfficeHell_Data/globalgamemanagers' is corrupted!
[Position out of bounds!]  Crash!!!
```

根因是播放引擎两个变体的版本戳不一致：

| 变体 | `UnityPlayer.dll` FileVersion | 结果 |
| --- | --- | --- |
| `win64_player_development_mono` | `2022.3.47.0`（与被改过的 editor 同戳） | **崩** |
| `win64_player_nondevelopment_mono` | `2022.3.47.8962679` | 正常 |

运行时自报的引擎版本也对得上：release player 是 `2022.3.47f1 (88c277b85d21)`，editor 与 dev player 都是 `(0)`。所以 `BuildWindowsDevelopment` / `Build Windows Player (Development)` 两个入口只在换回标准版 Unity 后才可用，本机一律走 Release。

Release 构建不带 Profiler 与脚本调试，但 **`-officehell-soak` 照样生效**（`SoakRunner` 读进程命令行，不依赖 Development），所以打包后的冒烟测试不受影响：

```bash
Build\Windows\OfficeHell.exe -officehell-soak 70 -logFile smoke.log
```

`Build()` 每次先删输出目录再全量构建：增量构建会留下与 player 不匹配的数据文件，症状同样是 `globalgamemanagers is corrupted`，曾误判为 editor 缺陷。

## 8. 尚未实现（明确不在粗模范围）

- TextMeshPro 中文 SDF
- 手动穿脱装备：掉落一律**自动装备**（补空位 → 换最差 → 打不过则折算 +3 经验），没有背包与替换 UI
- 存档与解锁：一局结束即清空，没有跨局进度
- 结算页的数据上报 / 排行

商店已在 996 策划案中删除，替换为 3 秒下班过场（`UIOffWorkController`）；升级三选一与 3 防具槽均已实现，见 [`AGENTS.md`](AGENTS.md) 第 8 节。
