using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using OfficeHell.Systems;
using UnityEngine;

namespace OfficeHell.View
{
    /// <summary>
    /// One way binding from models to views. Models never hold a view reference and views never
    /// write back, so a model can be recycled without touching the presentation side.
    /// </summary>
    public sealed class ViewBinder
    {
        const string KeyEnemy = "view.enemy";
        const string KeyProjectile = "view.proj";
        const string KeyLoot = "view.loot";
        const string KeySlam = "view.slam";
        const string KeyWarn = "view.warn";
        const string KeyOrbit = "view.orbit";

        readonly GameContext _ctx;
        readonly PoolService _pool;
        readonly Transform _root;

        readonly Dictionary<int, EntityView> _bound = new Dictionary<int, EntityView>(512);
        readonly List<int> _stale = new List<int>(128);
        readonly HashSet<int> _seen = new HashSet<int>();

        EntityView _playerView;
        EntityView _pickupRing;

        public ViewBinder(GameContext ctx, PoolService pool, Transform root)
        {
            _ctx = ctx;
            _pool = pool;
            _root = root;
        }

        public void Sync(float unscaledDt)
        {
            _seen.Clear();

            SyncPlayer();
            SyncEnemies();
            SyncProjectiles();
            SyncSlams();
            SyncTelegraphs();
            SyncOrbits();
            SyncLoot();
            Prune();
        }

        public void RecycleAll()
        {
            foreach (KeyValuePair<int, EntityView> kv in _bound)
            {
                if (kv.Value != null)
                {
                    kv.Value.ResetDecorations();
                    _pool.Recycle(kv.Value.gameObject);
                }
            }

            _bound.Clear();
        }

        void SyncPlayer()
        {
            PlayerModel p = _ctx.Run.Player;

            if (_playerView == null)
            {
                _playerView = EntityView.Create("PlayerView", 40);
                _playerView.transform.SetParent(_root, false);
                _playerView.Bind(_ctx.Cfg.View("v_player"), ViewShape.Circle, false);

                _pickupRing = EntityView.Create("PickupRing", 5);
                _pickupRing.transform.SetParent(_root, false);
                _pickupRing.Bind(ConfigManager.FallbackView, ViewShape.Quad, true);
                _pickupRing.Body.enabled = false;
            }

            _playerView.SetWorldPosition(p.Pos);
            _playerView.TickAnimation(
                GameClock.Delta,
                p.Facing.x,
                p.MoveIntent.sqrMagnitude > 0.0001f);

            // Invulnerability has to be visible or the frame reads as an unexplained miss.
            bool invuln = p.IsInvulnerable(GameClock.Now);
            _playerView.SetFlash(invuln && Mathf.Repeat(Time.unscaledTime * 12f, 1f) > 0.5f);

            if (p.HasShield)
            {
                _playerView.ShowRing(new Color(0.4f, 0.85f, 1f, 0.55f), p.Radius * 2.2f);
            }
            else
            {
                _playerView.HideRing();
            }

            _pickupRing.SetWorldPosition(p.Pos);
            _pickupRing.ShowRing(new Color(1f, 1f, 1f, 0.12f), p.MagnetRadius);
        }

        void SyncEnemies()
        {
            List<EnemyModel> enemies = _ctx.Run.Enemies;
            float now = GameClock.Now;

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyModel e = enemies[i];
                if (e.IsDead || e.Def == null)
                {
                    continue;
                }

                _seen.Add(e.Id);
                EntityView v = Bind(e.Id, KeyEnemy, 20, _ctx.Cfg.View(e.Def.ViewId));
                Vector2 motion = v.SetTrackedWorldPosition(e.Pos);
                v.TickAnimation(GameClock.Delta, motion.x, motion.sqrMagnitude > 0.000001f);
                v.SetFlash(now < e.FlashUntil || now < e.InvulnUntil);

                // The grace window has to be legible, otherwise a split BUG that cannot hurt you yet
                // looks identical to one that can and the player backs off for no reason.
                if (!e.CanTouch(now))
                {
                    float t = Mathf.InverseLerp(e.SpawnedAt, e.ContactArmedAt, now);
                    v.SetAlpha(Mathf.Lerp(0.45f, 1f, t));
                    v.SetScaleMultiplier(Mathf.Lerp(0.6f, 1f, t));
                }
                else
                {
                    v.SetScaleMultiplier(1f);
                }

                if (e.Def.Tier != EnemyTier.Normal)
                {
                    v.ShowBar(e.MaxHp > 0f ? e.Hp / e.MaxHp : 0f, Mathf.Max(0.9f, e.Radius * 2.4f));
                    v.ShowLabel(
                        BossLabel(e),
                        e.Def.Tier == EnemyTier.Boss ? new Color(1f, 0.5f, 0.9f) : new Color(1f, 0.85f, 0.3f));
                }

                if (now < e.TelegraphUntil)
                {
                    v.ShowRing(new Color(1f, 0.2f, 0.2f, 0.75f), e.Radius * 2.2f);
                }
                else if (e.AuraRadius > 0f)
                {
                    v.ShowRing(AuraColor(e.AuraKind), e.AuraRadius);
                }
                else
                {
                    v.HideRing();
                }
            }
        }

        /// <summary>Three bars means the label has to say which one, or the fight has no landmarks.</summary>
        static string BossLabel(EnemyModel e)
        {
            if (!e.IsBoss)
            {
                return e.Def.Name;
            }

            return e.Def.Name + "  " + new string('|', e.BarsLeft) + "  " + Mathf.CeilToInt(e.Hp);
        }

        static Color AuraColor(AuraChannel channel)
        {
            switch (channel)
            {
                case AuraChannel.MoveSlow: return new Color(0.35f, 0.6f, 1f, 0.16f);
                case AuraChannel.AttackSlow: return new Color(1f, 0.9f, 0.3f, 0.16f);
                default: return new Color(1f, 0.35f, 0.35f, 0.16f);
            }
        }

        void SyncProjectiles()
        {
            List<ProjectileModel> projectiles = _ctx.Run.Projectiles;

            for (int i = 0; i < projectiles.Count; i++)
            {
                ProjectileModel p = projectiles[i];
                if (p.IsDead)
                {
                    continue;
                }

                _seen.Add(p.Id);
                EntityView v = Bind(p.Id, KeyProjectile, 30, _ctx.Cfg.View(p.ViewId));
                v.SetWorldPosition(p.Pos);

                if (p.Vel.sqrMagnitude > 0.01f)
                {
                    float angle = Mathf.Atan2(p.Vel.y, p.Vel.x) * Mathf.Rad2Deg;
                    v.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                }
            }
        }

        /// <summary>
        /// The keyboard flies in from its own slot and lands on the marked spot. Without the wind up
        /// travel the hit is just a white flash and the only melee weapon in the game has no weight.
        /// </summary>
        void SyncSlams()
        {
            List<SlamModel> slams = _ctx.Run.Slams;
            float now = GameClock.Now;

            for (int i = 0; i < slams.Count; i++)
            {
                SlamModel s = slams[i];
                if (s.IsDead)
                {
                    continue;
                }

                _seen.Add(s.Id);

                float t = s.Progress01(now);
                EntityView v = Bind(s.Id, KeySlam, 34, _ctx.Cfg.View("v_slam"));

                Vector2 air = Vector2.Lerp(s.From, s.Target + Vector2.up * 1.2f, t);
                v.SetWorldPosition(Vector2.Lerp(air, s.Target, t * t));
                v.SetScaleMultiplier(Mathf.Lerp(0.7f, 1.15f, t));
                v.ShowRing(new Color(1f, 0.85f, 0.4f, 0.35f + 0.3f * t), s.Radius);
                v.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-40f, 0f, t));
            }
        }

        void SyncTelegraphs()
        {
            List<TelegraphModel> warns = _ctx.Run.Telegraphs;
            float now = GameClock.Now;

            for (int i = 0; i < warns.Count; i++)
            {
                TelegraphModel w = warns[i];
                if (w.IsDead)
                {
                    continue;
                }

                _seen.Add(w.Id);

                float t = w.Progress01(now);
                EntityView v = Bind(w.Id, KeyWarn, 6, _ctx.Cfg.View(w.ViewId));
                v.SetWorldPosition(w.Pos);
                v.Body.enabled = false;
                v.ShowRing(new Color(1f, 0.3f, 0.25f, 0.25f + 0.45f * t), w.Radius * Mathf.Lerp(0.6f, 1f, t));
            }
        }

        void SyncOrbits()
        {
            List<OrbitCardModel> cards = _ctx.Run.OrbitCards;

            for (int i = 0; i < cards.Count; i++)
            {
                OrbitCardModel c = cards[i];
                _seen.Add(c.Id);

                EntityView v = Bind(c.Id, KeyOrbit, 32, _ctx.Cfg.View(c.ViewId));
                v.SetWorldPosition(c.Pos);
                v.transform.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 90f);
            }

            // The orange tether is one ring, matching its hit shape. Four line segments would read as
            // four separate ropes rather than as the single halo the upgrade promises.
            if (cards.Count > 0 && cards[0].Tethered)
            {
                ShowTether(cards[0]);
            }
            else
            {
                HideTether();
            }
        }

        EntityView _tether;

        void ShowTether(OrbitCardModel c)
        {
            if (_tether == null)
            {
                _tether = EntityView.Create("OrbitTether", 8);
                _tether.transform.SetParent(_root, false);
                _tether.Bind(ConfigManager.FallbackView, ViewShape.Quad, true);
                _tether.Body.enabled = false;
            }

            _tether.SetWorldPosition(_ctx.Run.Player.Pos);
            _tether.ShowRing(new Color(1f, 0.68f, 0.2f, 0.5f), c.Radius);
        }

        void HideTether()
        {
            if (_tether != null)
            {
                _tether.HideRing();
            }
        }

        void SyncLoot()
        {
            List<LootModel> loots = _ctx.Run.Loots;
            float t = Time.unscaledTime;

            for (int i = 0; i < loots.Count; i++)
            {
                LootModel l = loots[i];
                if (l.IsDead)
                {
                    continue;
                }

                _seen.Add(l.Id);

                QualityDef qd = _ctx.Cfg.QualityOf(l.Quality);
                bool gear = l.Kind != LootKind.Coffee;
                EntityView v = Bind(l.Id, KeyLoot, 15, _ctx.Cfg.View(l.ViewId));

                if (gear)
                {
                    v.Body.color = qd.Color;
                }

                v.SetWorldPosition(l.Pos);

                // A still object looks dead. Idle loot breathes and its beam spins.
                if (l.State == LootState.Idle)
                {
                    v.SetBodyOffset(Mathf.Sin(t * 2.2f + l.Id) * 0.07f);
                }
                else
                {
                    v.SetBodyOffset(0f);
                }

                if (gear && qd.Beam != "none")
                {
                    v.ShowBeam(qd.Color, BeamHeight(qd.Beam), BeamWidth(qd.Beam));
                    v.RotateBeam(t * 60f);
                }
                else
                {
                    v.HideBeam();
                }

                if (gear && qd.Label)
                {
                    v.ShowLabel(l.Name, qd.Color);
                }
                else
                {
                    v.HideLabel();
                }
            }
        }

        static float BeamHeight(string beam)
        {
            switch (beam)
            {
                case "thin": return 1.6f;
                case "medium": return 2.6f;
                case "thick": return 4.2f;
                default: return 0f;
            }
        }

        static float BeamWidth(string beam)
        {
            switch (beam)
            {
                case "thin": return 0.12f;
                case "medium": return 0.22f;
                case "thick": return 0.40f;
                default: return 0f;
            }
        }

        EntityView Bind(int id, string poolKey, int sortingOrder, ViewDef def)
        {
            EntityView v;
            if (_bound.TryGetValue(id, out v) && v != null)
            {
                return v;
            }

            GameObject go = _pool.Spawn(poolKey, () => NewView(poolKey, sortingOrder));
            v = go.GetComponent<EntityView>();
            v.ResetDecorations();
            v.Bind(def, ViewShape.Quad, false);
            _bound[id] = v;
            return v;
        }

        GameObject NewView(string poolKey, int sortingOrder)
        {
            EntityView v = EntityView.Create(poolKey, sortingOrder);
            v.transform.SetParent(_root, false);
            return v.gameObject;
        }

        void Prune()
        {
            _stale.Clear();
            foreach (KeyValuePair<int, EntityView> kv in _bound)
            {
                if (!_seen.Contains(kv.Key))
                {
                    _stale.Add(kv.Key);
                }
            }

            for (int i = 0; i < _stale.Count; i++)
            {
                EntityView v;
                if (_bound.TryGetValue(_stale[i], out v) && v != null)
                {
                    v.ResetDecorations();
                    _pool.Recycle(v.gameObject);
                }

                _bound.Remove(_stale[i]);
            }
        }
    }
}
