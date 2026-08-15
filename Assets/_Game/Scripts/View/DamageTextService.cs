using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.UI;
using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.View
{
    /// <summary>
    /// Pooled floating numbers on a dedicated overlay canvas. Runs on unscaled time so numbers keep
    /// rising through a hitstop, which is the whole point of not touching Time.timeScale.
    /// </summary>
    public sealed class DamageTextService
    {
        const float Lifetime = 0.7f;
        const float RiseSpeed = 60f;
        const int MaxLive = 48;
        const string PlayerViewId = "v_player";

        readonly EventBus _bus;
        readonly ConfigManager _cfg;
        readonly Transform _canvasRoot;

        readonly List<Entry> _live = new List<Entry>(MaxLive);
        readonly Stack<Text> _idle = new Stack<Text>(MaxLive);

        Camera _camera;
        Canvas _canvas;

        class Entry
        {
            public Text Label;
            public Vector2 World;
            public float StartedAt;
            public float ScreenOffset;
            public Color Color;
            public float Scale;
        }

        public DamageTextService(EventBus bus, ConfigManager cfg, Transform canvasRoot)
        {
            _bus = bus;
            _cfg = cfg;
            _canvasRoot = canvasRoot;

            _bus.Register(EventID.EnemyDamaged, OnEnemyDamaged);
            _bus.Register(EventID.PlayerDamaged, OnPlayerDamaged);
            _bus.Register(EventID.PlayerDodged, OnPlayerDodged);
            _bus.Register(EventID.PlayerHealed, OnPlayerHealed);
            _bus.Register(EventID.LootPicked, OnLootPicked);
            _bus.Register(EventID.EquipDeclined, OnEquipDeclined);
            _bus.Register(EventID.PlayerLevelUp, OnLevelUp);
        }

        public void Dispose()
        {
            _bus.Unregister(EventID.EnemyDamaged, OnEnemyDamaged);
            _bus.Unregister(EventID.PlayerDamaged, OnPlayerDamaged);
            _bus.Unregister(EventID.PlayerDodged, OnPlayerDodged);
            _bus.Unregister(EventID.PlayerHealed, OnPlayerHealed);
            _bus.Unregister(EventID.LootPicked, OnLootPicked);
            _bus.Unregister(EventID.EquipDeclined, OnEquipDeclined);
            _bus.Unregister(EventID.PlayerLevelUp, OnLevelUp);
        }

        public void Bind(Camera camera)
        {
            _camera = camera;
        }

        public void Push(Vector2 world, string content, Color color, float scale)
        {
            if (_live.Count >= MaxLive)
            {
                return;
            }

            Text label = _idle.Count > 0 ? _idle.Pop() : NewLabel();
            label.gameObject.SetActive(true);
            label.text = content;
            label.color = color;

            Entry e = new Entry();
            e.Label = label;
            e.World = world;
            e.StartedAt = Time.unscaledTime;
            e.ScreenOffset = Random.Range(-14f, 14f);
            e.Color = color;
            e.Scale = scale;
            _live.Add(e);
        }

        public void Tick(float unscaledDt)
        {
            if (_camera == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            if (_canvas == null && _canvasRoot != null)
            {
                _canvas = _canvasRoot.GetComponentInParent<Canvas>();
            }

            float scaleFactor = _canvas != null ? _canvas.scaleFactor : 1f;
            if (scaleFactor <= 0f)
            {
                scaleFactor = 1f;
            }

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Entry e = _live[i];
                float t = (now - e.StartedAt) / Lifetime;

                if (t >= 1f)
                {
                    e.Label.gameObject.SetActive(false);
                    _idle.Push(e.Label);
                    _live.RemoveAt(i);
                    continue;
                }

                Vector3 screen = _camera.WorldToScreenPoint(new Vector3(e.World.x, e.World.y, 0f));
                Vector2 local = new Vector2(
                    screen.x / scaleFactor + e.ScreenOffset,
                    screen.y / scaleFactor + t * RiseSpeed);

                e.Label.rectTransform.anchoredPosition = local;

                Color c = e.Color;
                c.a = 1f - t * t;
                e.Label.color = c;

                float pop = 1f + Mathf.Sin(Mathf.Min(1f, t * 4f) * Mathf.PI * 0.5f) * 0.25f;
                e.Label.rectTransform.localScale = Vector3.one * e.Scale * pop;
            }
        }

        Text NewLabel()
        {
            Text text = UIFactory.CreateText(_canvasRoot, "dmg", "0", 30, Color.white, TextAnchor.LowerCenter);
            RectTransform rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(220f, 40f);
            return text;
        }

        /// <summary>
        /// Character art is anchored at the feet, so an event position is the ground the entity stands
        /// on. Numbers pushed there read as coming out of the floor and are covered by the body.
        /// </summary>
        Vector2 AboveHead(Vector2 origin, string viewId, float gap)
        {
            origin.y += EntityView.VisualTopOf(_cfg.View(viewId)) + gap;
            return origin;
        }

        // ---------- event handlers ----------

        void OnEnemyDamaged(EvtArg arg)
        {
            bool crit = arg.I1 == 1;
            string content = Mathf.RoundToInt(arg.F0).ToString();

            Vector2 at = arg.P0;
            Model.EnemyModel e = arg.O0 as Model.EnemyModel;
            if (e != null && e.Def != null)
            {
                // Elites and the boss already carry a health bar and a name plate over their head,
                // so their numbers start above that stack instead of through it.
                at = AboveHead(at, e.Def.ViewId, e.Def.Tier == EnemyTier.Normal ? 0.22f : 0.62f);
            }

            Push(at,
                crit ? content + "!" : content,
                crit ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 1f, 1f, 0.92f),
                crit ? 1.35f : 1f);
        }

        void OnPlayerDamaged(EvtArg arg)
        {
            Push(AboveHead(arg.P0, PlayerViewId, 0.22f),
                "-" + Mathf.RoundToInt(arg.F0), new Color(1f, 0.3f, 0.3f), 1.2f);
        }

        void OnPlayerDodged(EvtArg arg)
        {
            Push(AboveHead(arg.P0, PlayerViewId, 0.22f), "已读不回", new Color(0.6f, 0.9f, 1f), 1f);
        }

        void OnPlayerHealed(EvtArg arg)
        {
            int amount = Mathf.RoundToInt(arg.F0);

            // A heal against a nearly full bar is a real heal of a fraction of a point, and printing
            // it renders "+0" over the player's head. The skill fires itself on a cooldown, so at
            // full sanity that is a green zero every twelve seconds for the rest of the run.
            if (amount <= 0)
            {
                return;
            }

            Push(AboveHead(arg.P0, PlayerViewId, 0.22f),
                "+" + amount, new Color(0.4f, 1f, 0.5f), 1f);
        }

        void OnLootPicked(EvtArg arg)
        {
            Model.LootModel loot = arg.O0 as Model.LootModel;
            if (loot == null)
            {
                return;
            }

            Vector2 at = AboveHead(arg.P0, loot.ViewId, 0.22f);

            if (loot.Kind == Model.LootKind.Coffee)
            {
                Push(at, "咖啡", new Color(0.85f, 0.65f, 0.4f), 0.9f);
                return;
            }

            QualityDef qd = _cfg.QualityOf(loot.Quality);
            Push(at, loot.Name, qd.Color, 1.15f);
        }

        /// <summary>
        /// A declined drop has to say so. Silence on a downgrade reads as a pickup that did nothing,
        /// which is worse than not dropping the item at all.
        /// </summary>
        void OnEquipDeclined(EvtArg arg)
        {
            Model.LootModel loot = arg.O0 as Model.LootModel;
            Vector2 at = loot != null ? AboveHead(arg.P0, loot.ViewId, 0.22f) : arg.P0;
            Push(at, "折算 +" + arg.I0 + " 经验", new Color(0.75f, 0.78f, 0.82f), 0.95f);
        }

        void OnLevelUp(EvtArg arg)
        {
            Push(AboveHead(arg.P0, PlayerViewId, 0.22f),
                "升职 " + _cfg.RankOf(arg.I0), new Color(1f, 0.95f, 0.6f), 1.4f);
        }
    }
}
