# 办公室地狱 · 工程总览

本文件面向**接手本工程的 AI 代理**，目标是读完即可直接改代码而不需要先做一轮全库探索。
`README.md` 面向人类使用者（如何打开、如何构建、踩过的坑），部分章节写于旧策划案时期，**数值与模块清单以本文件为准**。

---

## 0. 事实卡

| 项 | 值 |
| --- | --- |
| 引擎 | Unity **2022.3.47f1**，定制重打包版，`Unity.exe` 在 `E:\Unity2022.3.47f1\Data\` 而非 `Editor\` |
| 渲染管线 | **Built-in RP**（URP 因 `com.unity.burst` 只有 1.3.4 凑不到 1.8.9 而放弃，见 README「包依赖」一节） |
| 工程路径 | `E:\OfficeHell`，与 Free Fire 主工程完全隔离，不在 Perforce 工作区内 |
| 规模 | 自有代码约 **13k 行** / 73+ 个 cs 文件，配置 8 个 XML |
| 资产 | **零 prefab、零自定义材质、零 ScriptableObject**；美术位于 `Assets/_Game/Art/`，23 个正式音频位于 `Assets/_Game/Audio/` |
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

**老板**：三条 **9999** 血条，三阶段。阶段转换 2 秒无敌 + 召 20 只小怪。
`ignoreScaling="true"`，逐天成长公式不得触碰它的数值。**代价是伤害也一起被冻住**：这个标记只为让 9999 照字面读出来，
但它同时让老板错过了 `dmgPerDay`，而周六的 `dmgScale` 是 2.6，一度出现 Deadline 打 31 而老板只打 20 的局面。
老板只在周六出现，所以 `contactDamage` 直接按周六的值手写（当前 52 = 20 × 2.6）；**改 `dmgPerDay` 必须回来重算这个数**。

老板的远程是 **KPI 甩锅**：在玩家周围标地面预警圈，0.8 秒后结算，不是飞行物。
飞行文件夹打不中任何会走路的玩家——瞄的是投掷瞬间的坐标，速度 8 对玩家 4.5，跨越 6 格要飞 0.75 秒，
而那段时间玩家已经走出 3.5 格、爆炸半径只有 2.2，任意方向都是躲，包括朝老板跑。

---

## 2. 分层与依赖方向

```
Assets/_Game/
  Art/        地图、品牌、画饼、42 张角色动画帧
  Audio/      14 个 SFX、4 个立体声 Drop、低 SAN Loop、4 段 BGM（Resources/Audio）
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
              ArtCatalog  JuiceService  AudioService  DamageTextService  WorkClockView
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

`EventID` 全量（`Core/EventID.cs`）：配置重载、状态与流程（`GameStateChanged` `RunStarted` `RunEnded` `DayStarted` `DayCleared`）、敌人（`EnemySpawned` `EnemyDamaged` `EnemyKilled`）、玩家（`PlayerDamaged` `PlayerDodged` `PlayerHealed` `PlayerShielded` `PlayerShieldBroken` `PlayerDied` `PlayerLevelUp` `PlayerRankUp`）、掉落（`LootDropped` `LootPicked`）、装备与攻击（`WeaponEquipped` `ArmorEquipped` `EquipDeclined` `WeaponFired` `SlamLanded` `SelectAll` `OrbitRebuilt` `SkillCast` `CoffeeDrunk`）、老板（`BossSpawned` `BossTelegraph` `BossPhaseChanged` `BossClockedOut`）、卡片（`CardsOffered` `CardPicked`）。

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
| `Cards.xml` | 升级三选一权重、按天装备品质、卡池 | `CardPoolDef` `CardDef` |
| `Views.xml` | 形象：形状 × 颜色 × 尺寸 | `ViewDef` |
| `Audio.xml` | 正式 Clip、逻辑总线、节流/并发、Synth 回退、BGM 淡化与 ducking | `AudioDef` `SfxDef` `BgmDef` |

### 解析纪律

- **解析永不抛异常**。坏行降级为默认值继续跑，问题聚合成一份报告打到 Console。XML 丢掉了编译期类型检查，所以运行不能被一个错字打断。
- `behaviorParam` 走 `KvBag`（`key=value;key=value`），避免为每个行为的参数各开一个 Def 类。
- `WeaponTierDef` 未声明的字段**继承下一档**，所以 XML 只写增量，「白 + 三次改动」保持可读。
- 热重载后 def 是新实例，`RunModel.RebindDefs` 按 id 重新解析每个存活实体、每个武器槽、每个防具槽以及当天的 `DayDef`，否则旧数值会继续生效。

### ConfigValidator 守的规则

这些是**会静默退化**的约束，不是能一眼看出来的错误：

- 引用完整性：`enemyId` / `viewId` / `sfxId` / 卡片 `passive` 是否存在
- 敌人速度必须 < 玩家 4.5：压迫感只能来自被包围，不能来自被追上
- **椭圆环带必须完整包住相机矩形**：`(halfW/semiX)² + (halfH/semiY)² <= 1`。逐轴比较是错的，会让敌人在四个对角线角落凭空出现
- 环带最大触及范围（半轴 + 外推 + 边距）必须落在场地内，否则上下扇区每次放置都失败，方向权重会悄悄失效
- **刷怪窗口必须铺满整天**：非 `totalSpawn` 覆盖的天，最晚的 `Spawner.To` 不得早于 `duration`，否则收工前那段没有任何敌人到达
- **`knockbackCd > 0`**：归零等于恢复每次命中都击退，六个槽位一起打足以把怪永久顶在身前
- **`slowSeconds` 必须短于武器攻击间隔 `1/rate`**：否则下一次命中在减速失效前就续上，减速不再是减速而是敌人的真实速度
- `selectAllEvery > 0` 时 `selectAllPct` 不得为 0（Ctrl+A 一击不掉血）、`selectAllRadius` 必须大于 `blastRadius`（否则大招覆盖还不如它替掉的那一击）、`selectAllSharedCd` 必须 > 0（否则六个槽位同时扫）
- `orange` 权重必须为 0：橙装只走保底与脚本通道
- 武器档位不得回退（如高品质 `projCount` 比低品质小）
- `weaponShare + armorShare == 100`；防具必须覆盖三个槽位
- `RankNames` 数量 == `MaxLevel`；`KpiCap == 99`；`MaxSan == 99`；`stepPickupRadius < pickupRadius`
- 天索引唯一、`Fixed.atSecond` 不超过天长度、`budgetPct` 合计为 100、权重合计非 0

---

## 7. 数据模型

### RunModel

一局的全部状态。**重开 = 清空这个对象 + 回收视图池**，从不重载场景（`GameApp.StartRun` 0 帧重开）。

六类实体各有 `List` + `Stack` 池，`RentXxx()` 取用，`Compact()` 归还：`Enemies` `Projectiles` `Loots` `Slams` `Telegraphs` `OrbitCards`（轨道卡不入池，随武器槽存在）。

关键字段：`DayIndex` `DayElapsed` `Day` `HpScale` `DmgScale` `SpawnDebt`（同屏上限造成的欠账，只延后不丢弃）、`KillsByType`（按 def id 计数，供结算页生成「处理邮件 328 封」）、`SecondsSinceLastLegendary`（保底计时器，只在战斗中累加）、`Ending` `BossBarsLeft`。

### PlayerModel

6 武器槽 + 3 防具槽。属性走 `StatSheet`（base + `StatModifier` 列表，按 sourceId 增删，装备摘下即撤销）。

**光环用固定长度数组 `_aura[3]` 表达，每帧头部清零，系统只允许用 `Max` 写入，永不 `+=`。** 所以五个 PPT 叠在一起仍然只减速 25%。三个通道：`MoveSlow` `AttackSlow` `EnemyHaste`。

派生值不进 `StatSheet`，而是即时计算，因为它们依赖当前 SAN 或当前时间：`EffectiveMoveSpeed(now)` `EffectiveHaste(now)` `EffectiveDef()` `MagnetRadius` `ImmuneToControl`。

`PendingLevelUps` 是队列而非布尔：一帧内连升两级不能吞掉一次选卡。

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
| 工资 | `finalSalary * servedSeconds / totalSeconds`，满周恰好 9996 |

`Base99 = 99f` 既是主题也是 DEF / HASTE 的衰减常数。

---

## 8. 系统层

| 系统 | 职责 | 扩展点 |
| --- | --- | --- |
| `GameFlowFsm` | 6 状态机 + `GameClock.Scale` 闸门 | 状态：`MainMenu` `DayStart` `Battle` `LevelUp` `OffWork` `Result` |
| `SpawnSystem` | 按天预算刷怪、同屏上限欠账、`Fixed` 定时刷、精英登场 | 密度公式 `ceil(density * duration)`，`totalSpawn` 仅周六覆盖。预算按**累计进度表**释放而非按固定组量抽干：`interval` / `groupSize` 只决定节奏，窗口一定铺满到收工。`ramp` 是窗口末尾相对开头的到达倍率（默认 2），只改分布不改总量 |
| `SpawnBand` | 椭圆环带取点：24 扇区轮转 + 左右权重 + 外推 + 间距校验 + 边界回退 | 取点失败**回退到最近可用扇区而非跳过**：贴墙让怪停刷会在一局内被玩家发现并利用 |
| `EnemyAiSystem` | 直线追击 + 委派 `IEnemyBehavior` | `EnemyBehaviorRegistry` 字符串→实例映射 |
| `EnemyBehaviors` | 7 种行为 | `SuicideOnContact` `SplitOnDeath` `AuraMoveSlow` `AuraAttackSlow` `AuraHaste` `RangedKeepDistance` `BossSkills` |
| `WeaponSystem` | 6 槽 CD 与相位错开，委派 `IWeaponBehavior` | 无目标时**不消耗 CD**，0.12s 后重试，否则空场地白烧攻速 |
| `WeaponBehaviors` | 2 种发射行为 | `ProjectileLauncher` `GroundAoe`。**`Orbit` 不在此处**：轨道卡随装备常驻，由 `OrbitSystem` 独占，`Get(Orbit)` 返回 null |
| `SlamSystem` | `GroundAoe` 落点延迟结算 | 所有落点走同一个圆判定，**Ctrl+A 不再是全屏**：以玩家为心扫 `selectAllRadius`（6.0，相机半展 9.78×5.5，纵向盖满、横向留活口），按 `selectAllPct` 打折且不带击退与减速。全屏满伤 + 全屏控制是三个效果收一份钱，会把场地清空到其余五个槽位没有目标。`selectAllSharedCd` 是**跨槽位共享**的，存在 `PlayerModel.SelectAllReadyAt`：单把键盘约 7.7s 才轮到一次，永远碰不到这个 CD；六把同时到点会在两秒内扫六次，那就不是大招而是环境光。被挡下的槽位**不推进 `AttackCount`**，下一击重试而不是作废整轮 |
| `OrbitSystem` | 环绕卡位置、同目标 CD、橙品质 tether | |
| `TelegraphSystem` | 地面预警圈到期后结算伤害、生成实体，`SummonDrop` 携带保证掉落 | 老板的三种落地攻击（甩锅 / 开会 / 落雨）全部走这里，敌方投射物只剩周报一种，且只做接触判定 |
| `CombatSystem` | 投射物命中、接触伤害、无敌帧、闪避、护盾 | 无敌帧触发帧取所有接触敌人的 **max(contactDamage)** |
| `LootSystem` | 咖啡 / 装备掉落、词缀 roll、保底、磁吸与踩取、自动装备 | |
| `ProgressionSystem` | 经验、等级、职级变更事件 | |
| `CardSystem` | 升级三选一：数值 45% / 装备 30% / 技能 25% | 装备卡**直接塞给玩家**而不是掉在地上：唯一的决策不能因为没走过去而丢失。数值卡取走后**永久离池**，否则六次升级可能全是「攻击力 +6」。装备卡**不在 XML 里**，由 `CardSystem` 按 `EquipQualityByDay` 与武器表合成，描述走 `TierBlurb` |
| `SkillSystem` | 摸鱼：无敌 + 回血 + 推开，5 种被动强化 | |
| `ArmorSystem` | 护盾周期、按品质解锁的防具效果 | |
| `MovementSystem` `InputSystem` `CameraSystem` `ProjectileSystem` `ProjectileFactory` | 单一职责，各 25-75 行 | |

三个注册表是**新增内容的标准入口**：`EnemyBehaviorRegistry`（字符串键）、`WeaponBehaviors`（按 `WeaponKind` 查）、`CardSystem` 的卡片种类分派。

---

## 9. 表现层

角色与场地已有首批美术，但仍保持**零 prefab、空场景、代码构建表现**：

- `ArtCatalog` 从 `Resources/OfficeHellArt/` 缓存地图、Logo、画饼与 9 组角色帧；缺资源时回退 `PrimitiveFactory`
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

`VoiceTest/` 保留 23 个交付源，不参与构建运行。`Tools/Audio/prepare_audio.py` 校验固定清单并生成 `Assets/_Game/Audio/Resources/Audio/{SFX,Drop,Loop,BGM}`；只裁派生版邮件死亡到 0.30 秒并淡出 0.05 秒。`OfficeHellAudioImporter` 固定 SFX 为 PCM/Decompress On Load，Drop 为 Vorbis 80/Decompress On Load/立体声，Loop 为 Vorbis 70/Compressed In Memory，BGM 为 Vorbis 65/Streaming。蓝色与黄色 Drop 源为 11025Hz，Unity Vorbis 运行时会报告 11000Hz；白色与橙色是 48000Hz。Release 已嵌入这些资源，不依赖 `VoiceTest/`。

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
| 周一 | 40s | 33 | 5% | 2 | 2 | 高级专员 Lv.3 |
| 周二 | 90s | 115 | 17% | 4 | 2 | 主管 Lv.4 |
| 周三 | 150s | 226 | 35% | 6 | 3 | 经理 Lv.5 |
| 周四 | 220s | 379 | 59% | 6 | 3 | 总监 Lv.7 |
| 周五 | 300s | 594 | 92% | 6 | 3 | 高级总监 Lv.8 |
| 周六 | 420s | 681 | 99% | 6 | 3 | CEO Lv.9 |

首次升级 7.9s，结局 `Clear`，`hpScale` 终值 5.00，同屏峰值 30，累计发卡 8 张。

> **这张表已作废两轮，尚未重测。** 第一轮是刷怪改为进度表释放（预算不再在 55% 处抽干，窗口铺满全天），
> 第二轮是刷怪总量翻倍：475 → 923，同时每天的怪物种类拓宽。**下面所有派生数字都是推算值，不是实测值。**
>
> 翻倍不是均匀的。周一 1.00 → 1.70、周五 2.03 → 4.30，因为接触伤害按无敌帧封顶而不是按敌人数封顶，
> 二十只和四十只在被围住之后收同样的 SAN，先崩的永远是只有一把武器的那天。周六 `totalSpawn` 保持 20 不动：
> 武器自动锁最近目标，多加 40 只小怪等于六个槽位整场在打邮件，老板站着不掉血。
>
> **跑第 3 道闸门后按实测值订正本表与下面两个锚点。**四个要盯的输出：`first level up` 是否仍 < 10s、
> `last rank on day` 是否仍为 6、`kpi peaked` 是否到 99、`peak alive` 是否顶到了 `concurrentMax`
> （顶到了说明欠账在吞增量，得继续抬 `concurrentMax` 而不是抬 `density`）。

### 三处刻意偏离策划案

**`expCoef = 12` / `expPower = 1.95`（策划案写 coef 10、power 1.55）**。策划案正文公式推出到 9 级累计 920 点，但表格写 1162——表格把当前级花费也算进了累计列，整体错位一级。按 920 实测，玩家在周五 2/3 处就满级，最后一天半没有任何升级。策划案散文明确写「玩家正好在周六升到 CEO」。

刷怪翻倍后经验供给同步翻倍（约 1230 → 2480），曲线必须跟着翻，否则周三就满级。**抬的是 `power` 不是 `coef`**：1 级花费恒等于 `coef`，而「首次升级 10 秒内」是这张表里唯一对玩家的承诺，翻 `coef` 等于把它从 6 只邮件变成 12 只。`power` 1.55 → 1.95 让 1 级停在 12 点不动，把全部增量压到 5-9 级——也正是多出来的经验实际到账的地方。累计 1103 → 约 2240，每一级晋升落在的天数与翻倍前一致。`OfficeHellSelfTest` 有一条 `firstLevel <= 16` 的硬断言专门守这个，动 `coef` 会直接红。

**`kpiTargetKills = 1120`（策划案未给）**。翻倍前是 640 对满周实测 681 具。翻倍后预计约 1200 具，取其 94%。取更高则永远停在 98，上限读不出是上限；取更低则周四就贴住 99，上限会被读成满条。**这个数按自测的 `total kills` × 0.94 订正。**

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

`E:\OfficeHellCI` 是一次性副本（11 MB），删了只是下次重新导入。

```powershell
# 0. 同步副本
robocopy Assets ..\OfficeHellCI\Assets /MIR /NFL /NDL /NJH /NJS /NP

# 1. 离线编译，约 2 秒，不启动 Unity
dotnet build Tools/Verify/Verify.csproj

# 2. 分层纪律检查，约 8 秒
& "E:\Unity2022.3.47f1\Data\Unity.exe" -batchmode -quit -nographics -projectPath "E:\OfficeHellCI" `
  -executeMethod OfficeHell.EditorTools.OfficeHellDisciplineCheck.RunBatch -logFile discipline.log

# 3. 无头逻辑自测，约 6 秒，覆盖 Config + Model + Systems，含完整六天
& "E:\Unity2022.3.47f1\Data\Unity.exe" -batchmode -quit -nographics -projectPath "E:\OfficeHellCI" `
  -executeMethod OfficeHell.EditorTools.OfficeHellSelfTest.RunBatch -logFile selftest.log

# 4. 真 Play 模式压测，约 75 秒，覆盖 View + UI + Audio，统计所有 Error 与异常
& "E:\Unity2022.3.47f1\Data\Unity.exe" -batchmode -nographics -projectPath "E:\OfficeHellCI" `
  -officehell-soak 70 -executeMethod OfficeHell.EditorTools.OfficeHellPlayModeCheck.RunBatch -logFile playmode.log
```

菜单入口同样存在：`Office Hell/Run Discipline Check` / `Run Headless Self Test` / `Run Play Mode Check`。

`OfficeHellSelfTest` 断言的都是**会静默退化的规则**：23 个 AudioClip 与四类导入设置完整、四档 Drop 保持立体声/原采样率/独立引用/音量阶梯、邮件死亡 0.30 秒、BUG 分裂 0.48 秒、角色帧数完整、环形采样面积均匀（写成 `Lerp(minR, maxR, rand)` 会让内圈偏密）、环带最近点不侵入相机、时钟单调且按 30 分钟对齐、光环同通道取最大值、一弱一强接触伤害 6→12、保底触发后计时器归零、BUG 分裂子代 0 经验、自动装备三条规则、三类武器各自产生效果、老板破条进阶段、首次升级 10 秒内、职级在最后一天满、KPI 撞到 99、重开后实体与计数与属性与被动与装备槽全清。

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
| 新增升级卡 | `Cards.xml` 只承载数值卡与技能卡；技能卡需在 `SlackPassive` 加位并在 `CardSystem.PassiveOf` 映射 |
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
- 掉落改成全自动吸取 —— 走过去捡黄橙的那两秒是期待感本身
- 在本机做 Development 构建 —— 定制编辑器的 dev player 版本戳不一致，必崩在 `globalgamemanagers`，早于任何游戏代码。只能用 Release
