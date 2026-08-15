using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using OfficeHell.Systems;
using UnityEngine;

namespace OfficeHell.View
{
    /// <summary>
    /// Code-owned audio buses keep the empty-scene architecture while imported clips replace Synth
    /// one key at a time. All fades use the existing presentation tick, so paused gameplay never
    /// requires a coroutine or another Update owner.
    /// </summary>
    public sealed class AudioService
    {
        public const string SfxStaplerFire = "sfx_weapon_stapler_fire";
        public const string SfxStaplerHit = "sfx_weapon_stapler_hit";
        public const string SfxEnemyDeath = "sfx_enemy_email_death";
        public const string SfxBugSplit = "sfx_enemy_bug_split";
        public const string SfxPlayerHurt = "sfx_player_hurt";
        public const string SfxPlayerDeath = "sfx_player_death";
        public const string SfxLowSanLoop = "sfx_player_lowsan_loop";
        public const string SfxDropPickup = "sfx_drop_pickup";
        public const string SfxConvertXp = "sfx_drop_convert_xp";
        public const string SfxCoffeeDrop = "sfx_coffee_drop";
        public const string SfxCoffeeDrink = "sfx_coffee_drink";
        public const string SfxLevelUp = "sfx_growth_levelup";
        public const string SfxCardAppear = "sfx_growth_card_appear";
        public const string SfxClockIn = "sfx_ui_clockin";
        public const string SfxDayEnd = "sfx_flow_dayend";

        public const string SfxDodge = "sfx_dodge";
        public const string SfxSkill = "sfx_skill";
        public const string SfxSlam = "sfx_slam";
        public const string SfxSelectAll = "sfx_select_all";
        public const string SfxBossPhase = "sfx_boss_phase";
        public const string SfxShieldBreak = "sfx_shield_break";
        public const string SfxUiClick = "sfx_ui_click";

        public const string BgmLogin = "bgm_login";
        public const string BgmBattle = "bgm_battle";
        public const string BgmBoss = "bgm_boss";
        public const string BgmResult = "bgm_result";

        const int SampleRate = 44100;

        sealed class SfxVoice
        {
            public AudioSource Source;
            public string Key;
            public SfxDef Def;
        }

        sealed class BgmChannel
        {
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public string Key;
            public BgmDef Def;
            public float FadeGain;
        }

        readonly ConfigManager _cfg;
        readonly GameContext _ctx;
        readonly EventBus _bus;
        readonly Transform _root;

        readonly List<SfxVoice> _voices = new List<SfxVoice>(24);
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>(32);
        readonly Dictionary<string, int> _playing = new Dictionary<string, int>(32);
        readonly Dictionary<string, float> _lastPlayedAt = new Dictionary<string, float>(32);
        readonly BgmChannel[] _bgm = new BgmChannel[2];

        AudioSource _lowSan;
        SfxDef _lowSanDef;
        float _lowSanGain;

        int _bgmCurrent = -1;
        int _bgmIncoming = -1;
        float _bgmFadeElapsed;
        float _bgmFadeDuration;

        float _duckUntil;
        float _duckSeconds;
        GameState _state = GameState.MainMenu;
        int _bossPhase = 1;
        bool _muted;

        public bool Muted
        {
            get { return _muted; }
            set
            {
                if (_muted == value)
                {
                    return;
                }

                _muted = value;
                ApplyVolumes();
            }
        }

        public string CurrentBgmId
        {
            get
            {
                if (_bgmIncoming >= 0)
                {
                    return _bgm[_bgmIncoming].Key;
                }

                return _bgmCurrent >= 0 ? _bgm[_bgmCurrent].Key : null;
            }
        }

        public AudioService(ConfigManager cfg, GameContext ctx, Transform root)
        {
            _cfg = cfg;
            _ctx = ctx;
            _bus = ctx.Bus;
            _root = root;

            BuildSources();
            BuildBgm();
            BuildLowSan();
            Subscribe();
            SwitchBgm(BgmLogin);
        }

        void BuildSources()
        {
            int count = Mathf.Clamp(_cfg.Audio.MaxSourcePool, 2, 48);
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("Sfx" + i);
                go.transform.SetParent(_root, false);

                SfxVoice voice = new SfxVoice();
                voice.Source = go.AddComponent<AudioSource>();
                voice.Source.playOnAwake = false;
                voice.Source.loop = false;
                voice.Source.spatialBlend = 0f;
                _voices.Add(voice);
            }
        }

        void BuildBgm()
        {
            for (int i = 0; i < _bgm.Length; i++)
            {
                GameObject go = new GameObject("Bgm" + i);
                go.transform.SetParent(_root, false);

                BgmChannel channel = new BgmChannel();
                channel.Source = go.AddComponent<AudioSource>();
                channel.Source.playOnAwake = false;
                channel.Source.loop = false;
                channel.Source.spatialBlend = 0f;
                channel.LowPass = go.AddComponent<AudioLowPassFilter>();
                channel.LowPass.cutoffFrequency = 22000f;
                _bgm[i] = channel;
            }
        }

        void BuildLowSan()
        {
            GameObject go = new GameObject("LowSanLoop");
            go.transform.SetParent(_root, false);
            _lowSan = go.AddComponent<AudioSource>();
            _lowSan.playOnAwake = false;
            _lowSan.loop = true;
            _lowSan.spatialBlend = 0f;
        }

        void Subscribe()
        {
            _bus.Register(EventID.ConfigReloaded, OnConfigReloaded);
            _bus.Register(EventID.GameStateChanged, OnGameStateChanged);
            _bus.Register(EventID.RunStarted, OnRunStarted);
            _bus.Register(EventID.DayStarted, OnDayStarted);
            _bus.Register(EventID.DayCleared, OnDayCleared);
            _bus.Register(EventID.WeaponFired, OnWeaponFired);
            _bus.Register(EventID.EnemyDamaged, OnEnemyDamaged);
            _bus.Register(EventID.EnemyKilled, OnEnemyKilled);
            _bus.Register(EventID.PlayerDamaged, OnPlayerDamaged);
            _bus.Register(EventID.PlayerDodged, OnPlayerDodged);
            _bus.Register(EventID.PlayerRankUp, OnRankUp);
            _bus.Register(EventID.PlayerDied, OnPlayerDied);
            _bus.Register(EventID.LootDropped, OnLootDropped);
            _bus.Register(EventID.LootPicked, OnLootPicked);
            _bus.Register(EventID.EquipDeclined, OnEquipDeclined);
            _bus.Register(EventID.CoffeeDrunk, OnCoffeeDrunk);
            _bus.Register(EventID.CardsOffered, OnCardsOffered);
            _bus.Register(EventID.CardPicked, OnCardPicked);
            _bus.Register(EventID.SkillCast, OnSkillCast);
            _bus.Register(EventID.SlamLanded, OnSlamLanded);
            _bus.Register(EventID.SelectAll, OnSelectAll);
            _bus.Register(EventID.BossSpawned, OnBossSpawned);
            _bus.Register(EventID.BossPhaseChanged, OnBossPhase);
            _bus.Register(EventID.PlayerShieldBroken, OnShieldBroken);

            // Shares the shield clip. The delivered set has no guard sound, and both events are the
            // same beat to the player: armour just stopped something.
            _bus.Register(EventID.PlayerGuarded, OnShieldBroken);
        }

        public void Dispose()
        {
            _bus.Unregister(EventID.ConfigReloaded, OnConfigReloaded);
            _bus.Unregister(EventID.GameStateChanged, OnGameStateChanged);
            _bus.Unregister(EventID.RunStarted, OnRunStarted);
            _bus.Unregister(EventID.DayStarted, OnDayStarted);
            _bus.Unregister(EventID.DayCleared, OnDayCleared);
            _bus.Unregister(EventID.WeaponFired, OnWeaponFired);
            _bus.Unregister(EventID.EnemyDamaged, OnEnemyDamaged);
            _bus.Unregister(EventID.EnemyKilled, OnEnemyKilled);
            _bus.Unregister(EventID.PlayerDamaged, OnPlayerDamaged);
            _bus.Unregister(EventID.PlayerDodged, OnPlayerDodged);
            _bus.Unregister(EventID.PlayerRankUp, OnRankUp);
            _bus.Unregister(EventID.PlayerDied, OnPlayerDied);
            _bus.Unregister(EventID.LootDropped, OnLootDropped);
            _bus.Unregister(EventID.LootPicked, OnLootPicked);
            _bus.Unregister(EventID.EquipDeclined, OnEquipDeclined);
            _bus.Unregister(EventID.CoffeeDrunk, OnCoffeeDrunk);
            _bus.Unregister(EventID.CardsOffered, OnCardsOffered);
            _bus.Unregister(EventID.CardPicked, OnCardPicked);
            _bus.Unregister(EventID.SkillCast, OnSkillCast);
            _bus.Unregister(EventID.SlamLanded, OnSlamLanded);
            _bus.Unregister(EventID.SelectAll, OnSelectAll);
            _bus.Unregister(EventID.BossSpawned, OnBossSpawned);
            _bus.Unregister(EventID.BossPhaseChanged, OnBossPhase);
            _bus.Unregister(EventID.PlayerShieldBroken, OnShieldBroken);
            _bus.Unregister(EventID.PlayerGuarded, OnShieldBroken);
        }

        // ---------- playback ----------

        public void Play(string sfxId)
        {
            if (_muted || string.IsNullOrEmpty(sfxId))
            {
                return;
            }

            SfxDef def;
            if (!_cfg.Audio.Sfx.TryGetValue(sfxId, out def))
            {
                return;
            }

            float now = Time.unscaledTime;
            float last;
            if (_lastPlayedAt.TryGetValue(sfxId, out last) && now - last < def.ThrottleSeconds)
            {
                return;
            }

            int active;
            _playing.TryGetValue(sfxId, out active);
            if (active >= def.MaxConcurrent)
            {
                return;
            }

            SfxVoice voice = FreeVoice();
            if (voice == null)
            {
                return;
            }

            AudioClip clip = ClipFor(def);
            if (clip == null)
            {
                return;
            }

            voice.Key = sfxId;
            voice.Def = def;
            voice.Source.clip = clip;
            voice.Source.loop = false;
            voice.Source.pitch = 1f + Random.Range(-def.PitchJitter, def.PitchJitter);
            voice.Source.volume = VolumeFor(def);
            voice.Source.Play();

            _lastPlayedAt[sfxId] = now;
            _playing[sfxId] = active + 1;
        }

        public void PlayBgm()
        {
            if (CurrentBgmId == null)
            {
                SwitchBgm(BgmForState());
            }
        }

        public void StopBgm()
        {
            for (int i = 0; i < _bgm.Length; i++)
            {
                _bgm[i].Source.Stop();
                _bgm[i].Source.clip = null;
                _bgm[i].Key = null;
                _bgm[i].Def = null;
                _bgm[i].FadeGain = 0f;
            }

            _bgmCurrent = -1;
            _bgmIncoming = -1;
            _bgmFadeElapsed = 0f;
        }

        /// <summary>Rare rewards own the mix while ordinary combat and music move behind them.</summary>
        public void DuckBgm(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            _duckSeconds = seconds;
            _duckUntil = Mathf.Max(_duckUntil, Time.unscaledTime + seconds);
        }

        public void Tick(float unscaledDt)
        {
            TickVoices();
            TickLowSan(unscaledDt);
            TickBgm(unscaledDt);
        }

        void TickVoices()
        {
            _playing.Clear();
            for (int i = 0; i < _voices.Count; i++)
            {
                SfxVoice voice = _voices[i];
                if (!voice.Source.isPlaying || voice.Source.clip == null || voice.Def == null)
                {
                    voice.Key = null;
                    voice.Def = null;
                    continue;
                }

                int active;
                _playing.TryGetValue(voice.Key, out active);
                _playing[voice.Key] = active + 1;
                voice.Source.volume = VolumeFor(voice.Def);
            }
        }

        void TickLowSan(float unscaledDt)
        {
            PlayerModel player = _ctx.Run.Player;
            float max = player.MaxSan;
            float threshold = _cfg.Coffee.LowSanThresholdPct * 0.01f;
            bool wanted = _state == GameState.Battle && player.Alive && max > 0f && player.San < max * threshold;

            if (wanted && !_lowSan.isPlaying)
            {
                if (_lowSanDef == null)
                {
                    _cfg.Audio.Sfx.TryGetValue(SfxLowSanLoop, out _lowSanDef);
                }

                AudioClip clip = _lowSanDef != null ? ClipFor(_lowSanDef) : null;
                if (clip != null)
                {
                    _lowSan.clip = clip;
                    _lowSan.pitch = 1f;
                    _lowSan.Play();
                }
            }

            float fade = Mathf.Max(0.01f, _cfg.Audio.LowSanFadeSeconds);
            _lowSanGain = Mathf.MoveTowards(_lowSanGain, wanted ? 1f : 0f, unscaledDt / fade);

            if (_lowSanDef != null)
            {
                float bus = _cfg.Audio.SfxVolume;
                float duck = IsDucked() ? DuckMultiplier() : 1f;
                _lowSan.volume = _muted ? 0f :
                    _lowSanDef.Volume * DbToLinear(_lowSanDef.GainDb) * bus * duck * _lowSanGain;
            }

            if (!wanted && _lowSanGain <= 0f && _lowSan.isPlaying)
            {
                _lowSan.Stop();
            }
        }

        void TickBgm(float unscaledDt)
        {
            if (_bgmIncoming >= 0)
            {
                _bgmFadeElapsed += unscaledDt;
                float t = Mathf.Clamp01(_bgmFadeElapsed / Mathf.Max(0.01f, _bgmFadeDuration));
                _bgm[_bgmIncoming].FadeGain = t;
                if (_bgmCurrent >= 0)
                {
                    _bgm[_bgmCurrent].FadeGain = 1f - t;
                }

                if (t >= 1f)
                {
                    CompleteBgmFade();
                }
            }
            else if (_bgmCurrent >= 0)
            {
                BgmChannel current = _bgm[_bgmCurrent];
                AudioClip clip = current.Source.clip;
                if (clip != null && current.Source.isPlaying)
                {
                    float pitch = Mathf.Max(0.01f, current.Source.pitch);
                    float fade = Mathf.Max(0.01f, current.Def.CrossfadeSeconds);
                    float remaining = (clip.length - current.Source.time) / pitch;
                    if (remaining <= fade)
                    {
                        BeginBgm(current.Key, true);
                    }
                }
                else if (current.Key != null)
                {
                    BeginBgm(current.Key, true);
                }
            }

            ApplyBgmProfiles(unscaledDt);
        }

        void SwitchBgm(string bgmId)
        {
            if (string.IsNullOrEmpty(bgmId))
            {
                return;
            }

            if (_bgmIncoming >= 0 && _bgm[_bgmIncoming].Key == bgmId)
            {
                return;
            }

            if (_bgmIncoming < 0 && _bgmCurrent >= 0 && _bgm[_bgmCurrent].Key == bgmId &&
                _bgm[_bgmCurrent].Source.isPlaying)
            {
                _bgm[_bgmCurrent].Def = BgmDefOf(bgmId);
                return;
            }

            BeginBgm(bgmId, false);
        }

        void BeginBgm(string bgmId, bool loopRestart)
        {
            BgmDef def = BgmDefOf(bgmId);
            if (def == null)
            {
                return;
            }

            AudioClip clip = BgmClip(def);
            if (clip == null)
            {
                return;
            }

            if (_bgmIncoming >= 0)
            {
                CompleteBgmFade();
            }

            int target = _bgmCurrent == 0 ? 1 : 0;
            if (_bgmCurrent < 0)
            {
                target = 0;
            }

            BgmChannel incoming = _bgm[target];
            incoming.Source.Stop();
            incoming.Source.clip = clip;
            incoming.Source.time = 0f;
            incoming.Key = bgmId;
            incoming.Def = def;
            incoming.FadeGain = 0f;
            incoming.Source.pitch = PitchFor(incoming);
            incoming.Source.Play();

            _bgmIncoming = target;
            _bgmFadeElapsed = 0f;
            _bgmFadeDuration = Mathf.Max(0.01f, def.CrossfadeSeconds);

            if (loopRestart && _bgmCurrent < 0)
            {
                incoming.FadeGain = 1f;
                CompleteBgmFade();
            }
        }

        void CompleteBgmFade()
        {
            if (_bgmIncoming < 0)
            {
                return;
            }

            if (_bgmCurrent >= 0 && _bgmCurrent != _bgmIncoming)
            {
                BgmChannel old = _bgm[_bgmCurrent];
                old.Source.Stop();
                old.Source.clip = null;
                old.Key = null;
                old.Def = null;
                old.FadeGain = 0f;
            }

            _bgmCurrent = _bgmIncoming;
            _bgmIncoming = -1;
            _bgm[_bgmCurrent].FadeGain = 1f;
            _bgmFadeElapsed = 0f;
        }

        void ApplyBgmProfiles(float unscaledDt)
        {
            for (int i = 0; i < _bgm.Length; i++)
            {
                BgmChannel channel = _bgm[i];
                if (!channel.Source.isPlaying || channel.Def == null)
                {
                    continue;
                }

                channel.Source.pitch = PitchFor(channel);

                float volume = channel.Def.Volume * VolumeBoostFor(channel) * channel.FadeGain;
                if (IsDucked())
                {
                    volume *= DuckMultiplier();
                }
                channel.Source.volume = _muted ? 0f : volume;

                float baseline = CutoffFor(channel);
                float target = IsDucked() ? Mathf.Min(baseline, channel.Def.CutoffDucked) : baseline;
                float speed = IsDucked() ? 12f : 3f / Mathf.Max(0.05f, _duckSeconds);
                channel.LowPass.cutoffFrequency = Mathf.Lerp(
                    channel.LowPass.cutoffFrequency,
                    Mathf.Clamp(target, 10f, 22000f),
                    Mathf.Clamp01(speed * unscaledDt));
            }
        }

        void ApplyVolumes()
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                SfxVoice voice = _voices[i];
                if (voice.Source.isPlaying && voice.Def != null)
                {
                    voice.Source.volume = VolumeFor(voice.Def);
                }
            }

            if (_lowSanDef != null)
            {
                float duck = IsDucked() ? DuckMultiplier() : 1f;
                _lowSan.volume = _muted ? 0f : _lowSanDef.Volume * DbToLinear(_lowSanDef.GainDb) *
                    _cfg.Audio.SfxVolume * duck * _lowSanGain;
            }

            ApplyBgmProfiles(0f);
        }

        float VolumeFor(SfxDef def)
        {
            if (_muted)
            {
                return 0f;
            }

            float bus = def.Bus == AudioBus.Ui ? _cfg.Audio.UiVolume : _cfg.Audio.SfxVolume;
            if (def.Bus == AudioBus.Sfx && !def.DuckExempt && IsDucked())
            {
                bus *= DuckMultiplier();
            }
            return def.Volume * DbToLinear(def.GainDb) * bus;
        }

        float PitchFor(BgmChannel channel)
        {
            if (channel.Key == BgmBattle)
            {
                return 1f + channel.Def.PitchPerDay * Mathf.Max(0, _ctx.Run.DayIndex - 1);
            }

            if (channel.Key == BgmBoss && _bossPhase >= 3)
            {
                return channel.Def.PhaseThreePitch;
            }

            return 1f;
        }

        float VolumeBoostFor(BgmChannel channel)
        {
            if (channel.Key == BgmBoss && _bossPhase >= 3)
            {
                return DbToLinear(channel.Def.PhaseThreeVolumeDb);
            }
            return 1f;
        }

        float CutoffFor(BgmChannel channel)
        {
            if (channel.Key == BgmBoss && _bossPhase <= 1)
            {
                return channel.Def.PhaseOneCutoff;
            }
            return channel.Def.CutoffNormal;
        }

        bool IsDucked()
        {
            return Time.unscaledTime < _duckUntil;
        }

        float DuckMultiplier()
        {
            return DbToLinear(_cfg.Audio.DuckVolumeDb);
        }

        static float DbToLinear(float db)
        {
            return Mathf.Pow(10f, db / 20f);
        }

        SfxVoice FreeVoice()
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                if (!_voices[i].Source.isPlaying)
                {
                    _voices[i].Key = null;
                    _voices[i].Def = null;
                    return _voices[i];
                }
            }
            return null;
        }

        AudioClip ClipFor(SfxDef def)
        {
            string cacheKey = !string.IsNullOrEmpty(def.Clip) ? "clip:" + def.Clip : "synth:" + def.Id;
            AudioClip clip;
            if (_clips.TryGetValue(cacheKey, out clip) && clip != null)
            {
                return clip;
            }

            if (!string.IsNullOrEmpty(def.Clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + def.Clip);
            }

            if (clip == null)
            {
                clip = Synth.Build(def, SampleRate);
            }

            _clips[cacheKey] = clip;
            return clip;
        }

        AudioClip BgmClip(BgmDef def)
        {
            string cacheKey = !string.IsNullOrEmpty(def.Clip) ? "clip:" + def.Clip : "bgm:" + def.Id;
            AudioClip clip;
            if (_clips.TryGetValue(cacheKey, out clip) && clip != null)
            {
                return clip;
            }

            if (!string.IsNullOrEmpty(def.Clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + def.Clip);
            }

            if (clip == null)
            {
                clip = Synth.BuildBgmLoop(SampleRate);
            }

            _clips[cacheKey] = clip;
            return clip;
        }

        BgmDef BgmDefOf(string bgmId)
        {
            BgmDef def;
            return _cfg.Audio.Bgm.TryGetValue(bgmId, out def) ? def : null;
        }

        string BgmForState()
        {
            if (_state == GameState.MainMenu)
            {
                return BgmLogin;
            }
            if (_state == GameState.Result)
            {
                return BgmResult;
            }
            if (_ctx.Run.Boss != null && !_ctx.Run.Boss.IsDead)
            {
                return BgmBoss;
            }
            return BgmBattle;
        }

        // ---------- event handlers ----------

        void OnConfigReloaded(EvtArg arg)
        {
            StopBgm();
            _lowSan.Stop();
            _lowSan.clip = null;
            _lowSanDef = null;
            _lowSanGain = 0f;
            _clips.Clear();
            SwitchBgm(BgmForState());
        }

        void OnGameStateChanged(EvtArg arg)
        {
            _state = (GameState)arg.I0;
            if (_state == GameState.MainMenu)
            {
                _bossPhase = 1;
                SwitchBgm(BgmLogin);
            }
            else if (_state == GameState.Result)
            {
                SwitchBgm(BgmResult);
            }
        }

        void OnRunStarted(EvtArg arg)
        {
            _bossPhase = 1;
            Play(SfxClockIn);
        }

        void OnDayStarted(EvtArg arg)
        {
            _bossPhase = 1;
            SwitchBgm(BgmBattle);
        }

        void OnDayCleared(EvtArg arg)
        {
            Play(SfxDayEnd);
        }

        void OnWeaponFired(EvtArg arg)
        {
            WeaponDef def = arg.O0 as WeaponDef;
            if (def != null && def.Id == "stapler")
            {
                Play(SfxStaplerFire);
            }
        }

        void OnEnemyDamaged(EvtArg arg)
        {
            Play(SfxStaplerHit);
        }

        /// <summary>
        /// The split layers on top of the death rather than replacing it. A body coming apart is two
        /// events, and swapping one clip for the other spent a whole enemy type's audio on a single
        /// low clip that never cut through. Keyed on the behavior, not on the "bug" id, so a second
        /// splitting enemy is a config change rather than another string compare here.
        /// </summary>
        void OnEnemyKilled(EvtArg arg)
        {
            Play(SfxEnemyDeath);

            EnemyModel enemy = arg.O0 as EnemyModel;
            if (enemy != null && enemy.Def != null && enemy.Def.Behavior == "SplitOnDeath")
            {
                Play(SfxBugSplit);
            }
        }

        void OnPlayerDamaged(EvtArg arg)
        {
            Play(SfxPlayerHurt);
        }

        void OnPlayerDodged(EvtArg arg)
        {
            Play(SfxDodge);
        }

        void OnRankUp(EvtArg arg)
        {
            Play(SfxLevelUp);
        }

        void OnPlayerDied(EvtArg arg)
        {
            Play(SfxPlayerDeath);
        }

        void OnLootDropped(EvtArg arg)
        {
            LootModel loot = arg.O0 as LootModel;
            if (loot == null)
            {
                return;
            }

            if (loot.Kind == LootKind.Coffee)
            {
                Play(SfxCoffeeDrop);
                return;
            }

            QualityDef def = _cfg.QualityOf(loot.Quality);
            Play(def.Sfx);
            DuckBgm(def.BgmLowPass);
        }

        void OnLootPicked(EvtArg arg)
        {
            LootModel loot = arg.O0 as LootModel;
            if (loot != null && loot.Kind != LootKind.Coffee)
            {
                Play(SfxDropPickup);
            }
        }

        void OnEquipDeclined(EvtArg arg)
        {
            Play(SfxConvertXp);
        }

        void OnCoffeeDrunk(EvtArg arg)
        {
            Play(SfxCoffeeDrink);
        }

        void OnCardsOffered(EvtArg arg)
        {
            Play(SfxCardAppear);
        }

        void OnCardPicked(EvtArg arg)
        {
            Play(SfxUiClick);
        }

        void OnSkillCast(EvtArg arg)
        {
            Play(SfxSkill);
        }

        void OnSlamLanded(EvtArg arg)
        {
            Play(SfxSlam);
        }

        void OnSelectAll(EvtArg arg)
        {
            Play(SfxSelectAll);
        }

        void OnBossSpawned(EvtArg arg)
        {
            _bossPhase = 1;
            SwitchBgm(BgmBoss);
        }

        void OnBossPhase(EvtArg arg)
        {
            _bossPhase = Mathf.Max(1, arg.I1);
            Play(SfxBossPhase);
            DuckBgm(0.8f);
        }

        void OnShieldBroken(EvtArg arg)
        {
            Play(SfxShieldBreak);
        }
    }
}
