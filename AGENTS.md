# 办公室地狱 · 工程总览

本文件面向**接手本工程的 AI 代理**，目标是读完即可直接改代码而不需要先做一轮全库探索。
`README.md` 面向人类使用者（如何打开、如何构建、踩过的坑），部分章节写于旧策划案时期，**数值与模块清单以本文件为准**。

---

## 0. 事实卡

| 项 | 值 |
| --- | --- |
| 引擎 | Unity **2022.3.47f1**，定制重打包版，`Unity.exe` 在 `E:\unity2022\Unity2022.3.47f1\Data\` 而非 `Editor\` |
| 渲染管线 | **Built-in RP**（URP 因 `com.unity.burst` 只有 1.3.4 凑不到 1.8.9 而放弃，见 README「包依赖」一节） |
| 工程路径 | `E:\Minigame_996`，与 Free Fire 主工程完全隔离，不在 Perforce 工作区内 |
| 规模 | 自有代码约 **13k 行** / 73+ 个 cs 文件，配置 8 个 XML |
| 资产 | **零 prefab、零自定义材质、零 ScriptableObject**；美术位于 `Assets/_Game/Art/`，24 个正式音频位于 `Assets/_Game/Audio/` |
| 场景 | `Assets/_Game/Scenes/Main.unity`，内容为空；任意空场景均可运行 |
| 语言子集 | 无 `var`、无 LINQ、无 `async`；`foreach` 仅出现在解析与非逐帧路径 |
| 注释语言 | 英文；注释只写约束与取舍理由，不写代码在做什么 |

---

## 1. 玩法契约

这一节是所有代码存在的理由。改动任何数值前先确认没有违反这里的约束。

**一局 = 一周六天**。周一到周六各一场战斗，时长 `40 / 50 / 60 / 70 / 80 / 120` 秒，合计 **420 秒**。
每天之间是 **3 秒下班过场**（`OffWork`），周六结束直接进结算。没有商店。

**玩家资源是 SAN（理智）**，上限 **99**。归零即 `Fail`。全游戏所有上限都是 99，这是设计母题。

**KPI 进度条永远停在 99%**，这是全局笑点，因此 clamp 写在 `CombatFormula.KpiPercent` 里而不是 UI 层。

**9 个职级**：实习生 / 专员 / 高级专员 / 主管 / 经理 / 高级经理 / 总监 / 高级总监 / **CEO**，对应等级 1-9。
职级同时驱动升级卡装备品质、结算页文案，因此**职级曲线是锚，不是可随手调的系数**。

**三种结局**，两胜一败：

| Ending | 触发 | 文案基调 |
| --- | --- | --- |
| `Clear` | 周六打完老板三条血条 | 打赢了老板 |
| `ClearTimeout` | 活到周六 21:00，老板自己下班 | 熬到了下班 |
| `Fail` | SAN 归零 | 猝死 / 离职 |

**硬指标**：首次升级必须落在开局 **10 秒内**（实测 7.9s）。这条依赖刷怪密度而非天长度。

**老板**：三条 **9999** 血条，三阶段。阶段转换 2 秒无敌 + 召 **26** 只小怪，按加权名单混合四种而不是清一色邮件。
`ignoreScaling="true"`，逐天成长公式不得触碰它的数值。**代价是伤害也一起被冻住**：这个标记只为让 9999 照字面读出来，
但它同时让老板错过了 `dmgPerDay`，而周六的 `dmgScale` 是 2.6，一度出现 Deadline 打 31 而老板只打 20 的局面。
老板只在周六出现，所以 `contactDamage` 直接按周六的值手写（当前 52 = 20 × 2.6）；**改 `dmgPerDay` 必须回来重算这个数**。

老板的远程是 **KPI 甩锅**：在玩家周围标地面预警圈，0.8 秒后结算，不是飞行物。
飞行文件夹打不中任何会走路的玩家——瞄的是投掷瞬间的坐标，速度 8 对玩家 4.5，跨越 6 格要飞 0.75 秒，
而那段时间玩家已经走出 3.5 格、爆炸半径只有 2.2，任意方向都是躲，包括朝老板跑。

**「画饼」是全场唯一标在老板脚下而不是玩家脚下的圈**，这是刻意的反向：甩锅必须跟着玩家走否则谁也打不中，
画饼的职责恰好相反——给老板身边那圈地标个价，让站桩输出要付钱。减速在施法瞬间生效、圈在 `pieWarn` 后结算，
所以这个技能收的正是它刚刚拿走的那段位移。圈里画的是 `Effects/Pie.png`（一张葱油饼），
`pieDamage` 写成 `contactDamage` 的份额（0.5）而不是绝对值，跟住那个手算过 `dmgScale` 的数。

**老板咽气后 2 秒直接进结算**（`GameFlowFsm.VictoryBeatSeconds`）。这 2 秒同时**跳过 `Fail` 判定并给玩家无敌**：
最后一条血条破掉时这局已经是 `Clear`，破条召出来的 26 只还在走，让它们把胜利改判成猝死是纯粹的账目错误。

---

## 2. 分层与依赖方向

```
Assets/_Game/
  Art/        地图、品牌、画饼、42 张角色动画帧
  Audio/      15 个 SFX、4 个立体声 Drop、低 SAN Loop、4 段 BGM（Resources/Audio）
  Editor/     OfficeHellSetup  OfficeHellDisciplineCheck  OfficeHellSelfTest
              OfficeHellPlayModeCheck  OfficeHellBuild  OfficeHellAudioImporter
  Scripts/
    Core/     GameApp  GameClock  EventBus  EventID  PoolService  SpatialGrid
              KvBag  Rng  InputProvider  SoakRunner
    Config/   ConfigManager  ConfigValidator  Defs  XmlRead  IConfigSource
    Model/    RunModel  PlayerModel  Entities  StatSheet  CombatFormula  WorkClockModel
    Systems/  GameLoopDriver  GameFlowFsm  GameContext
              + 17 个系统 + EnemyBehaviors / WeaponBehaviors / SpawnBand
   View/     ViewBinder  EntityView  PrimitiveFactory  Synth  FontProvider
             ArtCatalog  GameIconCatalog  WorldFxCatalog  LootBeamFx
             JuiceService  AudioService  DamageTextService  WorkClockView
    UI/       UIManager  UIFactory  UIControllerBase  UIContext  + 6 个 Controller
```

依赖只允许单向：**Config ← Model ← Systems ← View / UI**。
`Model` 与 `Systems` 内**没有任何 MonoBehaviour**，这是整局能在无场景、无表现、无音频的情况下纯逻辑跑完的前提，也是 7 秒回归的来源。

### 三条不可破的规则

1. **Model 不引用 View，View 不写 Model。** 实体与表现通过 `ViewBinder` 的 `Dictionary<int, EntityView>` 单向绑定，键是 `RunModel.NextId()` 发的实体 id。
2. **`Systems/` 与 `Model/` 内禁止出现 `Time.deltaTime` / `Time.timeScale` / `Time.time` 等引擎时间 API。** 由 `OfficeHellDisciplineCheck` 机制化扫描，剥注释后整词匹配。
3. **`UnityEngine.Time.timeScale` 全程恒为 1**，只在 `GameApp.Awake` 里写一次。逻辑时间是 `GameClock`。

---

## 3. 时间系统

`GameClock` 是静态类，三个缩放通道各有**唯一写入者**，相乘得到 `Delta`：

| 通道 | 唯一写入者 | 用途 |
| --- | --- | --- |
| `Scale` | `GameFlowFsm.Enter` | 只有 `Battle` 是 1，其余状态 0。状态切换即暂停 |
| `FxScale` | `JuiceService` | 顿帧降到 0，表现层不受影响 |
| `DebugScale` | 调试面板 | 验证用，玩法代码永不触碰 |

`GameClock.Delta = unscaledDelta * Scale * DebugScale * FxScale`，单帧 `unscaledDelta` 被 clamp 到 0.1s，防止编辑器卡顿导致实体瞬移。
`GameClock.Now` 是累计缩放时间，所有 CD / 无敌帧 / 出生宽限都以它为基准，因此暂停期间不会偷跑。

因为引擎时钟没被动过，**顿帧只冻结逻辑，UI 动画、伤害数字、粒子继续播**。

---

## 4. 唯一 Tick 点

`GameApp.Update` 是全工程唯一的 `Update`。没有任何实体有自己的 `Update`，没有协程驱动玩法。

```
GameApp.Update:
    HandleHotkeys()
    GameClock.Tick(Time.unscaledDeltaTime)      // 唯一一处引擎时间→逻辑时间的桥
    GameLoopDriver.Tick()

GameLoopDriver.Tick():
    Flow.Tick(UnscaledDelta)                    // 冻结状态也要能超时，故走 unscaled
    Camera.Tick(UnscaledDelta)
    if (GameClock.Delta <= 0) return            // 唯一的 dt 闸门
    Input → Movement → Spawn → EnemyAi → Weapons → Slams → Orbits
          → Projectiles → Telegraphs → Combat → Loot → Progression → Skill → Armor
    RunModel.Compact() + SpatialGrid.Rebuild()

GameApp.LateUpdate:
    ViewBinder.Sync → JuiceService → AudioService → DamageTextService → UIManager.Tick
```

### 帧内契约

**实体移除只发生在帧尾 `Compact()`**，紧接着重建 `SpatialGrid`。
死亡只打 `IsDead` 标记，查询方自行跳过。所以 `SpatialGrid` 查询返回的**裸 List 下标在整帧内保持有效**——这是网格查询不返回对象引用的原因，也是改动系统顺序时最容易破坏的隐式约定。

无 Collider、无 Rigidbody、无物理。全部命中判定走 `SpatialGrid` + 距离平方比较。

---

## 5. 事件总线

`EventBus` 是 `Dictionary<EventID, Action<EvtArg>>`，同步派发，无队列。`EvtArg` 是可复用结构，字段为 `I0/I1`、`F0/F1`、`P0/P1`（`Vector2`）、`O0`（`object`）、`S0`（`string`）。

**约定**：需要传实体时放 `O0` 传模型引用本身，不要用 `I1` 复用语义。历史上 `LootDropped` 曾用 `I1` 同时表达 `LootKind` 与 `Quality`，靠 `O0 == null` 区分金币与装备，是本工程修过的典型脆弱写法。

`EventID` 全量（`Core/EventID.cs`）：配置重载、状态与流程（`GameStateChanged` `RunStarted` `RunEnded` `DayStarted` `DayCleared`）、敌人（`EnemySpawned` `EnemyDamaged` `EnemyKilled`）、玩家（`PlayerDamaged` `PlayerDodged` `PlayerHealed` `PlayerShielded` `PlayerShieldBroken` `PlayerGuarded` `PlayerDied` `PlayerLevelUp` `PlayerRankUp`）、掉落（`LootDropped` `LootPicked`）、装备与攻击（`WeaponEquipped` `ArmorEquipped` `EquipDeclined` `WeaponFired` `SlamLanded` `SelectAll` `OrbitRebuilt` `SkillCast` `CoffeeDrunk`）、老板（`BossSpawned` `BossTelegraph` `BossPieCast` `BossPhaseChanged` `BossClockedOut`）、卡片（`CardsOffered` `CardPicked`）。

表现层（`JuiceService` / `AudioService` / `DamageTextService`）**只订阅事件，从不被系统直接调用**。新增一种打击感或音效的正确做法是订阅事件，而不是在系统里插一行播放调用。

---

## 6. 配置层

全部配置在 `Assets/StreamingAssets/Config/`，构建后位于 exe 旁边，策划可直接改。运行中按 **F5** 热重载。

| 文件 | 内容 | 主要 Def |
| --- | --- | --- |
| `Days.xml` | 六天节奏、成长公式、时钟档位、刷怪环带 | `DayDef` `SpawnerDef` `FixedSpawnDef` `ScalingDef` `ClockDef` `SpawnBandDef` |
| `Enemies.xml` | 9 种敌人数值 + `behavior` / `behaviorParam` | `EnemyDef` |
| `Weapons.xml` | 3 把武器 × 4 品质档位 + 品质系数 | `WeaponDef` `WeaponTierDef` `QualityCoefDef` |
| `Player.xml` | 玩家属性、摸鱼技能、相机、场地、成长、咖啡 | `PlayerDef` `SkillDef` `CameraDef` `ArenaDef` `ProgressionDef` `CoffeeDef` |
| `Loot.xml` | 品质权重与表现、保底、词缀、防具底材、掉率拆分 | `LootDef` `QualityDef` `AffixDef` `ArmorBaseDef` |
| `Cards.xml` | 升级三选一权重、按天品质下限与升档概率、卡池、保留手牌 | `CardPoolDef` `CardDef` `WeaponHandDef` |
| `Views.xml` | 形象：形状 × 颜色 × 尺寸 | `ViewDef` |
| `Audio.xml` | 正式 Clip、逻辑总线、节流/并发、Synth 回退、BGM 淡化与 ducking | `AudioDef` `SfxDef` `BgmDef` |

### 品质阶梯

四档是 **绿 / 蓝 / 紫 / 橙**（`Quality.Green` / `Blue` / `Purple` / `Orange`，值 0-3），武器档位、掉落、升级卡三处共用同一套枚举与同一张系数表 `Quality green="1.0" blue="1.25" purple="1.6" orange="2.1"`（在 `Weapons.xml`）。
早期用的是白 / 蓝 / 黄 / 橙，`ConfigManager.ParseQuality` 与武器系数表**仍然接受旧拼写**，只为让旧配置放在新 exe 边上时不至于整档错位。写新配置一律用新名。

两处按**名字**而不是按序号匹配，改名时最容易静默失效：

- `Loot.xml` 的 `LateBonus applyTo` 与 `Quality.ToString()` 逐词比对，拼错不报错、只是后期加权彻底不生效。`ConfigValidator` 会逐项解析并报出匹配不上的词。
- 四档掉落音效 id 是 `sfx_drop_green/blue/purple/orange`，对应四个**物理 wav 文件**（`Assets/_Game/Audio/Resources/Audio/Drop/`、`VoiceTest/` 源、`prepare_audio.py` 的 `DROP` 清单三处同名）。响度补偿跟的是素材不是档位，换素材要重算。

代码里所有档位判定一律写成 `>=` 比较，不要按名字分支——品质只应改参数，不应开新代码路径。

### 两套档位词汇

同一档在界面上有两个词，分工固定，不要互相替换：

| 场合 | 用词 | 来源 |
| --- | --- | --- |
| **装备名**里的品质位 | 初级 / 高级 / 资深 / 专家 | `Loot.xml` 的 `Quality rankName`，读 `QualityDef.RankName` |
| **界面上的档位标签**（卡面页脚、结算「最高品质」、调试面板） | 绿色 / 蓝色 / 紫色 / 橙色 | 各 Controller 自己的 `QualityName` |

装备名的公式是 **`词缀 + 品质 + 底板`**，例如「卷王的资深格子衬衫」。三段都可能缺席：武器不掷词缀、绿装 `affixCount=0`，缺的那段直接不出现。
名字里用职级词而不是颜色词，是因为颜色已经由光柱、描边、页脚各说了一遍，而装备跟着玩家一起升职是这套系统白捡的一个梗。
`rankName` 缺省时 `QualityDef.RankName` 回落到编译期的四个词而不是返回空串：空串会让四档在名字里同时消失，读起来像「这两件是同一个东西」而不像配置坏了。
**四个词必须互不相同**，否则两档撞成同一个名字，`ConfigValidator` 与 `OfficeHellSelfTest` 各拦一道。

装备卡的标题就是 `RankName + 底板名`，且 `CardSystem.Grant` 会把 `offer.DefId` 透传给 `LootSystem.SpawnWeapon/SpawnArmor` 的三参重载。
**不要退回两参版本**：两参版本自己随机抽底板，卡面写着「专家订书机」而实际发下去的三次里有两次是别的武器。

### 解析纪律

- **解析永不抛异常**。坏行降级为默认值继续跑，问题聚合成一份报告打到 Console。XML 丢掉了编译期类型检查，所以运行不能被一个错字打断。
- `behaviorParam` 走 `KvBag`（`key=value;key=value`），避免为每个行为的参数各开一个 Def 类。
- **敌人名单（roster）写成 `id:weight,id:weight`，光写一个 id 就是权重 1 的单项名单**，所以旧的单 id 写法读起来完全不变。目前 `phaseAddId` / `summonId` / `splitInto` 三个键走这个格式，由 `SpawnSystem.SpawnBurst` 与 `PickFromRoster` 解析。**名单每次调用都重新解析，不做缓存**：热重载后 def 是新实例而文本没变，按文本缓存会一直发上一张表的数值。
- `WeaponTierDef` 未声明的字段**继承下一档**，所以 XML 只写增量，「绿 + 三次改动」保持可读。
- `CardDef.Value` / `Value2` 是**绿色档**，其余三档乘 `QualityCoefDef`（与武器共用，1.0 / 1.25 / 1.6 / 2.1）。`Desc` 是模板，`{v}` `{v2}` 在发牌时替换成掷出来的数——把数字写进句子里，是"攻击力 +6"熬过三轮改数的原因。
- `CardQuality` 节点的 `quality` 是**三类卡共用的品质下限**，`upgradeChance` 是逐卡掷的升一档概率。旧名 `EquipQuality` 仍会被读取，只为让旧配置放在新 exe 边上时不至于整周退回最低档。
- `WeaponHand` 把指定等级的整手牌从抽卡里拿走，换成**武器表里每把各一张、同一档**（当前 3 级蓝、6 级紫）。按「等级已达到」而不是「等级正好等于」发放，连升两级跳过 3 级时这手牌不会被吞掉；发过一次就不再发，`CardSystem.Reset` 随重开清空。武器不足 `choices` 张时**整手回退到普通抽卡**而不是发半手，`ConfigValidator` 会拦。
- 热重载后 def 是新实例，`RunModel.RebindDefs` 按 id 重新解析每个存活实体、每个武器槽、每个防具槽以及当天的 `DayDef`，否则旧数值会继续生效。

### ConfigValidator 守的规则

这些是**会静默退化**的约束，不是能一眼看出来的错误：

- 引用完整性：`enemyId` / `viewId` / `sfxId` / 卡片 `passive` 是否存在
- **敌人名单里每一项都要解析得到一个真敌人**：`phaseAddId` / `summonId` / `splitInto` 拼错的项会在刷怪时被静默跳过，整串都错就是一波空气——老板少掉三分之一压迫感而日志干干净净。权重写成 0 或非数字同样拦下：那一项永远抽不到，但看上去像是故意配的
- 敌人速度必须 < 玩家 4.5：压迫感只能来自被包围，不能来自被追上
- **椭圆环带必须完整包住相机矩形**：`(halfW/semiX)² + (halfH/semiY)² <= 1`。逐轴比较是错的，会让敌人在四个对角线角落凭空出现
- 环带最大触及范围（半轴 + 外推 + 边距）必须落在场地内，否则上下扇区每次放置都失败，方向权重会悄悄失效
- **刷怪窗口必须铺满整天**：非 `totalSpawn` 覆盖的天，最晚的 `Spawner.To` 不得早于 `duration`，否则收工前那段没有任何敌人到达
- **`knockbackCd > 0`**：归零等于恢复每次命中都击退，六个槽位一起打足以把怪永久顶在身前
- **`slowSeconds` 必须短于武器攻击间隔 `1/rate`**：否则下一次命中在减速失效前就续上，减速不再是减速而是敌人的真实速度
- `selectAllEvery > 0` 时 `selectAllPct` 不得为 0（Ctrl+A 一击不掉血）、`selectAllRadius` 必须大于 `blastRadius`（否则大招覆盖还不如它替掉的那一击）、`selectAllSharedCd` 必须 > 0（否则六个槽位同时扫）
- 声明了 `pieCd` 的敌人，四个画饼参数都要成立：`pieCount` / `pieCountLate` 至少 1（否则技能是一个没有任何提示的减速）、`pieInner < pieOuter`（否则整环撒在同一个半径上）、`pieBlast > 0`、**`pieDamage > 0`**。四种写错法都照样把红圈画出来，而红圈在 `Views.xml` 里只有一个意思，不掉血的红圈是在教玩家躲一个不存在的攻击
- `orange` 权重必须为 0：橙装只走保底与脚本通道
- `WeaponHand` 的等级不得超过 `maxLevel`、同一等级不得声明两次，且武器数量不得少于 `choices`：三条都只会让保留手牌**静默退回普通抽卡**，玩家在那一级拿到的是一手合法的卡，只是不是被安排好的那手
- 咖啡 `healOverTimeSeconds` 不得为 0（持续段会在拾取那一帧一次付清，读起来像即时回血翻倍而不像配置坏了）、`lowSanChancePct` 不得低于 `chancePct`（反死亡螺旋会反向生效）
- `LateBonus applyTo` 的每一项都必须解析得到一个档位名：拼错不会报错，只会让后期加权彻底不生效
- 四档 `rankName` 互不相同：装备名里只有这一段带档位，撞词就是两档共用一个名字，玩家读不出哪件是升级
- 数值卡与技能卡的 `desc` 必须含 `{v}`：没有占位符，四档品质在卡面上完全读不出来
- `value2` 与 `{v2}` 必须同时出现或同时不出现
- 已经是橙的那天 `upgradeChance` 必须为 0：升无可升，非零值是配置在骗自己
- 武器档位不得回退（如高品质 `projCount` 比低品质小）
- `weaponShare + armorShare == 100`；防具必须覆盖三个槽位
- `RankNames` 数量 == `MaxLevel`；`KpiCap == 99`；`MaxSan == 99`；`stepPickupRadius < pickupRadius`
- 天索引唯一、`Fixed.atSecond` 不超过天长度、`budgetPct` 合计为 100、权重合计非 0

---

## 7. 数据模型

### RunModel

一局的全部状态。**重开 = 清空这个对象 + 回收视图池**，从不重载场景（`GameApp.StartRun` 0 帧重开）。

六类实体各有 `List` + `Stack` 池，`RentXxx()` 取用，`Compact()` 归还：`Enemies` `Projectiles` `Loots` `Slams` `Telegraphs` `OrbitCards`（轨道卡不入池，随武器槽存在）。

关键字段：`DayIndex` `DayElapsed` `Day` `HpScale` `DmgScale` `SpawnDebt`（同屏上限造成的欠账，只延后不丢弃）、`KillsByType`（按 def id 计数，供结算页生成「处理邮件 328 封」）、`SecondsSinceLastLegendary`（保底计时器，只在战斗中累加）、`OrangeArmorTaken`（按 `EquipSlot` 索引，橙色防具每个部位一局只出一件）、`Ending` `BossBarsLeft`。

### PlayerModel

6 武器槽 + 3 防具槽。属性走 `StatSheet`（base + `StatModifier` 列表，按 sourceId 增删，装备摘下即撤销）。

**光环用固定长度数组 `_aura[3]` 表达，每帧头部清零，系统只允许用 `Max` 写入，永不 `+=`。** 所以五个 PPT 叠在一起仍然只减速 25%。三个通道：`MoveSlow` `AttackSlow` `EnemyHaste`。

派生值不进 `StatSheet`，而是即时计算，因为它们依赖当前 SAN 或当前时间：`EffectiveMoveSpeed(now)` `EffectiveHaste(now)` `EffectiveDef()` `MagnetRadius` `ImmuneToControl`。

`PendingLevelUps` 是队列而非布尔：一帧内连升两级不能吞掉一次选卡。

**五个摸鱼被动的数值存在 `_passiveValue[5]` / `_passiveValue2[5]` / `_passiveQuality[5]` 里，`Passives` 位标记只回答"有没有"。** 掷出来的量在发牌时就定了，`SkillSystem` 只读 `SkillInvulnSeconds` / `SkillCd` / `SkillHealPct` / `SkillPushRadius` / `SkillDamagePct` / `SkillStunSeconds` 六个 getter，不认识品质也不认识卡。同一个被动再次取走是**替换**而非叠加：橙色之上叠一张绿色该算什么，没有好答案。索引由 `PassiveIndex` 映射，新增被动要同时加位、加索引、加 getter。

三个只服务于防具的字段：`ShieldUntil`（耳机蓝色护盾的自然过期，**过期不算破碎**，不发事件也不触发橙色反击）、`HitsSinceGuard`（格子衫橙色数到 5，换身体防具时清零）、以及拖鞋紫色的咖啡渍环形数组 `_stainPos/_stainUntil[10]`。咖啡渍是全工程唯一没有实体 id 的可见物，`ViewBinder` 用 `StainIdBase` 起的负数块给它发视图 id，因为 `RunModel.NextId` 只发正数。

### CombatFormula

纯静态函数，一个函数对应策划案一条公式，无状态，可直接对表检查：

| 公式 | 实现 |
| --- | --- |
| 武器伤害 | `(baseDamage + atk * atkCoef) * qualityCoef` |
| 承受伤害 | `raw * 99 / (99 + def)`，99 DEF 恰好减半 |
| 攻击间隔 | `baseInterval * 99 / (99 + haste)`，与 DEF 同形，CD 永不到 0 |
| 升级经验 | `ceil(coef * L^power)`，当前 `coef=12` `power=1.55` |
| KPI | `min(cap, floor(kills / target * 100))`，cap 99 |
| 掉落权重 | `base * (1 + luck/100 * qualityTier) * lateBonus`，幸运只放大高档 |
| 属性 roll | `base * qualityCoef * Random(0.85, 1.15)`，主属性与词缀共用 |
| 工资 | `finalSalary * servedSeconds / totalSeconds`，满周恰好 9999 |

`Base99 = 99f` 既是主题也是 DEF / HASTE 的衰减常数。

---

## 8. 系统层

| 系统 | 职责 | 扩展点 |
| --- | --- | --- |
| `GameFlowFsm` | 6 状态机 + `GameClock.Scale` 闸门 | 状态：`MainMenu` `DayStart` `Battle` `LevelUp` `OffWork` `Result`。**老板死后 2 秒收工**：这一段排在 `Fail` 判定**之前**并附带无敌，胜负已定的 2 秒里不接受翻盘。走逻辑时钟，所以同帧升级会暂停这个节拍而不是吃掉它 |
| `SpawnSystem` | 按天预算刷怪、同屏上限欠账、`Fixed` 定时刷、精英登场、**加权名单爆发** | 密度公式 `ceil(density * duration)`，`totalSpawn` 仅周六覆盖。预算按**累计进度表**释放而非按固定组量抽干：`interval` / `groupSize` 只决定节奏，窗口一定铺满到收工。`ramp` 是窗口末尾相对开头的到达倍率（默认 2），只改分布不改总量。`SpawnBurst` / `PickFromRoster` 吃 `behaviorParam` 的名单串（见下）|
| `SpawnBand` | 椭圆环带取点：24 扇区轮转 + 左右权重 + 外推 + 间距校验 + 边界回退 | 取点失败**回退到最近可用扇区而非跳过**：贴墙让怪停刷会在一局内被玩家发现并利用 |
| `EnemyAiSystem` | 直线追击 + 委派 `IEnemyBehavior` | `EnemyBehaviorRegistry` 字符串→实例映射 |
| `EnemyBehaviors` | 7 种行为 | `SuicideOnContact` `SplitOnDeath` `AuraMoveSlow` `AuraAttackSlow` `AuraHaste` `RangedKeepDistance` `BossSkills`。`BossSkills` 的四个技能里**只有画饼以老板为心**，`ScatterPies` 在 `pieInner`-`pieOuter` 的环带里**随机撒而不是均匀摆一圈**：均匀摆出来的圈要么彻底封死（技能变成「不许靠近」），要么每次在同样的位置留同样的缺口（技能变成免费）|
| `WeaponSystem` | 6 槽 CD 与相位错开，委派 `IWeaponBehavior` | 无目标时**不消耗 CD**，0.12s 后重试，否则空场地白烧攻速 |
| `WeaponBehaviors` | 2 种发射行为 | `ProjectileLauncher` `GroundAoe`。**`Orbit` 不在此处**：轨道卡随装备常驻，由 `OrbitSystem` 独占，`Get(Orbit)` 返回 null |
| `SlamSystem` | `GroundAoe` 落点延迟结算 | 所有落点走同一个圆判定，**Ctrl+A 不再是全屏**：以玩家为心扫 `selectAllRadius`（6.0，相机半展 9.78×5.5，纵向盖满、横向留活口），按 `selectAllPct` 打折且不带击退与减速。全屏满伤 + 全屏控制是三个效果收一份钱，会把场地清空到其余五个槽位没有目标。`selectAllSharedCd` 是**跨槽位共享**的，存在 `PlayerModel.SelectAllReadyAt`：单把键盘约 7.7s 才轮到一次，永远碰不到这个 CD；六把同时到点会在两秒内扫六次，那就不是大招而是环境光。被挡下的槽位**不推进 `AttackCount`**，下一击重试而不是作废整轮 |
| `OrbitSystem` | 环绕卡位置、同目标 CD、橙品质 tether | |
| `TelegraphSystem` | 地面预警圈到期后结算伤害、生成实体，`SummonDrop` 携带保证掉落 | 老板的四种落地攻击（甩锅 / 开会 / 落雨 / 画饼）全部走这里，敌方投射物只剩周报一种，且只做接触判定 |
| `CombatSystem` | 投射物命中、接触伤害、无敌帧、闪避、护盾、三件防具的受击反应 | 无敌帧触发帧取所有接触敌人的 **max(contactDamage)**。格子衫蓝色反弹**按位置找最近的敌人而不是按攻击者**：投射物与老板预警圈结算时已经没有攻击者可反弹，而接触伤害按位置找到的必然就是撞上来的那只。拖鞋橙色闪避位移是**朝 `Facing` 向前**，反方向弹开会把玩家推进他没在看的那半个屏幕 |
| `LootSystem` | 咖啡 / 装备掉落、词缀 roll、保底、磁吸与踩取、自动装备 | **橙色防具每个部位一局只出一件**：全周只有几件橙，两件撞同一个部位时第二件只值 3 点经验，而它照样起光柱、照样响橙色音效。三个部位都出过之后，橙色防具改发**橙色武器**而不是不发——保底计时器在调用方无条件归零，这里空手等于把这次保底扔在地上。卡片指名底板的那条路径**不过滤**（卡面写什么就必须发什么），但照样占位，防止地上再掉一件同部位的。咖啡是**即时 8% + 6 秒内 8%**，持续段由 `TickCoffeeHealing` 每帧结算：**刷新而不叠加**（即时段本就随击杀线性增长，持续段再跟着叠会让刷怪最密的周五变成最安全的一天）、**逐 tick 不发 `PlayerHealed`**（一杯会飘出七个 `+1`）、满 SAN 时**静默跳过但照常倒计时**（冻住剩余量等于把咖啡变成可囤积资源） |
| `ProgressionSystem` | 经验、等级、职级变更事件 | |
| `CardSystem` | 升级三选一：数值 45% / 装备 30% / 技能 25%，三类卡都掷品质 | 装备卡**直接塞给玩家**而不是掉在地上：唯一的决策不能因为没走过去而丢失。装备卡**不在 XML 里**，由 `CardSystem` 按 `QualityByDay` 与武器表合成，描述走 `TierBlurb`。数值卡与技能卡按 `QualityByDay` 取下限、`UpgradeChanceByDay` 逐卡掷升一档，数值乘 `QualityCoefDef`；**装备卡不掷升档**：只动数字的卡早一天出现无所谓，一档装备同时解锁一个行为，提前发出来会让后面所有配平失去参照。**数值卡不再永久离池，只能以更高品质复现并叠加**；技能卡复现时替换。一手内按 `Id` 去重，所以 id 不带品质后缀：同一张卡的两档并排放着不是选择。`WeaponHand` 声明的等级**整手改为武器表每把各一张、同一档**，绕开权重与去重：武器占 30% 卡类里的 60%、底板三选一、六个槽位又自动装备，抽卡完全可能整局没让玩家在三种攻击方式之间选过一次 |
| `SkillSystem` | 摸鱼：无敌 + 回血 + 推开，5 种被动强化 | 满 SAN 时**不发 `PlayerHealed`**，否则每 12 秒在头顶飘一个 `+0` |
| `ArmorSystem` | 耳机护盾节拍、拖鞋咖啡渍 | 护盾**上 5 秒歇 5 秒**而不是一直挂着：常亮的护盾让紫色那档的免疫控制变成永久，PPT / 老油条 / 老板画饼三条设计线同时失效。咖啡渍**按时间间隔掉而不是按移动距离**，且只在移动时掉，否则原地打转能堆出一摊永久减速场；生命期短于环形数组转一圈的总时长，新的一枚永远不会覆盖还活着的那一枚。踩上去**减速 50% 并按 ATK 的 15% 掉血**，伤害的节流写在**敌人身上（`EnemyModel.StainTickAt`，0.5 秒一次）而不是写在渍上**：渍是重叠铺开的，按渍计费会让绕一个小圈的收费是走直线的六倍，而且每帧都跳伤害数字 |
| `MovementSystem` `InputSystem` `CameraSystem` `ProjectileSystem` `ProjectileFactory` | 单一职责，各 25-75 行 | |

三个注册表是**新增内容的标准入口**：`EnemyBehaviorRegistry`（字符串键）、`WeaponBehaviors`（按 `WeaponKind` 查）、`CardSystem` 的卡片种类分派。

---

## 9. 表现层

角色与场地已有首批美术，但仍保持**零 prefab、空场景、代码构建表现**：

- `ArtCatalog` 从 `Resources/OfficeHellArt/` 缓存地图、Logo、画饼与 9 组角色帧；缺资源时回退 `PrimitiveFactory`
- `GameIconCatalog` 是**一张 22 行的 `AssetNames` 表**，键是升级卡在 `Cards.xml` 里的 id 或装备的 def id，值是 `Resources/Icon/card/` 下的文件名。卡面、HUD 六武器槽与三防具槽、地上掉落三处共用它，所以同一件东西在三个地方一定是同一张图。**表里没有的键返回 null，卡面退回 `IconFallback` 的三字母缩写**——能读、但那是占位符不是美术，`OfficeHellSelfTest` 因此拿 `Cards.xml` 的整个卡池逐张对这张表，而不是等它在屏幕上被看见
- 预警圈默认**只画圈不画主体**（`Body.enabled = false`，填充就是计时器）。`v_warn_pie` 是唯一的例外：`TelegraphFill` 给它填 `ArtCatalog.Pie`，尺寸压在圈直径的 85% 以内并随进度淡入，**长大的仍然是圈而不是饼**。画饼原本是一张铺满屏幕的 UI 大图（`UIManager.PieOverlay`），正好盖在玩家要读的那几个圈上，已删除
- `EntityView` 由 `ViewBinder` 用 `GameClock.Delta` 手动切帧，不使用 Animator、协程或额外 `Update`
- `PrimitiveFactory` 仍生成 5 种形状 Sprite，承担投射物、掉落、预警与角色缺图回退
- `Synth` 实时合成 6 种回退音（`Blip` `Thud` `Chime` `Noise` `Sweep` `Bell`），正式 Clip 缺失时自动接管
- `FontProvider` 走 `Font.CreateDynamicFontFromOSFont` 拿系统中文字体
- `GameApp.BuildFloor` 使用完整办公室地图；家具只是装饰，不引入碰撞或寻路

原始 PSD/JPG/PNG 保留在 `testAssets/`。先用 `Tools/Art/requirements.txt` 安装导出依赖，再运行
`Tools/Art/prepare_art.py` 生成派生帧。角色映射与帧数为：
`player 3 / deadline 5 / mail 6 / ppt 4 / bug 6 / report 4 / veteran 6 / leader 4 / boss 4`，小 BUG 复用 `bug`。

`ViewBinder.Sync` 每帧比对模型列表与 `Dictionary<int, EntityView>`，缺失则从 `PoolService` 取，多余则在 `Prune()` 回收。`EntityView` 是通用视图：主体 + 装饰（光柱 / 圆环 / 血条 / 名字标签），`ResetDecorations()` 必须把 `Body.enabled` 恢复为 true，否则被当作预警圈用过的视图回收后不再显示。

`JuiceService` 内置 **0.25s 顿帧节流**且只接受 `>= Elite` 优先级：后期每秒十几次击杀，逐次顿帧会退化成幻灯片。

`AudioService` 有 SFX / UI / BGM 三条代码式逻辑总线：24 个池化 SFX Source、两个 0.5 秒交叉淡化 BGM Source、一个低 SAN Loop Source。节流与并发按播放 key 统计，不能改回 `AudioClip.name`。四档掉落使用独立立体声 Clip，配置音量为 `0.36 / 0.52 / 1.00 / 0.90`；这组值是按素材实测响度反推的补偿而非阶梯本身，换掉任何一档素材都必须重算；SFX 总线留 3dB 余量，四档掉落以 `gainDb=3` 单项取回。主菜单/战斗/Boss/结算分别播放 `bgm_login/battle/boss/result`；战斗曲每天升调 0.04，Boss 三阶段调整低通、音高和音量。黄/橙奖励音不衰减，其余 SFX 与 BGM duck -6dB，恢复时回到当前 Boss 阶段基线。

`VoiceTest/` 保留 24 个交付源，不参与构建运行。`Tools/Audio/prepare_audio.py` 校验固定清单并生成 `Assets/_Game/Audio/Resources/Audio/{SFX,Drop,Loop,BGM}`；只裁派生版邮件死亡到 0.30 秒并淡出 0.05 秒。`OfficeHellAudioImporter` 固定 SFX 为 PCM/Decompress On Load，Drop 为 Vorbis 80/Decompress On Load/立体声，Loop 为 Vorbis 70/Compressed In Memory，BGM 为 Vorbis 65/Streaming。蓝色与紫色 Drop 源为 11025Hz，Unity Vorbis 运行时会报告 11000Hz；绿色与橙色是 48000Hz。Release 已嵌入这些资源，不依赖 `VoiceTest/`。

相机分两级：`CameraRig` 持有 z=-10 的跟随位置，`Camera` 作为子节点本地 z=0 只承载抖动偏移。混在一层会让抖动破坏正交投影距离。

---

## 10. UI 层

严格 MVC：`UIManager` 只订阅 `GameStateChanged` 并映射到面板开关，**状态机从不直接调用面板**。

`UIControllerBase` 生命周期：`UIInit`（构建，一次）→ `UIOpen` / `UIClose`（切换）→ `UITick`（每帧，走 unscaled）→ `UIDestroy`。

| Controller | 对应状态 |
| --- | --- |
| `UIMainMenuController` | `MainMenu` |
| `UIHudController` | 除 `MainMenu` 外全部 |
| `UICardController` | `LevelUp` |
| `UIOffWorkController` | `OffWork` |
| `UIResultController` | `Result` |
| `UIDebugController` | F1 切换，走 `OnGUI` 而非 UGUI |

**全部 UGUI 由 `UIFactory` 代码构建**，无 prefab。`CardsOffered` 事件独立存在的原因：同一次暂停内连升两级要重开卡手，仅靠状态变化不足以触发重绑。

HUD 布局：左上 SAN + 职级，中上时钟 + 星期，右上 KPI 条，左下 6 武器槽 + 技能，右下 3 防具槽，中下老板血条。
结算页是「离职证明 / 年终述职报告」，分段揭示 + 点击跳过 + KPI 爬条到 99% 卡住。

---

## 11. 数值与节奏基线

`OfficeHellSelfTest` 全周实测（god mode，完整六天）：

| 天收盘于 | 累计战斗秒 | 累计击杀 | KPI | 武器 | 防具 | 职级 |
| --- | --- | --- | --- | --- | --- | --- |
| 周一 | 40s | 46 | 5% | 6 | 0 | 高级专员 Lv.3 |
| 周二 | 90s | 153 | 16% | 6 | 0 | 主管 Lv.4 |
| 周三 | 150s | 284 | 31% | 6 | 1 | 经理 Lv.5 |
| 周四 | 220s | 480 | 52% | 6 | 2 | 总监 Lv.7 |
| 周五 | 300s | 803 | 88% | 6 | 2 | 高级总监 Lv.8 |
| 周六 | 420s | 990 | 99% | 6 | 2 | CEO Lv.9 |

开局固定装备绿色订书机、绿色键盘、绿色工牌各一件。首次升级 7.2s，结局 `ClearTimeout`，`hpScale` 终值 5.00，同屏峰值 50，累计发卡 8 张，25605 帧。

**这一跑是完全确定性的**：同一份配置连跑两次，帧数、击杀、峰值逐位一致，所以两个配置之间的差值可以直接对比，不需要取平均。**因此每次改完配置都要重跑这张表，而不是沿用上一次的数**——下面两条就是没重跑留下的。

> 四个盯的输出都是好的：首次升级 7.2s < 10s、职级在周六才满、`kpiTargetKills = 910` 周五收盘 88%、
> 周六才撞 99%，最后一天仍有推进感。同屏峰值 50 是**全周**峰值，由周五的 `concurrentMax`（102）决定，
> 不是周六在顶。
>
> **结局是 `ClearTimeout` 而不是 `Clear`，且与周六刷怪加量无关。** 把周六配置还原再跑一次，
> 结局同样是 `ClearTimeout`，加量只是抬高全天吞吐。
> god mode 跑满六天没能在 120 秒内打掉 9999×3，**`Clear` 这个结局目前在自测里够不到**；
> 要让它回来，该动的是老板血量或后期 DPS，不是周六的刷怪表。
>
> **防具从 3 件掉到 2 件，是保留手牌的直接代价。** 全周只发 8 张卡，3 级和 6 级两张被换成武器，
> 剩下 6 张里能掷出防具的期望不到一件。这一格空着是这次改动在表上唯一变差的地方，
> 要补该补的是掉落侧（`Loot.xml` 的 `armorShare`），不是把保留手牌收回去。
>
> **这条曲线的余量很薄，改动前先记住这一点。** 把两张保留手牌摘掉单独重跑，同一份代码只跑到
> 高级总监 Lv.8（441/467 exp，930 具）——差一级不是因为摘掉手牌本身减了经验，而是
> `expPower = 1.76` 拟合得刚好压在供给线上，掉落顺序被扰动几十具就能翻过去。
> 也就是说「周六升 CEO」这条断言现在靠的是精确的供给量，任何改刷怪表的改动都必须重跑第 3 道闸门。

### 三处刻意偏离策划案

**`expCoef = 12` / `expPower = 1.76`（策划案写 coef 10、power 1.55）**。策划案正文公式推出到 9 级累计 920 点，但表格写 1162——表格把当前级花费也算进了累计列，整体错位一级。按 920 实测，玩家在周五 2/3 处就满级，最后一天半没有任何升级。策划案散文明确写「玩家正好在周六升到 CEO」。

经验供给等于刷怪表付出多少，所以刷怪密度或武器覆盖范围改变后，这条曲线就得重拟。键盘落点范围减半并删除工卡击退后，固定六天自测的经验供给降到约 1608，`power` 因此由 1.80 微调为 **1.76**，到 9 级累计 1596。**动的是 `power` 不是 `coef`**：1 级花费恒等于 `coef`，而「首次升级 10 秒内」是这张表里唯一对玩家的承诺；`power` 只调整后段价格。`OfficeHellSelfTest` 的 `firstLevel <= 16` 与“周六升 CEO”断言共同守住两端节奏。

**`kpiTargetKills = 910`（策划案未给）**。取更高则可能永远停在 98，上限读不出是上限；取更低则会提前贴住 99，上限被读成满条。当前自测周五收盘 803 击杀 / 88%，周六才撞上 99%，节奏成立。910 对满周 990 具是 92%，落在「约 94%」这条经验值附近。

### 几何耦合关系

改动其中任何一个都必须同步检查其余三个，`ConfigValidator` 会拦下不一致：

```
Camera.orthographicSize 5.5  →  相机半展 9.78 × 5.5
SpawnBand semiX 14.5 / semiY 8.6  →  (9.78/14.5)² + (5.5/8.6)² = 0.86 <= 1  ✓ 完整包住相机
Arena halfWidth 17 / halfHeight 11.5  →  容纳 8.6 + outwardPush 1.4 + edgeMargin 1
```

---

## 12. 四道验证闸门

全部可命令行执行、全部返回退出码。**编辑器实例会锁项目，批处理模式必须跑在副本上**，否则 Unity 直接以 `UnityLockfile` 中止且退出码为空（不是崩溃）。

`E:\OfficeHellCI` 是一次性副本，删了只是下次重新导入。首次要连 `Packages` 与 `ProjectSettings` 一起拷，之后只同步 `Assets`。

```powershell
# 0. 同步副本（首次三个目录都要，之后只需第一行）
robocopy Assets ..\OfficeHellCI\Assets /MIR /NFL /NDL /NJH /NJS /NP
robocopy Packages ..\OfficeHellCI\Packages /MIR /NFL /NDL /NJH /NJS /NP
robocopy ProjectSettings ..\OfficeHellCI\ProjectSettings /MIR /NFL /NDL /NJH /NJS /NP

# 1. 离线编译，约 2 秒，不启动 Unity（需要 .NET SDK；本机没装时直接跳到第 2 道，
#    Unity 批处理会自己编译，编译错误出现在日志的 "error CS" 行）
dotnet build Tools/Verify/Verify.csproj

# 2. 分层纪律检查，约 8 秒
& "E:\unity2022\Unity2022.3.47f1\Data\Unity.exe" -batchmode -quit -nographics -projectPath "E:\OfficeHellCI" `
  -executeMethod OfficeHell.EditorTools.OfficeHellDisciplineCheck.RunBatch -logFile discipline.log

# 3. 无头逻辑自测，约 6 秒，覆盖 Config + Model + Systems，含完整六天
& "E:\unity2022\Unity2022.3.47f1\Data\Unity.exe" -batchmode -quit -nographics -projectPath "E:\OfficeHellCI" `
  -executeMethod OfficeHell.EditorTools.OfficeHellSelfTest.RunBatch -logFile selftest.log

# 4. 真 Play 模式压测，约 75 秒，覆盖 View + UI + Audio，统计所有 Error 与异常
& "E:\unity2022\Unity2022.3.47f1\Data\Unity.exe" -batchmode -nographics -projectPath "E:\OfficeHellCI" `
  -officehell-soak 70 -executeMethod OfficeHell.EditorTools.OfficeHellPlayModeCheck.RunBatch -logFile playmode.log
```

`&` 调起 Unity 后 `$LASTEXITCODE` **不会被写入**，读到的是上一条原生命令的值。要退出码就走
`Start-Process -Wait -PassThru` 取 `.ExitCode`，或者直接看日志尾部的 `[SelfTest] PASSED`。

**第 4 道必须单独跑，不要和第 2、3 道串在一条命令里。** 前三道是逻辑测试，跑多快都得同一个答案；
第 4 道数的是 70 秒真实时间里推进了多少逻辑时间，而单帧 `unscaledDelta` 被 clamp 到 0.1s，
机器被占住时逻辑时间会落后于墙上时间。上一个 Unity 实例还在退出就起下一个，足以让它只打完半天，
报出 `days closed 0` 的 `code 2`——读起来像流程卡死，实际只是慢。串跑失败先单独重跑一次再查代码。

菜单入口同样存在：`Office Hell/Run Discipline Check` / `Run Headless Self Test` / `Run Play Mode Check`。

`OfficeHellSelfTest` 断言的都是**会静默退化的规则**：24 个 AudioClip 与四类导入设置完整、四档 Drop 保持立体声/原采样率/独立引用/音量阶梯、邮件死亡 0.30 秒、BUG 分裂 0.48 秒、键盘砸击不超过 0.50 秒（它是全场最密的一个键，`maxConcurrent=3` 挡不住更长的尾巴）、角色帧数完整、环形采样面积均匀（写成 `Lerp(minR, maxR, rand)` 会让内圈偏密）、环带最近点不侵入相机、时钟单调且按 30 分钟对齐、光环同通道取最大值、一弱一强接触伤害 6→12、保底触发后计时器归零、BUG 分裂子代 0 经验、自动装备三条规则、三类武器各自产生效果、老板破条进阶段且**破条波数量等于 `phaseAdds`、种类多于一种**（名单塌回单一 id 只会让画面变单调，配置本身看不出来）、首次升级 10 秒内、职级在最后一天满、KPI 撞到 99、**保留手牌是三把互不相同的同档武器**（同一等级只发一次，跳级也照发）、**橙色防具三个部位各一件后改发橙武器**（低档不受影响）、**咖啡持续段刷新而不叠加**、重开后实体与计数与属性与被动与装备槽与橙色防具占位全清、卡面模板全部填完且数值等于绿色档乘品质系数、技能卡的 `passive` 都映射得到（映射不到的卡会被当成已拥有而**静默消失**）、**卡池里每一张卡都在 `GameIconCatalog` 里有图，且 22 个键各指一张互不相同的素材**（漏一行只是让那张卡戴着三字母缩写上场，两行指同一张图则是两张卡长得一样，两种都不报错）、数值卡两档叠加而被动两档替换、四档 `rankName` 齐全且互不相同、装备名按 `词缀 + 品质 + 底板` 排列（橙装带词缀、绿装恰好等于品质加底板）、装备卡发下去的底板就是卡面写的那个、耳机护盾会自然过期且免死不随天数回满、格子衫第五次受击整下免掉、拖鞋橙色闪避会位移、**拖鞋紫色咖啡渍能减速又能掉血、且下一帧不会再收一次费**（节流写在渍上而不是敌人上，在测试里读起来一样对，在实战里是六倍收费）、**画饼撒出的圈数量对、每个都在 `pieInner`-`pieOuter` 环带内、每个都真的掉血、减速照旧生效**（环带按到老板的距离量，老板离玩家六格，所以这一条同时挡住「把圈改回标在玩家脚下」）、**老板咽气后 2 秒收工且结局是 `Clear`**（同一测试把玩家标成已死再跑，胜利不许被改判成 `Fail`）。

**修改配置数值后必须跑第 3 道**，它是唯一能在 6 秒内发现节奏被改坏的手段。

---

## 13. 改动指引

| 任务 | 涉及文件 |
| --- | --- |
| 调数值 / 节奏 | 只改 `StreamingAssets/Config/*.xml`，F5 热重载，跑自测 |
| 更新角色 / 地图 / Logo | 更新 `testAssets/` 源文件并运行 `Tools/Art/prepare_art.py`；`OfficeHellArtImporter` 统一导入设置，随后跑自测与 PlayMode |
| 更新正式音频 | 更新 `VoiceTest/` 已转换文件并运行 `Tools/Audio/prepare_audio.py`；`OfficeHellAudioImporter` 统一导入设置，随后跑自测与 PlayMode |
| 新增敌人 | `Enemies.xml` + `Views.xml`；若需新行为再加 `EnemyBehaviors.cs` 并在 `EnemyBehaviorRegistry` 注册 |
| 新增武器 | `Weapons.xml` 四档 + `Views.xml`。**不要加第四个 `WeaponKind`**，品质差异应表达为现有三条路径的参数 |
| 新增升级卡 | `Cards.xml` 只承载数值卡与技能卡，`value` 写**绿色档**、`desc` 必须带 `{v}`；技能卡需在 `SlackPassive` 加位、在 `PlayerModel.PassiveIndex` 加索引、在 `CardSystem.PassiveOf` 映射。**还要在 `GameIconCatalog.AssetNames` 加一行**并把素材放进 `UI/Resources/Icon/card/`，否则卡面上是三字母缩写 |
| 新增掉落词缀 | `Loot.xml` 的 `Affix`，`StatKey` 已有即可 |
| 新增打击感 / 音效 | 订阅 `EventID`，改 `JuiceService` / `AudioService` / `Audio.xml`。不要在系统里插播放调用 |
| 新增 UI 面板 | 继承 `UIControllerBase`，在 `UIManager.Build` 构建、`ApplyState` 映射状态 |
| 改流程 | `GameFlowFsm` 的状态与 `Enter()`；注意 `GameClock.Scale` 只有 `Battle` 是 1 |

### 反模式清单

- 在 `Systems/` 或 `Model/` 里读 `Time.*` —— 纪律检查会拦
- 写 `Time.timeScale` —— 顿帧会连带冻住 UI 与特效
- 在 `Compact()` 之外移除实体 —— 会让本帧已发出的网格下标失效
- 用 `+=` 累加光环 —— 五个 PPT 会把玩家减速到 0
- 直接写 `EnemyModel.Knockback` —— 绕过内置 CD，六个槽位的击退会叠起来把怪永久顶住；一律走 `TryKnockback`，参数是**距离**不是冲量
- 给系统之间加直接引用 —— 应走 `EventBus` 或 `GameContext`
- 在表现层写死预警圈颜色 —— 四种 `v_warn_*` 的色相是配置里唯一区分"这个圈会打你"和"这个圈只是仪式"的手段，一律取 `ViewDef.Color`。精英登场圈 `Damage` 恒为 0，画成红色等于让玩家躲一个不存在的攻击
- 表现层按内容 id 分派（`DefId == "bug"`）—— 按行为名或 def 字段判定，否则新增同类敌人时音效与特效会静默漏掉
- 用 `EvtArg` 的整数字段复用多重语义 —— 传模型引用放 `O0`
- 为品质差异新开代码路径 —— 品质只应改参数
- 在 `SkillSystem` 里按 `SlackPassive` 位判定后写死数值 —— 位标记只回答"有没有"，量一律走 `PassiveValue`，否则被动的四档品质在代码里根本不存在
- 给卡片 id 加品质后缀 —— 一手内的去重按 id 走，加了后缀就会把同一张卡的绿色与橙色并排发出来
- 掉落改成全自动吸取 —— 走过去捡黄橙的那两秒是期待感本身
- 在本机做 Development 构建 —— 定制编辑器的 dev player 版本戳不一致，必崩在 `globalgamemanagers`，早于任何游戏代码。只能用 Release
