using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.View
{
    public enum FxPriority
    {
        Trash = 0,
        Elite = 1,
        Legendary = 2,
    }

    /// <summary>
    /// Hitstop, shake, screen flash and drop performance, all parameterised by quality so one
    /// code path produces four tiers. Time.timeScale is never touched: a hitstop only freezes
    /// GameClock, which means particles and ui keep playing through it.
    /// </summary>
    public sealed class JuiceService
    {
        /// <summary>
        /// Throttle plus a priority gate, enforced here rather than by convention. Late game kills
        /// ten enemies a second and any per kill hitstop would turn the game into a slideshow.
        /// </summary>
        const float HitStopMinInterval = 0.25f;

        readonly ConfigManager _cfg;
        readonly EventBus _bus;
        readonly PoolService _pool;
        readonly Transform _fxRoot;

        Transform _cameraTransform;
        Image _flash;
        Image _pieOverlay;

        float _hitStopUntil;
        float _lastHitStopAt = -999f;

        float _shakeAmount;
        float _shakeDecay = 12f;

        Color _flashColor = Color.white;
        float _flashAmount;

        float _pieStartedAt;
        float _pieUntil;
        float _pieDuration;

        readonly List<PulseFx> _pulses = new List<PulseFx>(32);

        struct PulseFx
        {
            public EntityView View;
            public float StartedAt;
            public float Duration;
            public float FromRadius;
            public float ToRadius;
            public Color Color;
        }

        public JuiceService(ConfigManager cfg, EventBus bus, PoolService pool, Transform fxRoot)
        {
            _cfg = cfg;
            _bus = bus;
            _pool = pool;
            _fxRoot = fxRoot;

            _bus.Register(EventID.LootDropped, OnLootDropped);
            _bus.Register(EventID.EnemyKilled, OnEnemyKilled);
            _bus.Register(EventID.PlayerDamaged, OnPlayerDamaged);
            _bus.Register(EventID.LootPicked, OnLootPicked);
            _bus.Register(EventID.BossTelegraph, OnAoePulse);
            _bus.Register(EventID.BossPieCast, OnBossPieCast);
            _bus.Register(EventID.SkillCast, OnSkillCast);
            _bus.Register(EventID.SlamLanded, OnSlamLanded);
            _bus.Register(EventID.SelectAll, OnSelectAll);
            _bus.Register(EventID.BossPhaseChanged, OnBossPhaseChanged);
            _bus.Register(EventID.PlayerShieldBroken, OnShieldBroken);
            _bus.Register(EventID.RunStarted, OnRunStarted);
            _bus.Register(EventID.GameStateChanged, OnGameStateChanged);
        }

        public void Dispose()
        {
            _bus.Unregister(EventID.LootDropped, OnLootDropped);
            _bus.Unregister(EventID.EnemyKilled, OnEnemyKilled);
            _bus.Unregister(EventID.PlayerDamaged, OnPlayerDamaged);
            _bus.Unregister(EventID.LootPicked, OnLootPicked);
            _bus.Unregister(EventID.BossTelegraph, OnAoePulse);
            _bus.Unregister(EventID.BossPieCast, OnBossPieCast);
            _bus.Unregister(EventID.SkillCast, OnSkillCast);
            _bus.Unregister(EventID.SlamLanded, OnSlamLanded);
            _bus.Unregister(EventID.SelectAll, OnSelectAll);
            _bus.Unregister(EventID.BossPhaseChanged, OnBossPhaseChanged);
            _bus.Unregister(EventID.PlayerShieldBroken, OnShieldBroken);
            _bus.Unregister(EventID.RunStarted, OnRunStarted);
            _bus.Unregister(EventID.GameStateChanged, OnGameStateChanged);
        }

        public void Bind(Transform cameraTransform, Image flash, Image pieOverlay)
        {
            _cameraTransform = cameraTransform;
            _flash = flash;
            _pieOverlay = pieOverlay;
        }

        // ---------- requests ----------

        public void RequestHitStop(float duration, FxPriority priority)
        {
            if (duration <= 0f)
            {
                return;
            }

            if (priority < FxPriority.Elite)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - _lastHitStopAt < HitStopMinInterval)
            {
                return;
            }

            _lastHitStopAt = now;
            _hitStopUntil = Mathf.Max(_hitStopUntil, now + duration);
        }

        public void RequestShake(float amount)
        {
            _shakeAmount = Mathf.Max(_shakeAmount, amount);
        }

        public void RequestFlash(Color color, float amount)
        {
            _flashColor = color;
            _flashAmount = Mathf.Max(_flashAmount, amount);
        }

        public bool HitStopActive
        {
            get { return Time.unscaledTime < _hitStopUntil; }
        }

        // ---------- per frame ----------

        public void Tick(float unscaledDt)
        {
            // JuiceService owns the hitstop channel of the logic clock. The flow machine owns the
            // pause channel, and the debug panel owns the third one, so no two writers collide.
            GameClock.FxScale = HitStopActive ? 0f : 1f;

            if (_cameraTransform != null)
            {
                // Shake is a local offset on the camera under a follow rig, so it can never feed
                // back into the follow lerp.
                Vector2 offset = _shakeAmount > 0.001f
                    ? new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * _shakeAmount
                    : Vector2.zero;

                _cameraTransform.localPosition = new Vector3(offset.x, offset.y, 0f);
                _shakeAmount = Mathf.Max(0f, _shakeAmount - _shakeDecay * unscaledDt * _shakeAmount);
                if (_shakeAmount < 0.002f)
                {
                    _shakeAmount = 0f;
                }
            }

            if (_flash != null)
            {
                _flashAmount = Mathf.Max(0f, _flashAmount - unscaledDt * 3.2f);
                Color c = _flashColor;
                c.a = _flashAmount;
                _flash.color = c;
                _flash.enabled = _flashAmount > 0.001f;
            }

            TickPie(unscaledDt);
            TickPulses();
        }

        void TickPie(float unscaledDt)
        {
            if (_pieOverlay == null || !_pieOverlay.enabled)
            {
                return;
            }

            float now = GameClock.Now;
            if (now >= _pieUntil)
            {
                HidePie();
                return;
            }

            float t = Mathf.Clamp01((now - _pieStartedAt) / Mathf.Max(0.01f, _pieDuration));
            float fadeIn = Mathf.Clamp01(t / 0.12f);
            float fadeOut = 1f - Mathf.Clamp01((t - 0.72f) / 0.28f);
            Color color = Color.white;
            color.a = 0.58f * Mathf.Min(fadeIn, fadeOut);
            _pieOverlay.color = color;
            _pieOverlay.rectTransform.localRotation *= Quaternion.Euler(0f, 0f, unscaledDt * 8f);
        }

        void HidePie()
        {
            if (_pieOverlay == null)
            {
                return;
            }

            _pieOverlay.enabled = false;
            _pieOverlay.color = new Color(1f, 1f, 1f, 0f);
            _pieOverlay.rectTransform.localRotation = Quaternion.identity;
            _pieStartedAt = 0f;
            _pieUntil = 0f;
            _pieDuration = 0f;
        }

        void TickPulses()
        {
            float now = Time.unscaledTime;
            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                PulseFx fx = _pulses[i];
                float t = (now - fx.StartedAt) / fx.Duration;

                if (t >= 1f || fx.View == null)
                {
                    if (fx.View != null)
                    {
                        fx.View.ResetDecorations();
                        _pool.Recycle(fx.View.gameObject);
                    }

                    _pulses.RemoveAt(i);
                    continue;
                }

                float radius = Mathf.Lerp(fx.FromRadius, fx.ToRadius, t);
                Color c = fx.Color;
                c.a = fx.Color.a * (1f - t);
                fx.View.ShowRing(c, radius);
            }
        }

        public void SpawnPulse(Vector2 pos, float radius, Color color, float duration)
        {
            GameObject go = _pool.Spawn("fx.pulse", NewPulse);
            EntityView v = go.GetComponent<EntityView>();
            v.ResetDecorations();
            v.Body.enabled = false;
            v.SetWorldPosition(pos);

            PulseFx fx = new PulseFx();
            fx.View = v;
            fx.StartedAt = Time.unscaledTime;
            fx.Duration = Mathf.Max(0.05f, duration);
            fx.FromRadius = radius * 0.35f;
            fx.ToRadius = radius;
            fx.Color = color;
            _pulses.Add(fx);
        }

        GameObject NewPulse()
        {
            EntityView v = EntityView.Create("fx.pulse", 10);
            v.transform.SetParent(_fxRoot, false);
            v.Body.enabled = false;
            return v.gameObject;
        }

        // ---------- event handlers ----------

        void OnLootDropped(EvtArg arg)
        {
            Model.LootModel loot = arg.O0 as Model.LootModel;
            if (loot == null || loot.Kind == Model.LootKind.Coffee)
            {
                return;
            }

            Quality q = (Quality)arg.I1;
            QualityDef def = _cfg.QualityOf(q);

            FxPriority priority = q == Quality.Orange
                ? FxPriority.Legendary
                : q == Quality.Yellow ? FxPriority.Elite : FxPriority.Trash;

            RequestHitStop(def.HitStop, priority);
            RequestShake(def.Shake * 0.02f);

            if (q >= Quality.Yellow)
            {
                RequestFlash(def.Color, q == Quality.Orange ? 0.55f : 0.18f);
                SpawnPulse(arg.P0, q == Quality.Orange ? 3.2f : 1.8f, def.Color, 0.5f);
            }
        }

        void OnLootPicked(EvtArg arg)
        {
            Model.LootModel loot = arg.O0 as Model.LootModel;
            if (loot != null && loot.Kind == Model.LootKind.Coffee)
            {
                RequestFlash(new Color(0.75f, 0.55f, 0.35f), 0.12f);
                return;
            }

            Quality q = (Quality)arg.I1;
            QualityDef def = _cfg.QualityOf(q);
            RequestFlash(def.Color, q >= Quality.Yellow ? 0.25f : 0.06f);
        }

        void OnEnemyKilled(EvtArg arg)
        {
            EnemyTier tier = (EnemyTier)arg.I1;
            if (tier == EnemyTier.Normal)
            {
                return;
            }

            RequestHitStop(tier == EnemyTier.Boss ? 0.16f : 0.06f, tier == EnemyTier.Boss ? FxPriority.Legendary : FxPriority.Elite);
            RequestShake(tier == EnemyTier.Boss ? 0.22f : 0.08f);
            SpawnPulse(arg.P0, tier == EnemyTier.Boss ? 4f : 2f, new Color(1f, 0.9f, 0.6f, 0.9f), 0.45f);
        }

        void OnPlayerDamaged(EvtArg arg)
        {
            RequestShake(0.09f);
            RequestFlash(new Color(0.9f, 0.15f, 0.2f), 0.3f);
        }

        void OnAoePulse(EvtArg arg)
        {
            SpawnPulse(arg.P0, Mathf.Max(0.5f, arg.F0), new Color(0.6f, 0.8f, 1f, 0.8f), 0.3f);
        }

        void OnBossPieCast(EvtArg arg)
        {
            if (_pieOverlay == null || _pieOverlay.sprite == null)
            {
                return;
            }

            _pieDuration = Mathf.Max(0.05f, arg.F0);
            _pieStartedAt = GameClock.Now;
            _pieUntil = _pieStartedAt + _pieDuration;
            _pieOverlay.enabled = true;
            _pieOverlay.color = new Color(1f, 1f, 1f, 0f);
            _pieOverlay.rectTransform.localRotation = Quaternion.identity;
        }

        void OnRunStarted(EvtArg arg)
        {
            HidePie();
        }

        void OnGameStateChanged(EvtArg arg)
        {
            Systems.GameState state = (Systems.GameState)arg.I0;
            if (state == Systems.GameState.MainMenu || state == Systems.GameState.Result)
            {
                HidePie();
            }
        }

        void OnSkillCast(EvtArg arg)
        {
            SpawnPulse(arg.P0, Mathf.Max(0.5f, arg.F0), new Color(0.5f, 1f, 0.8f, 0.9f), 0.35f);
            RequestShake(0.05f);
        }

        void OnSlamLanded(EvtArg arg)
        {
            SpawnPulse(arg.P0, Mathf.Max(0.5f, arg.F0), new Color(1f, 0.85f, 0.45f, 0.85f), 0.28f);
            RequestShake(0.04f);
        }

        void OnSelectAll(EvtArg arg)
        {
            RequestFlash(new Color(0.45f, 0.7f, 1f), 0.35f);
            RequestShake(0.12f);
        }

        void OnBossPhaseChanged(EvtArg arg)
        {
            RequestHitStop(0.14f, FxPriority.Legendary);
            RequestShake(0.26f);
            RequestFlash(new Color(1f, 0.4f, 0.85f), 0.4f);
            SpawnPulse(arg.P0, 5f, new Color(1f, 0.45f, 0.85f, 0.9f), 0.6f);
        }

        void OnShieldBroken(EvtArg arg)
        {
            SpawnPulse(arg.P0, Mathf.Max(0.5f, arg.F0), new Color(0.45f, 0.85f, 1f, 0.9f), 0.4f);
            RequestFlash(new Color(0.45f, 0.85f, 1f), 0.2f);
        }
    }
}
