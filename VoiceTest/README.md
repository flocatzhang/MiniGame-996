# 音效接入说明

《996》MiniGame 音频资产目录。设计依据见 wiki：[MiniGame-Project 996](https://wiki.garena.com/pages/viewpage.action?pageId=323135980) 第十四节。

---

## 零、先读这一段

当前目录里的 19 个文件**没有一个能直接拖进 Unity 用**，原因有两个，都必须先处理：

1. **10 个 `.mp4` 文件里带着 H.264 视频轨。** Unity 会把它们识别成 `VideoClip` 而不是 `AudioClip`，直接拖进去连 AudioSource 都挂不上。这不是设置问题，是容器问题，**必须转格式**。
2. **9 个 `.mp3` 虽然 Unity 能读，但不该用。** MP3 编码器会在音频头尾各塞几十毫秒静音（编码器 delay/padding），短音效听起来会"慢半拍"，loop 音效会有明显的接缝。

**这两件事已经处理完了。** `convert.ps1` 跑过一遍，19 个文件全部转换、去静音、归一化，产出在 `converted/`。

**程序只用 `converted/` 里的文件，根目录的原始文件是存档，不要删也不要接。** 新素材放进根目录后重跑一次 `convert.ps1` 即可，脚本是幂等的。

转换完还剩三件事：两个文件的时长需要人耳确认（第二节末尾），还缺 4 个 P0 音效（第六节）。

---

## 一、文件清单与对应关系

「目标时长」来自设计文档第十四节，是拿到素材后应该裁到的长度。

### 音效（15 个）

| 现文件名 | 对应设计条目 | 目标时长 | 现时长 | 转换后文件名 |
| --- | --- | --- | --- | --- |
| `Stapler_Launch.mp4` | 订书机发射 | 0.15s | <1s | `sfx_weapon_stapler_fire.wav` |
| `Stapler_Hit.mp4` | 订书针命中 | 0.12s | <1s | `sfx_weapon_stapler_hit.wav` |
| `email_death.mp4` | 邮件死亡（兼通用死亡音） | 0.25s | ~1s | `sfx_enemy_email_death.wav` |
| `BUG_slipt.mp3` | BUG 分裂 | 0.20s | <1s | `sfx_enemy_bug_split.wav` |
| `take_hit.mp3` | 玩家受击 | 0.30s | <1s | `sfx_player_hurt.wav` |
| `role_death.mp3` | 玩家死亡 | 1.50s | ~1s | `sfx_player_death.wav` |
| `low_health_loop.mp3` | 低 SAN 心跳 loop | 3.0s loop | ~3s | `sfx_player_lowsan_loop.wav` |
| `Drop.mp4` | **升级三选一卡片登场** | ~1.0s | ~1s | `sfx_growth_card_appear.wav` |
| `PickUp.mp3` | 拾取成功 | 0.15s | <1s | `sfx_drop_pickup.wav` |
| `ConvertedXP.mp3` | 折算经验（挂二手平台 +3 经验） | 0.40s | <1s | `sfx_drop_convert_xp.wav` |
| `coffee_drop.mp3` | 咖啡掉落 | 0.50s | <1s | `sfx_coffee_drop.wav` |
| `coffee_drink.mp3` | 咖啡拾取 | 0.90s | ~1s | `sfx_coffee_drink.wav` |
| `clock_in.mp3` | 打卡上班（登录页主按钮） | 0.50s | <1s | `sfx_ui_clockin.wav` |
| `good_night.mp4` | 下班铃 / 下班过场 | 1.50s | ~2s | `sfx_flow_dayend.wav` |
| `upgrade_BGM.mp4` | **升职音效**（不是 BGM，见下） | 1.20s | ~1s | `sfx_growth_levelup.wav` |

### BGM（4 段）

| 现文件名 | 用途 | 目标时长 | 现时长 | 转换后文件名 |
| --- | --- | --- | --- | --- |
| `loading_BGM.mp4` | 登录页 | 40s loop | ~29s | `bgm_login.ogg` |
| `flight_BGM.mp4` | 战斗（六天共用，靠变速推情绪） | 60s loop | ~15s | `bgm_battle.ogg` |
| `BOSS_BGM.mp4` | 周六 BOSS 战 | 60s loop | ~14s | `bgm_boss.ogg` |
| `Payout_BGM.mp4` | 结算页 | 30s | ~19s | `bgm_result.ogg` |

### 掉落音效（子目录 `掉落音效/`，4 个，原样使用）

这四个是定稿素材，**脚本原样拷贝，不去静音、不归一化、不重采样**，拷完做过 MD5 校验，与源文件逐字节一致。

| 源文件 | 档位 | 时长 | 声道 | 采样率 | 响度 | 真峰值 |
| --- | --- | --- | --- | --- | --- | --- |
| `drop_white.wav` | 白 | 1.676s | 立体声 | 48000 | −12.2 LUFS | +0.09 dBTP |
| `drop_blue.wav` | 蓝 | 2.259s | 立体声 | 48000 | −8.3 LUFS | +0.39 dBTP |
| `drop_yellow.wav` | 黄 | 2.276s | 立体声 | **11025** | −12.2 LUFS | +0.33 dBTP |
| `drop_orange.wav` | 橙 | 3.305s | 立体声 | 48000 | −8.0 LUFS | +0.03 dBTP |

时长比设计文档里写的目标值（0.1 / 0.4 / 0.7 / 1.0 秒）长得多，但**这不构成问题**：一局只掉 29 件装备，平均 14 秒才响一次，重叠的概率极低。当初定那个目标是按高频音的标准定的，对掉落这种低频高价值音来说，长一点反而更有分量。**这四条时长可以直接用。**

有两件事需要知道，都不用改文件：

**响度不是四档递进，是两两成对。** 白和黄都是 −12.2 LUFS，蓝和橙都是 −8.1 左右。也就是说**蓝色现在和橙色一样响，黄色和白色一样轻**——直接接进去，玩家听到的层级是错的。解决办法不在文件上，在 Unity 的 `AudioLibrary.volume` 字段里，具体数值见第四节。

**黄色档是 11025 Hz。** 另外三条都是 48000。11 kHz 采样率意味着高频只到 5.5 kHz，听感会明显比另外三条闷。它偏偏是四档里的第二好档位，本该比白蓝更亮更有质感。这一条改不了文件就只能忍，**如果后面还有一次重新生成的机会，优先换它**。

### 三处命名要在转换时一并修掉

改名这件事**要么现在改，要么永远别改**。转换本来就要重新生成一批文件，改名是顺手的；等程序把引用都连上之后再改，就是一次全局搜索替换加一轮回归测试。

| 现名 | 问题 | 改成 |
| --- | --- | --- |
| `upgrade_BGM.mp4` | 它只有 1 秒，是升职音效不是 BGM。留着这个名字，程序会照着 BGM 的方式接（Streaming 加载、挂 BGM 总线），接完发现不对再返工 | `sfx_growth_levelup` |
| `flight_BGM.mp4` | 拼写应为 fight。flight 是"飞行" | `bgm_battle` |
| `BUG_slipt.mp3` | 拼写应为 split | `sfx_enemy_bug_split` |

---

## 二、第一步：转格式（已完成）

### 怎么跑

需要 ffmpeg（已装，`winget install --id=Gyan.FFmpeg -e`）。在本目录下：

```powershell
powershell -ExecutionPolicy Bypass -File convert.ps1
```

**改完 `convert.ps1` 记得存成 UTF-8 with BOM。** Windows PowerShell 5.1 会把无 BOM 的 `.ps1` 按 GBK 读，脚本里的中文注释会把语法解析打乱，报一堆莫名其妙的 UnexpectedToken。

脚本是幂等的，重跑一次全量覆盖 `converted/`，原始文件只读不动。

### 两条处理路径

脚本里有两组文件走不同的路：

- **`$sfx` / `$bgm`**：需要加工的原始素材，走完整流水线（下面四步）
- **`$passthrough`**：已经定稿的成品，**只做拷贝，一个字节都不改**。目前是 `掉落音效/` 下的四条

以后再有"这个文件别动，直接用"的素材，加到 `$passthrough` 里就行。**不要手工拷进 `converted/`**——脚本每次跑都会先清空那个目录，手工放的东西下次重跑就没了。

### 脚本做了四件事

1. **`-vn` 丢掉视频轨。** 整个脚本最关键的一个参数，少了它转出来的东西 Unity 照样认成视频
2. **去首尾静音**（`silenceremove` + `areverse` 正反各切一次）
3. **音效峰值归一化到 −6 dBFS**，BGM 响度归一化到 −16 LUFS
4. 汇总打印时长、声道、采样率、峰值，直接对着这张表验收

第 2 步对手感的影响比任何音量调整都大——AI 生成的音效经常在开头留几十毫秒静音，单独试听完全察觉不到，但在游戏里就是"按下去过一会儿才响"。订书机每秒响 1.2 次，40 毫秒的延迟足以让整个射击手感发黏。

第 3 步用的是两趟：先解码去静音出临时 wav，测出实际峰值，再按差值补增益写最终文件。BGM 因此只经过**一次** ogg 编码，没有二次压缩损失。

### 转换结果

`converted/` 里现在有 **23 个文件**：19 个加工产出（下表）加 4 条原样拷贝的掉落音（规格见第一节）。

| 文件 | 时长 | 峰值 | 文件 | 时长 | 峰值 |
| --- | --- | --- | --- | --- | --- |
| `sfx_weapon_stapler_fire.wav` | 0.088s | −6 dB | `sfx_coffee_drop.wav` | 0.188s | −6 dB |
| `sfx_weapon_stapler_hit.wav` | 0.093s | −6 dB | `sfx_coffee_drink.wav` | 0.315s | −6 dB |
| `sfx_enemy_email_death.wav` | 0.704s | −6 dB | `sfx_ui_clockin.wav` | 0.480s | −6 dB |
| `sfx_enemy_bug_split.wav` | 0.480s | −6 dB | `sfx_flow_dayend.wav` | 2.015s | −6 dB |
| `sfx_player_hurt.wav` | 0.480s | −6 dB | `sfx_growth_levelup.wav` | 1.500s | −6 dB |
| `sfx_player_death.wav` | 1.480s | −6 dB | `bgm_login.ogg` | 29.3s | −1.4 dB |
| `sfx_player_lowsan_loop.wav` | 3.080s | −6 dB | `bgm_battle.ogg` | 15.2s | −2.9 dB |
| `sfx_growth_card_appear.wav` | 1.025s | −6 dB | `bgm_boss.ogg` | 14.4s | −3.5 dB |
| `sfx_drop_pickup.wav` | 0.235s | −6 dB | `bgm_result.ogg` | 20.0s | −1.6 dB |
| `sfx_drop_convert_xp.wav` | 0.313s | −6 dB | | | |

归一化之前的原始素材问题很典型，记在这里备查：`BOSS_BGM` 和 `take_hit` 峰值都顶在 **0 dB 已经削波**，而 `PickUp` 只有 **−18.8 dB**。最响和最轻差了 18 分贝，如果直接接进 Unity，结果就是订书机震耳朵、拾取声完全听不见——**而这种问题在单独试听每个文件时是发现不了的**，必须靠统一归一化在源头解决。

### 还需要人耳确认的时长

归一化是机器能替你做完的，时长不行。

**`sfx_enemy_email_death.wav` 是 0.704 秒，目标 0.25 秒。** 它是全场第二高频的音，还兼着通用死亡音。周五一秒杀十只怪，0.7 秒的音会有七八个实例持续叠着——即使有 4 实例上限，听感也是一团糊。建议裁到 0.3 秒以内，尾巴加 50 毫秒淡出防爆音：

```powershell
ffmpeg -y -i converted/sfx_enemy_email_death.wav -af "atrim=0:0.30,afade=t=out:st=0.25:d=0.05" -c:a pcm_s16le converted/sfx_enemy_email_death.wav
```

`sfx_enemy_bug_split.wav`（0.48s / 目标 0.2s）同理，但它频率低得多，可以放到后面再说。

### 新素材怎么加进来

1. 把原始文件丢进本目录根下（**不要放进 `converted/`，那个目录每次跑脚本都会被清空**）
2. 在 `convert.ps1` 的 `$sfx` 或 `$bgm` 里加一行映射
3. 重跑脚本

漏了第 2 步的话，脚本结尾会用黄字列出**未映射的源文件**，不会静默跳过。这条检查是专门为"素材陆续到货"这种情况加的——四个掉落音分批进来时，最容易发生的事故就是转了三个忘了第四个，而缺的那个在游戏里表现为"这一档没声音"，很难第一时间联想到是转换漏了。

---

## 三、Unity 导入设置

放到 `Assets/Audio/` 下，按类型分三个子目录，导入设置**按目录批量选中一次性改**，不要一个个点。

| 目录 | 内容 | Load Type | Compression | Quality | Force To Mono | Preload |
| --- | --- | --- | --- | --- | --- | --- |
| `Audio/SFX/` | 所有 < 1 秒的音效 | Decompress On Load | **PCM** | — | ✓ | ✓ |
| `Audio/Drop/` | 四档掉落 | Decompress On Load | Vorbis | 80 | **✗** | ✓ |
| `Audio/Loop/` | `lowsan_loop`、后续的光环 loop | Compressed In Memory | Vorbis | 70 | ✓ | |
| `Audio/BGM/` | 4 段 BGM | **Streaming** | Vorbis | 65 | ✗ | |

三条选择理由：

**短音效用 PCM + Decompress On Load。** 它们体积极小（0.15 秒单声道 wav 只有 13KB），但会被高频触发。用压缩格式的话每次播放都要解码，六把订书机同时开火时会出现可听见的 CPU 尖峰。**用空间换时间，这里空间几乎不要钱。**

**Loop 用 Compressed In Memory。** 常驻内存但不占太多，切换时不卡。

**BGM 用 Streaming。** 四段加起来几分钟，全解压进内存是浪费。

**掉落音单独一档，而且不要勾 Force To Mono。** 这四条是立体声成品，勾了等于把左右声道加起来，声像和空间感全没了——这是唯一一处我建议保留立体声的音效。它们同时也要用 **2D 播放**（`spatialBlend = 0`），别跟着掉落物的世界坐标做声像：一是 Unity 对立体声 clip 做 3D 空间化的结果本来就不可控，二是掉落反馈本质上是给玩家的 UI 提示，不是环境音，让它稳定居中反而更清楚。四条加起来原始体积 1.4 MB，转 Vorbis 之后可以忽略。

还有一条容易漏的：**BGM 记得勾 Load In Background**，否则切场景时主线程会卡一下。

---

## 四、代码接入

### 目录结构

```
Assets/
  Audio/
    SFX/      *.wav      短音效，PCM
    Loop/     *.wav      循环音效
    BGM/      *.ogg      背景音乐
  Scripts/
    Audio/
      AudioKey.cs        音效 key 枚举
      AudioManager.cs    播放入口
      AudioLibrary.cs    ScriptableObject，key -> clip 映射
```

### key 枚举

```csharp
public enum AudioKey
{
    // 武器
    StaplerFire, StaplerHit,
    // 敌人
    EmailDeath, BugSplit,
    // 玩家
    PlayerHurt, PlayerDeath, LowSanLoop,
    // 掉落
    DropWhite, DropBlue, DropYellow, DropOrange,
    Pickup, ConvertXp,
    // 咖啡与成长
    CoffeeDrop, CoffeeDrink, LevelUp, CardAppear,
    // 流程
    ClockIn, DayEnd,
}
```

**key 按设计文档的全集写，不按当前素材写。** 缺素材的先在 AudioLibrary 里留空或指向一个占位 clip，`Play()` 里已经做了空判直接返回。这样素材陆续到货时只需要往 ScriptableObject 上拖文件，调用方一行都不用改。

### AudioLibrary（ScriptableObject）

```csharp
[CreateAssetMenu(menuName = "996/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public AudioKey key;
        public AudioClip[] variants;   // 多条时随机取一条
        [Range(0f, 2f)] public float volume = 1f;
        public bool loop;
    }

    public Entry[] entries;
}
```

`variants` 是给订书机、邮件死亡、受击这几个高频音准备的。目前每个只有一条，后面补到 3 条时直接往数组里拖，代码不用动。

### 四档掉落的 volume 必须手动设

`volume` 字段不是可选的微调，**对掉落音来说它是让四档层级成立的唯一手段**。素材本身的响度是白 −12.2、蓝 −8.3、黄 −12.2、橙 −8.0 LUFS，两两成对而不是四档递进。全部留 1.0 的话，玩家听到的是"蓝色和橙色一样响、黄色和白色一样轻"，等于没有分档。

填这组起始值：

| AudioKey | volume | 相当于 | 调整后响度 |
| --- | --- | --- | --- |
| `DropWhite` | **0.50** | −6.0 dB | ≈ −18 LUFS |
| `DropBlue` | **0.46** | −6.7 dB | ≈ −15 LUFS |
| `DropYellow` | **1.00** | 0 dB | ≈ −12 LUFS |
| `DropOrange` | **0.90** | −1.0 dB | ≈ −9 LUFS |

**蓝色的 volume 比白色还低，这看着反直觉但是对的。** 素材里蓝色本来就比白色响了近 4 dB，要让它只比白色高一档，就得先把这 4 dB 压回去再加。黄色只能留 1.0，因为它是四档里最轻的一条而真峰值已经顶到 0 dBFS 以上——**它没有往上调的空间，整个阶梯只能靠把其余三条往下压来搭**。

配套的一件事：**掉落总线要留 3 dB 余量**。四条素材的真峰值都在 0 dBFS 之上（+0.03 到 +0.39 dBTP），是压过限制器的成品，直接满量播放遇上重采样会出现瞬间过载。

调完验收只有一个动作：**把四个音连着播一遍，听得出由轻到重才算过。** 这是"听声辨品质"这个卖点唯一的验收方式，数字对不对不重要，耳朵说了算——上面这组值是起点不是终点。

### AudioManager

三件必做的事全在这里，**缺任何一件后期都会变成噪音墙**。

```csharp
public class AudioManager : MonoBehaviour
{
    public static AudioManager I;

    [SerializeField] AudioLibrary library;
    [SerializeField] AudioMixerGroup sfxGroup, uiGroup, bgmGroup;
    [SerializeField] AudioSource bgmSource;

    const float ThrottleWindow = 0.06f;   // 同 key 最小间隔 60ms
    const int   MaxSameKey     = 4;       // 同 key 同时最多 4 个实例
    const int   PoolSize       = 24;

    readonly Dictionary<AudioKey, float> _lastPlayTime = new();
    readonly Dictionary<AudioKey, int>   _activeCount  = new();
    readonly List<AudioSource> _pool = new();

    void Awake()
    {
        I = this;
        for (int i = 0; i < PoolSize; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.outputAudioMixerGroup = sfxGroup;
            _pool.Add(src);
        }
    }

    public void Play(AudioKey key)
    {
        // 一、节流
        if (_lastPlayTime.TryGetValue(key, out var t) && Time.unscaledTime - t < ThrottleWindow)
            return;
        if (_activeCount.TryGetValue(key, out var n) && n >= MaxSameKey)
            return;

        var entry = library.Find(key);
        if (entry == null || entry.variants.Length == 0) return;

        var src = GetFreeSource();
        if (src == null) return;

        src.clip   = entry.variants[Random.Range(0, entry.variants.Length)];
        src.volume = entry.volume;

        // 二、音高随机化
        src.pitch  = Random.Range(0.92f, 1.08f);

        src.Play();
        _lastPlayTime[key] = Time.unscaledTime;
        _activeCount[key]  = n + 1;
        StartCoroutine(ReleaseAfter(key, src.clip.length / src.pitch));
    }
}
```

**一、节流。** 周五一秒杀十只怪，十个死亡音同时播出来不是"激烈"，是一坨糊掉的白噪音。60 毫秒窗口加 4 个实例上限，听起来反而更清晰。

**二、音高随机化。** `Random.Range(0.92f, 1.08f)` 这一行。不加的话订书机连续响两百次会让人想关掉游戏——**这是所有弹幕类游戏里最经典的音频翻车点**，而修它只要一行。

**三、三条总线。** 在 AudioMixer 里建 `BGM / SFX / UI` 三组。掉落音效走 SFX 但**单独提 3 dB**，它是核心卖点，必须能从战斗噪音里穿出来。

### 黄橙掉落的闪避（ducking）

```csharp
public void PlayRareDrop(AudioKey key)
{
    Play(key);
    StartCoroutine(Duck());   // BGM 与普通 SFX 各压 6dB，0.3 秒后恢复
}
```

这一段**不是锦上添花，是四档掉落能不能被听见的前提**。后期战斗最激烈的时候正好也是掉落最多的时候，不做闪避，我们最贵的那个橙装音效会在最该被听到的时刻被彻底埋掉。

配套的 BGM 低通滤波（`AudioLowPassFilter` + 0.3 秒 Tween）见设计文档第十节。

### 战斗 BGM 的六天变速

```csharp
// 周一 100%，每天 +4%，周五 116%
bgmSource.pitch = 1f + 0.04f * (day - 1);
```

一首曲子推完六天的情绪，代价是零。BOSS 三个阶段同理：阶段一挂高切滤波器，阶段二去掉，阶段三 `pitch = 1.06f` 并提 2 dB。

### 现有素材的时长不够，怎么循环

`bgm_battle` 只有 15 秒、`bgm_boss` 14 秒，直接循环会听出重复。两个办法，优先第一个：

1. **在 Unity 里做 0.5 秒交叉淡入淡出**（两个 AudioSource 交替），15 秒的段落循环起来接缝不明显
2. 让 AI 重新生成 60 秒版本

**不要花时间手工修 loop 点**，那能耗掉一小时而玩家听不出区别。

---

## 五、接入顺序建议

按这个顺序接，每一步都能立刻听到效果，出问题也好定位：

1. `sfx_weapon_stapler_fire` —— 全场播放最频繁，**先验证节流和音高随机是否生效**
2. `sfx_enemy_email_death` —— 验证多实例并发
3. `sfx_player_hurt` —— 验证负反馈
4. 四档掉落 + `sfx_drop_pickup` —— 打宝闭环，同时把 `volume` 调到位
5. `bgm_battle` —— 验证 Streaming 和变速
6. 其余按需

第 1 步是最重要的一步。**如果订书机的节流和随机音高在第一天就调对了，后面所有音效直接套同一套参数**；如果留到最后再调，就得回头把每个音效重听一遍。

---

## 六、还缺的音效

对照设计文档第十四节的最小可用集（P0 共 12 个）：

### 还缺 1 个 P0

| 缺失 | 时长 | 英文提示词 | 为什么必须补 |
| --- | --- | --- | --- |
| **红章落下** | 0.60s | `a rubber stamp slammed hard onto a stack of paper on a hard wooden desk, heavy authoritative thud with a faint ink squelch` | 结算页的句号，会被截图录屏 |

生成时记得给提示词补上统一后缀：

```
..., close-miked, dry, no reverb, no music, single one-shot
```

P1 及之后还缺的完整清单（键盘砸击、工卡、周报、光环 loop、BOSS 技能与血条碎裂、精英登场、技能预警、摸鱼、经验拾取、换装提示、按钮音、KPI 进度条、日光灯 loop 等约 34 个），见 wiki 第十四节，每条都带时长和提示词。

---

## 七、验收 checklist

转换完成后逐条过一遍，**十分钟的事，能省掉赛中一堆莫名其妙的手感问题**：

脚本已经自动满足的（重跑时看汇总表即可，不用手工查）：

- [x] `converted/` 里没有任何 `.mp4`
- [x] 开头没有静音
- [x] 短音效全部单声道 44.1kHz，BGM 立体声
- [x] 音效峰值统一 −6 dBFS，BGM −16 LUFS，无削波

需要人来判断的：

- [ ] 四档掉落按第四节填 `volume`（0.50 / 0.46 / 1.00 / 0.90），掉落总线留 3 dB 余量
- [ ] 四档掉落音**连续播放一遍**，能明确听出由轻到重的递进
- [ ] `sfx_enemy_email_death` 裁到 0.3 秒以内
- [ ] Unity 里短音效是 PCM + Decompress On Load，BGM 是 Streaming
- [ ] 订书机连点 20 次，**不刺耳**（音高随机生效）
- [ ] 同时杀 10 只怪，**不糊**（节流生效）
- [ ] 战斗最激烈时掉一件橙装，**那声"叮咚"听得清**（闪避生效）

最后三条是这套音频系统真正的验收标准。前面所有工作都是为了这三个瞬间。
