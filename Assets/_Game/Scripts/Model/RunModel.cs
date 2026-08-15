using System.Collections.Generic;
using OfficeHell.Config;
using UnityEngine;

namespace OfficeHell.Model
{
    /// <summary>How the run ended. Two of the three are wins, which is deliberate.</summary>
    public enum Ending
    {
        None = 0,
        Clear = 1,
        ClearTimeout = 2,
        Fail = 3,
    }

    /// <summary>
    /// Everything that belongs to one run. Restart clears this object and recycles the pools,
    /// which is why the scene never needs to be reloaded.
    /// </summary>
    public sealed class RunModel
    {
        public readonly PlayerModel Player = new PlayerModel();

        public readonly List<EnemyModel> Enemies = new List<EnemyModel>(256);
        public readonly List<ProjectileModel> Projectiles = new List<ProjectileModel>(256);
        public readonly List<LootModel> Loots = new List<LootModel>(128);
        public readonly List<SlamModel> Slams = new List<SlamModel>(32);
        public readonly List<TelegraphModel> Telegraphs = new List<TelegraphModel>(32);
        public readonly List<OrbitCardModel> OrbitCards = new List<OrbitCardModel>(24);

        readonly Stack<EnemyModel> _enemyPool = new Stack<EnemyModel>(256);
        readonly Stack<ProjectileModel> _projPool = new Stack<ProjectileModel>(256);
        readonly Stack<LootModel> _lootPool = new Stack<LootModel>(128);
        readonly Stack<SlamModel> _slamPool = new Stack<SlamModel>(32);
        readonly Stack<TelegraphModel> _telePool = new Stack<TelegraphModel>(32);

        public int DayIndex = 1;
        public float DayElapsed;
        public DayDef Day;
        public float HpScale = 1f;
        public float DmgScale = 1f;

        public int SpawnedToday;
        public int KilledToday;

        /// <summary>Enemies the concurrent cap made us skip. Never discarded, only deferred.</summary>
        public int SpawnDebt;

        public int Kills;
        public float CombatSeconds;

        /// <summary>Per def id, so the end of day report can say "处理邮件 328 封" without a second pass.</summary>
        public readonly Dictionary<string, int> KillsByType = new Dictionary<string, int>(12);

        /// <summary>Only accumulates while actually fighting, the off work screen does not count.</summary>
        public float SecondsSinceLastLegendary;

        public bool AnyLegendaryDropped;

        public Quality BestQuality = Quality.White;
        public string BestLootName = "-";
        public bool AnyLootPicked;

        public Ending Ending = Ending.None;
        public bool BossDefeated;
        public int BossBarsLeft;
        public int BossBarsTotal;

        int _nextId = 1;
        int _nextSourceId = 1000;

        public float DayProgress01
        {
            get { return Day == null ? 0f : Mathf.Clamp01(DayElapsed / Day.Duration); }
        }

        public int Kpi(ProgressionDef prog)
        {
            return CombatFormula.KpiPercent(Kills, prog);
        }

        public int AliveEnemies
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Enemies.Count; i++)
                {
                    if (!Enemies[i].IsDead)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public EnemyModel Boss
        {
            get
            {
                for (int i = 0; i < Enemies.Count; i++)
                {
                    if (!Enemies[i].IsDead && Enemies[i].IsBoss)
                    {
                        return Enemies[i];
                    }
                }

                return null;
            }
        }

        public int NextId()
        {
            return _nextId++;
        }

        public int NextSourceId()
        {
            return _nextSourceId++;
        }

        public void CountKill(string defId)
        {
            Kills++;
            KilledToday++;
            if (string.IsNullOrEmpty(defId))
            {
                return;
            }

            int n;
            KillsByType.TryGetValue(defId, out n);
            KillsByType[defId] = n + 1;
        }

        public void ResetRun(ConfigManager cfg)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemies[i].Reset();
                _enemyPool.Push(Enemies[i]);
            }

            Enemies.Clear();

            for (int i = 0; i < Projectiles.Count; i++)
            {
                Projectiles[i].Reset();
                _projPool.Push(Projectiles[i]);
            }

            Projectiles.Clear();

            for (int i = 0; i < Loots.Count; i++)
            {
                Loots[i].Reset();
                _lootPool.Push(Loots[i]);
            }

            Loots.Clear();

            for (int i = 0; i < Slams.Count; i++)
            {
                Slams[i].Reset();
                _slamPool.Push(Slams[i]);
            }

            Slams.Clear();

            for (int i = 0; i < Telegraphs.Count; i++)
            {
                Telegraphs[i].Reset();
                _telePool.Push(Telegraphs[i]);
            }

            Telegraphs.Clear();
            OrbitCards.Clear();

            Player.ResetFrom(cfg.Player, cfg.Progression);

            DayIndex = 1;
            DayElapsed = 0f;
            Day = cfg.Day(1);
            HpScale = cfg.HpScale(1);
            DmgScale = cfg.DmgScale(1);
            SpawnedToday = 0;
            KilledToday = 0;
            SpawnDebt = 0;
            Kills = 0;
            KillsByType.Clear();
            CombatSeconds = 0f;
            SecondsSinceLastLegendary = 0f;
            AnyLegendaryDropped = false;
            BestQuality = Quality.White;
            BestLootName = "-";
            AnyLootPicked = false;
            Ending = Ending.None;
            BossDefeated = false;
            BossBarsLeft = 0;
            BossBarsTotal = 0;
            _nextId = 1;
            _nextSourceId = 1000;
        }

        public void BeginDay(int index, ConfigManager cfg)
        {
            DayIndex = index;
            DayElapsed = 0f;
            Day = cfg.Day(index);
            HpScale = cfg.HpScale(index);
            DmgScale = cfg.DmgScale(index);
            SpawnedToday = 0;
            KilledToday = 0;
            SpawnDebt = 0;

            // The orange headphone promises one save per day, so the day boundary is where it refills.
            Player.DeathSaveReady = Player.QualityOf(EquipSlot.Head) >= Quality.Orange;
        }

        // ---------- entity allocation ----------

        public EnemyModel RentEnemy()
        {
            EnemyModel e = _enemyPool.Count > 0 ? _enemyPool.Pop() : new EnemyModel();
            e.Reset();
            e.Id = NextId();
            Enemies.Add(e);
            return e;
        }

        public ProjectileModel RentProjectile()
        {
            ProjectileModel p = _projPool.Count > 0 ? _projPool.Pop() : new ProjectileModel();
            p.Reset();
            p.Id = NextId();
            Projectiles.Add(p);
            return p;
        }

        public LootModel RentLoot()
        {
            LootModel l = _lootPool.Count > 0 ? _lootPool.Pop() : new LootModel();
            l.Reset();
            l.Id = NextId();
            Loots.Add(l);
            return l;
        }

        public SlamModel RentSlam()
        {
            SlamModel s = _slamPool.Count > 0 ? _slamPool.Pop() : new SlamModel();
            s.Reset();
            s.Id = NextId();
            Slams.Add(s);
            return s;
        }

        public TelegraphModel RentTelegraph()
        {
            TelegraphModel t = _telePool.Count > 0 ? _telePool.Pop() : new TelegraphModel();
            t.Reset();
            t.Id = NextId();
            Telegraphs.Add(t);
            return t;
        }

        public EnemyModel FindEnemyById(int id)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                if (Enemies[i].Id == id)
                {
                    return Enemies[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Runs at the tail of the frame, immediately before the spatial grid is rebuilt.
        /// Nothing is removed mid frame, so indices handed out by a query stay valid for that frame.
        /// </summary>
        public void Compact()
        {
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                if (Enemies[i].IsDead)
                {
                    EnemyModel e = Enemies[i];
                    Enemies.RemoveAt(i);
                    e.Reset();
                    _enemyPool.Push(e);
                }
            }

            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                if (Projectiles[i].IsDead)
                {
                    ProjectileModel p = Projectiles[i];
                    Projectiles.RemoveAt(i);
                    p.Reset();
                    _projPool.Push(p);
                }
            }

            for (int i = Loots.Count - 1; i >= 0; i--)
            {
                if (Loots[i].IsDead)
                {
                    LootModel l = Loots[i];
                    Loots.RemoveAt(i);
                    l.Reset();
                    _lootPool.Push(l);
                }
            }

            for (int i = Slams.Count - 1; i >= 0; i--)
            {
                if (Slams[i].IsDead)
                {
                    SlamModel s = Slams[i];
                    Slams.RemoveAt(i);
                    s.Reset();
                    _slamPool.Push(s);
                }
            }

            for (int i = Telegraphs.Count - 1; i >= 0; i--)
            {
                if (Telegraphs[i].IsDead)
                {
                    TelegraphModel t = Telegraphs[i];
                    Telegraphs.RemoveAt(i);
                    t.Reset();
                    _telePool.Push(t);
                }
            }
        }

        /// <summary>
        /// After a hot reload the def objects are new instances, so every live entity has to
        /// re-resolve its def by id or it would keep running on the old numbers.
        /// </summary>
        public void RebindDefs(ConfigManager cfg)
        {
            for (int i = 0; i < Enemies.Count; i++)
            {
                EnemyDef d = cfg.Enemy(Enemies[i].DefId);
                if (d != null)
                {
                    Enemies[i].Def = d;
                }
            }

            for (int i = 0; i < PlayerModel.WeaponSlots; i++)
            {
                WeaponRuntime rt = Player.Weapons[i];
                if (rt.DefId == null)
                {
                    continue;
                }

                WeaponDef d = cfg.Weapon(rt.DefId);
                if (d != null)
                {
                    rt.Def = d;
                }
                else
                {
                    rt.Clear();
                }
            }

            for (int i = 0; i < PlayerModel.ArmorSlots; i++)
            {
                ArmorRuntime rt = Player.Armors[i];
                if (rt.DefId == null)
                {
                    continue;
                }

                ArmorBaseDef d = cfg.Armor(rt.DefId);
                if (d != null)
                {
                    rt.Def = d;
                }
                else
                {
                    rt.Clear();
                }
            }

            Day = cfg.Day(DayIndex);
            HpScale = cfg.HpScale(DayIndex);
            DmgScale = cfg.DmgScale(DayIndex);
        }
    }
}
