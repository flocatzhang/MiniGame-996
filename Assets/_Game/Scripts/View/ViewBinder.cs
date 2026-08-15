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
        const string KeyStain = "view.stain";

        /// <summary>
        /// Coffee marks are the one thing drawn here that has no model id, because they live in a
        /// fixed ring on the player rather than in a run entity list. Ids come out of a reserved
        /// negative block so a slot keeps the same view across frames and never collides with
        /// RunModel.NextId, which only ever hands out positives.
        /// </summary>
        const int StainIdBase = -1000;

        /// <summary>
        /// Recoil runs on unscaled time so a hitstop freezes the world but never the reaction that
        /// explains it. Short enough that the next hit on the same enemy always restarts it cleanly.
        /// </summary>
        const float HitReactSeconds = 0.16f;

        /// <summary>
        /// Corpses are the only views that outlive their model, so they need their own ceiling.
        /// A boss phase change kills twenty adds at once and the pool should not spike past that.
        /// </summary>
        const int MaxDeathFx = 96;

        /// <summary>
        /// Slacking, in the same pale warm key as the pulse JuiceService throws on SkillCast so the
        /// two read as one effect. Warm rather than green because it now sweeps instead of sitting,
        /// and a low amplitude pass needs a hue that survives being faint; it stays well clear of the
        /// hit flash by being brief rather than by being a different colour.
        /// </summary>
        static readonly Color SkillTint = new Color(1f, 0.95f, 0.62f);

        /// <summary>One pass of the sweep. Three of them fit inside the base 1.5s of immunity.</summary>
        const float SkillSweepSeconds = 0.5f;

        /// <summary>
        /// Peak of the sweep, reached once per pass and near zero either side of it. This marks a
        /// state rather than announcing one, so it is deliberately under half the old steady tint.
        /// </summary>
        const float SkillSweepPeak = 0.34f;

        /// <summary>
        /// Half the player's drawn height, refreshed each frame so a hot reload or a re-export of the
        /// character art cannot leave the shots hovering. See BodyPlane for what it is for.
        /// </summary>
        float _bodyPlaneY;

        readonly GameContext _ctx;
        readonly PoolService _pool;
        readonly Transform _root;

        readonly Dictionary<int, EntityView> _bound = new Dictionary<int, EntityView>(512);
        readonly List<int> _stale = new List<int>(128);
        readonly HashSet<int> _seen = new HashSet<int>();

        readonly Dictionary<int, HitFx> _hits = new Dictionary<int, HitFx>(128);
        readonly List<DeathFx> _deaths = new List<DeathFx>(64);

        EntityView _playerView;
        EntityView _pickupRing;

        float _playerHitAt = -999f;
        Vector2 _playerHitDir = Vector2.right;

        struct HitFx
        {
            public float StartedAt;
            public Vector2 Dir;
        }

        struct DeathFx
        {
            public EntityView View;
            public float StartedAt;
            public float Duration;
            public Vector2 Dir;
            public float Grow;
            public float Spin;
        }

        public ViewBinder(GameContext ctx, PoolService pool, Transform root)
        {
            _ctx = ctx;
            _pool = pool;
            _root = root;

            _ctx.Bus.Register(EventID.EnemyDamaged, OnEnemyDamaged);
            _ctx.Bus.Register(EventID.EnemyKilled, OnEnemyKilled);
            _ctx.Bus.Register(EventID.PlayerDamaged, OnPlayerDamaged);
        }

        public void Dispose()
        {
            _ctx.Bus.Unregister(EventID.EnemyDamaged, OnEnemyDamaged);
            _ctx.Bus.Unregister(EventID.EnemyKilled, OnEnemyKilled);
            _ctx.Bus.Unregister(EventID.PlayerDamaged, OnPlayerDamaged);
        }

        public void Sync(float unscaledDt)
        {
            _seen.Clear();
            _bodyPlaneY = EntityView.VisualTopOf(_ctx.Cfg.View("v_player")) * 0.5f;

            SyncPlayer();
            SyncEnemies();
            SyncProjectiles();
            SyncSlams();
            SyncTelegraphs();
            SyncStains();
            SyncOrbits();
            SyncLoot();
            Prune();
            TickDeaths();
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

            for (int i = 0; i < _deaths.Count; i++)
            {
                EntityView v = _deaths[i].View;
                if (v != null)
                {
                    v.ResetDecorations();
                    _pool.Recycle(v.gameObject);
                }
            }

            _deaths.Clear();
            _hits.Clear();
            _playerHitAt = -999f;
        }

        // ---------- hit and death reactions ----------

        void OnEnemyDamaged(EvtArg arg)
        {
            EnemyModel e = arg.O0 as EnemyModel;
            if (e == null)
            {
                return;
            }

            HitFx fx;
            fx.StartedAt = Time.unscaledTime;
            fx.Dir = AwayFrom(e.Pos, _ctx.Run.Player.Pos, e.Id);
            _hits[e.Id] = fx;
        }

        void OnPlayerDamaged(EvtArg arg)
        {
            _playerHitAt = Time.unscaledTime;
            _playerHitDir = AwayFrom(arg.P0, arg.P1, 0);
        }

        /// <summary>
        /// The model is gone by the time the next Sync runs, so the view is lifted out of the binding
        /// table here and finishes on its own. Without this an enemy simply stops existing mid frame,
        /// which is what six hundred kills a run currently look like.
        /// </summary>
        void OnEnemyKilled(EvtArg arg)
        {
            _hits.Remove(arg.I0);

            EntityView v;
            if (!_bound.TryGetValue(arg.I0, out v) || v == null)
            {
                return;
            }

            _bound.Remove(arg.I0);

            // The kill lands during Update but the view was last placed in the previous LateUpdate,
            // so take the position off the event rather than leaving the corpse a frame behind.
            v.SetWorldPosition(arg.P0);
            v.HideBar();
            v.HideLabel();
            v.HideRing();
            v.HideBeam();

            if (_deaths.Count >= MaxDeathFx)
            {
                EntityView oldest = _deaths[0].View;
                if (oldest != null)
                {
                    oldest.ResetDecorations();
                    _pool.Recycle(oldest.gameObject);
                }

                _deaths.RemoveAt(0);
            }

            EnemyTier tier = (EnemyTier)arg.I1;

            DeathFx fx;
            fx.View = v;
            fx.StartedAt = Time.unscaledTime;
            fx.Duration = tier == EnemyTier.Boss ? 0.85f : tier == EnemyTier.Elite ? 0.45f : 0.24f;
            fx.Grow = tier == EnemyTier.Boss ? 0.75f : tier == EnemyTier.Elite ? 0.55f : 0.4f;
            fx.Dir = AwayFrom(arg.P0, _ctx.Run.Player.Pos, arg.I0);
            fx.Spin = ((arg.I0 & 1) == 0 ? 1f : -1f) * (tier == EnemyTier.Normal ? 34f : 16f);
            _deaths.Add(fx);
        }

        void TickDeaths()
        {
            float now = Time.unscaledTime;

            for (int i = _deaths.Count - 1; i >= 0; i--)
            {
                DeathFx fx = _deaths[i];
                if (fx.View == null)
                {
                    _deaths.RemoveAt(i);
                    continue;
                }

                float t = (now - fx.StartedAt) / fx.Duration;
                if (t >= 1f)
                {
                    fx.View.ResetDecorations();
                    _pool.Recycle(fx.View.gameObject);
                    _deaths.RemoveAt(i);
                    continue;
                }

                // Ease out, so the burst reads as one impact rather than a slow balloon.
                float ease = 1f - (1f - t) * (1f - t);

                fx.View.SetFlashAmount(1f - Mathf.Clamp01(t * 3f));
                fx.View.SetAlpha(1f - ease);
                fx.View.SetBodyPose(
                    fx.Dir * (0.8f * ease) + new Vector2(0f, 0.3f * ease),
                    1f + fx.Grow * ease,
                    1f + 0.16f * ease,
                    1f - 0.24f * ease,
                    fx.Spin * ease);
            }
        }

        /// <summary>Zero length happens when an enemy dies standing on the player; pick a stable angle.</summary>
        static Vector2 AwayFrom(Vector2 pos, Vector2 source, int seed)
        {
            Vector2 delta = pos - source;
            float length = delta.magnitude;
            if (length > 0.0001f)
            {
                return delta / length;
            }

            float angle = seed * 137.5f * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
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

            float unscaledNow = Time.unscaledTime;
            float flash = 0f;
            float squashX = 1f;
            float squashY = 1f;
            Vector2 offset = Vector2.zero;

            float hit = 1f - (unscaledNow - _playerHitAt) / HitReactSeconds;
            if (hit > 0f)
            {
                float snap = hit * hit;
                flash = hit * 0.75f;
                offset = _playerHitDir * (0.18f * snap);
                squashX = 1f + 0.22f * snap;
                squashY = 1f - 0.16f * snap;
            }

            float logicalNow = GameClock.Now;

            // Only the i-frames a hit grants are worth blinking about, so this deliberately does not
            // ask IsInvulnerable: that is also true while slacking off and permanently true in god
            // mode, and a red strobe over a reward reads as "you are being hurt".
            if (logicalNow < p.InvulnUntil)
            {
                flash = Mathf.Max(flash, Mathf.Repeat(unscaledNow * 9f, 1f) > 0.5f ? 0.5f : 0.08f);
            }

            // Slacking gets a pass of light instead of a coat of paint. Held steady it read as the
            // character having been recoloured, which is something that happened to the sprite rather
            // than something happening to the player, and at 0.55 of a saturated green it was also the
            // loudest thing on screen during the one second and a half the player is safe. Sweeping
            // keeps the state legible while spending most of each cycle near zero. The fade over the
            // last quarter second is still the only warning that the immunity is about to end.
            float slackLeft = p.SkillInvulnUntil - logicalNow;
            float sweep = 0f;
            if (slackLeft > 0f)
            {
                float phase = Mathf.Repeat(unscaledNow, SkillSweepSeconds) / SkillSweepSeconds;
                sweep = Mathf.Sin(phase * Mathf.PI) * SkillSweepPeak * Mathf.Min(1f, slackLeft * 4f);
            }

            _playerView.SetTint(SkillTint, sweep);

            _playerView.SetFlashAmount(flash);
            _playerView.SetBodyPose(offset, 1f, squashX, squashY, 0f);

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
            float unscaledNow = Time.unscaledTime;

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

                float flash = 0f;
                float squashX = 1f;
                float squashY = 1f;
                Vector2 offset = Vector2.zero;

                // The model side flash is a 0.06s boolean. Recoil decays instead, so a single hit and
                // a burst of six hits look different, which is most of what "did that land" means here.
                HitFx fx;
                if (_hits.TryGetValue(e.Id, out fx))
                {
                    float hit = 1f - (unscaledNow - fx.StartedAt) / HitReactSeconds;
                    if (hit <= 0f)
                    {
                        _hits.Remove(e.Id);
                    }
                    else
                    {
                        float snap = hit * hit;
                        flash = hit;
                        offset = fx.Dir * (0.2f * snap);
                        squashX = 1f + 0.24f * snap;
                        squashY = 1f - 0.18f * snap;
                    }
                }

                // Boss phase invulnerability lasts two seconds. A steady tint would read as a broken
                // sprite, a blink reads as "not now".
                if (now < e.InvulnUntil)
                {
                    flash = Mathf.Max(flash, Mathf.Repeat(unscaledNow * 10f, 1f) > 0.5f ? 0.75f : 0.15f);
                }

                v.SetFlashAmount(flash);

                // The grace window has to be legible, otherwise a split BUG that cannot hurt you yet
                // looks identical to one that can and the player backs off for no reason.
                float scaleMultiplier = 1f;
                float alpha = 1f;
                if (!e.CanTouch(now))
                {
                    float t = Mathf.InverseLerp(e.SpawnedAt, e.ContactArmedAt, now);
                    alpha = Mathf.Lerp(0.45f, 1f, t);
                    scaleMultiplier = Mathf.Lerp(0.6f, 1f, t);
                }

                v.SetAlpha(alpha);
                v.SetBodyPose(offset, scaleMultiplier, squashX, squashY, 0f);

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

        /// <summary>
        /// Everything is solved on the floor plane, including the six muzzle offsets, which is correct
        /// top down geometry and completely wrong to look at: the two side slots sit level with the
        /// player's feet and the two lower ones start below them, so half the weapons appear to shoot
        /// out of the floor. Lifting is done at draw time by one body height rather than by moving the
        /// muzzles, so every range check, hit test and knockback direction stays in the plane the whole
        /// game is tuned in. Enemy sprites are drawn from their feet up too, so a shot crossing at this
        /// height passes through their middle.
        /// </summary>
        void SyncProjectiles()
        {
            List<ProjectileModel> projectiles = _ctx.Run.Projectiles;
            Vector2 lift = new Vector2(0f, _bodyPlaneY);

            for (int i = 0; i < projectiles.Count; i++)
            {
                ProjectileModel p = projectiles[i];
                if (p.IsDead)
                {
                    continue;
                }

                _seen.Add(p.Id);
                EntityView v = Bind(p.Id, KeyProjectile, 30, _ctx.Cfg.View(p.ViewId));
                v.SetWorldPosition(p.Pos + lift);

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

                // Leaves the hand rather than the floor, and the lift is gone by the time it lands
                // because the blast itself belongs on the ground.
                Vector2 from = s.From + new Vector2(0f, _bodyPlaneY);
                Vector2 air = Vector2.Lerp(from, s.Target + Vector2.up * 1.2f, t);
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
                ViewDef def = _ctx.Cfg.View(w.ViewId);
                EntityView v = Bind(w.Id, KeyWarn, 6, def);
                v.SetWorldPosition(w.Pos);
                v.Body.enabled = false;

                // Views.xml spends four rows distinguishing these markers and red is reserved there for
                // "this is about to hurt you". The elite entrance ring deals no damage, so painting every
                // telegraph red told the player to run from a landing that costs nothing.
                Color ring = def != null ? def.Color : new Color(1f, 0.3f, 0.25f);
                ring.a = 0.25f + 0.45f * t;
                v.ShowRing(ring, w.Radius * Mathf.Lerp(0.6f, 1f, t));
            }
        }

        /// <summary>
        /// Purple slipper. The mark has to be on the floor or the slow is the game cheating: the same
        /// rule the aura rings exist for. Fades out over its life so the player can read which end of
        /// the trail is still working.
        /// </summary>
        void SyncStains()
        {
            PlayerModel p = _ctx.Run.Player;
            float now = GameClock.Now;
            ViewDef def = _ctx.Cfg.View("v_stain");

            for (int i = 0; i < PlayerModel.StainSlots; i++)
            {
                float until = p.StainUntil(i);
                float left = until - now;
                if (left <= 0f)
                {
                    continue;
                }

                int id = StainIdBase - i;
                _seen.Add(id);

                EntityView v = Bind(id, KeyStain, 4, def);
                v.SetWorldPosition(p.StainPos(i));
                v.Body.enabled = false;

                Color ring = def != null ? def.Color : new Color(0.42f, 0.27f, 0.16f);
                ring.a = 0.42f * Mathf.Clamp01(left * 0.8f);
                v.ShowRing(ring, 0.9f);
            }
        }

        void SyncOrbits()
        {
            List<OrbitCardModel> cards = _ctx.Run.OrbitCards;
            Vector2 lift = new Vector2(0f, _bodyPlaneY);

            for (int i = 0; i < cards.Count; i++)
            {
                OrbitCardModel c = cards[i];
                _seen.Add(c.Id);

                EntityView v = Bind(c.Id, KeyOrbit, 32, _ctx.Cfg.View(c.ViewId));
                v.SetWorldPosition(c.Pos + lift);
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

            _tether.SetWorldPosition(_ctx.Run.Player.Pos + new Vector2(0f, _bodyPlaneY));
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
                    v.SetBaseColor(qd.Color);
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
                    v.ShowLootBeam(l.Quality, qd.Color, t, l.Id);
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

                // Clearing the field at day end kills enemies without a kill event, so a recoil that
                // was still running has no other chance to retire itself.
                _hits.Remove(_stale[i]);
            }
        }
    }
}
