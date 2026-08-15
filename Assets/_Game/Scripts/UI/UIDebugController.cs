using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using OfficeHell.Systems;
using UnityEngine;

namespace OfficeHell.UI
{
    /// <summary>
    /// Validation panel, drawn with IMGUI on purpose: a dev tool that has to survive constant
    /// churn is cheaper as code than as a ugui hierarchy, and it never pollutes the shipping canvas.
    /// Toggle with F1.
    /// </summary>
    public sealed class UIDebugController
    {
        readonly UIContext _ctx;

        public bool Visible;

        Rect _window = new Rect(20f, 20f, 460f, 700f);
        int _weaponPage;
        int _slotCursor;
        GUIStyle _label;

        public UIDebugController(UIContext ctx)
        {
            _ctx = ctx;
        }

        public void DrawGui()
        {
            if (!Visible)
            {
                return;
            }

            if (GUI.skin != null && FontProviderFont != null)
            {
                GUI.skin.font = FontProviderFont;
            }

            _window = GUILayout.Window(9271, _window, DrawWindow, "OFFICE HELL · 调试面板 (F1)");
        }

        static Font FontProviderFont
        {
            get { return View.FontProvider.Font; }
        }

        void DrawWindow(int id)
        {
            GameContext game = _ctx.Game;
            RunModel run = game.Run;
            PlayerModel p = run.Player;

            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label);
                _label.wordWrap = false;
            }

            GUILayout.Label(string.Format("状态 {0}   第 {1} 天 {2}   {3:0.0}/{4:0.0}s",
                _ctx.Driver.Flow.State, run.DayIndex,
                run.Day != null ? run.Day.Weekday : "-",
                run.DayElapsed,
                run.Day != null ? run.Day.Duration : 0f), _label);

            GUILayout.Label(string.Format("敌 {0}   弹 {1}   掉落 {2}   欠账 {3}   FPS {4:0}",
                run.Enemies.Count, run.Projectiles.Count, run.Loots.Count, run.SpawnDebt,
                1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime)), _label);

            GUILayout.Label(string.Format("KPI {0}%   击杀 {1}   今日 {2}/{3}",
                run.Kpi(game.Cfg.Progression), run.Kills, run.KilledToday, run.SpawnedToday), _label);

            GUILayout.Space(6f);

            GUILayout.Label("时间缩放  " + GameClock.DebugScale.ToString("0.00"), _label);
            GameClock.DebugScale = GUILayout.HorizontalSlider(GameClock.DebugScale, 0f, 3f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("0.25x"))
            {
                GameClock.DebugScale = 0.25f;
            }

            if (GUILayout.Button("1x"))
            {
                GameClock.DebugScale = 1f;
            }

            if (GUILayout.Button("2x"))
            {
                GameClock.DebugScale = 2f;
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            p.GodMode = GUILayout.Toggle(p.GodMode, "无敌 (God Mode)");
            _ctx.Audio.Muted = GUILayout.Toggle(_ctx.Audio.Muted, "静音");

            GUILayout.Space(6f);
            GUILayout.Label("工作日", _label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("下班"))
            {
                _ctx.Driver.Flow.DebugSkipDay();
            }

            if (GUILayout.Button("前一天"))
            {
                _ctx.Driver.Flow.DebugJumpToDay(run.DayIndex - 1);
            }

            if (GUILayout.Button("后一天"))
            {
                _ctx.Driver.Flow.DebugJumpToDay(run.DayIndex + 1);
            }

            if (GUILayout.Button("周六"))
            {
                _ctx.Driver.Flow.DebugJumpToDay(game.Cfg.DayCount);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("强制掉落", _label);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 4; i++)
            {
                Quality q = (Quality)i;
                if (GUILayout.Button(QualityLabel(q) + "武"))
                {
                    _ctx.Driver.Loot.SpawnWeapon(p.Pos, q);
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for (int i = 0; i < 4; i++)
            {
                Quality q = (Quality)i;
                if (GUILayout.Button(QualityLabel(q) + "防"))
                {
                    _ctx.Driver.Loot.SpawnArmor(p.Pos, q);
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("咖啡"))
            {
                _ctx.Driver.Loot.SpawnCoffee(p.Pos);
            }

            if (GUILayout.Button("经验 +50"))
            {
                _ctx.Driver.Progression.AddExp(50);
            }

            if (GUILayout.Button("升级卡"))
            {
                p.PendingLevelUps++;
            }

            if (GUILayout.Button("清屏"))
            {
                for (int i = 0; i < run.Enemies.Count; i++)
                {
                    CombatSystem.KillEnemy(game, run.Enemies[i]);
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            DrawWeaponSection(game, p);

            GUILayout.Space(6f);
            DrawArmorSection(p);

            GUILayout.Space(6f);
            DrawSpawnSection(game, p);

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重载配置 (F5)"))
            {
                GameApp.Instance.ReloadConfig();
            }

            if (GUILayout.Button("重开 (R)"))
            {
                GameApp.Instance.StartRun();
            }

            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        void DrawWeaponSection(GameContext game, PlayerModel p)
        {
            GUILayout.Label("武器槽  " + p.EquippedCount() + " / " + PlayerModel.WeaponSlots +
                            "   目标槽位 " + _slotCursor, _label);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < PlayerModel.WeaponSlots; i++)
            {
                WeaponRuntime rt = p.Weapons[i];
                string text = rt.IsEmpty ? "空" : rt.Def.Name;
                bool selected = _slotCursor == i;
                if (GUILayout.Toggle(selected, text) && !selected)
                {
                    _slotCursor = i;
                }
            }

            GUILayout.EndHorizontal();

            int count = game.Cfg.WeaponOrder.Count;
            if (count == 0)
            {
                GUILayout.Label("Weapons.xml 没有可用武器", _label);
                return;
            }

            const int perPage = 4;
            int pages = Mathf.Max(1, (count + perPage - 1) / perPage);
            _weaponPage = Mathf.Clamp(_weaponPage, 0, pages - 1);

            GUILayout.BeginHorizontal();
            for (int i = _weaponPage * perPage; i < Mathf.Min(count, (_weaponPage + 1) * perPage); i++)
            {
                WeaponDef def = game.Cfg.Weapon(game.Cfg.WeaponOrder[i]);
                if (def == null)
                {
                    continue;
                }

                if (GUILayout.Button(def.Name))
                {
                    p.Equip(_slotCursor, def, Quality.White);
                    _slotCursor = (_slotCursor + 1) % PlayerModel.WeaponSlots;
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<"))
            {
                _weaponPage = (_weaponPage - 1 + pages) % pages;
            }

            if (GUILayout.Button("清空槽位"))
            {
                p.Weapons[_slotCursor].Clear();
            }

            if (GUILayout.Button("品质升一级"))
            {
                WeaponRuntime rt = p.Weapons[_slotCursor];
                if (!rt.IsEmpty)
                {
                    rt.Quality = (Quality)Mathf.Min((int)Quality.Orange, (int)rt.Quality + 1);
                }
            }

            if (GUILayout.Button(">"))
            {
                _weaponPage = (_weaponPage + 1) % pages;
            }

            GUILayout.EndHorizontal();
        }

        void DrawArmorSection(PlayerModel p)
        {
            GUILayout.Label("防具  " + p.ArmorCount() + " / " + PlayerModel.ArmorSlots, _label);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < PlayerModel.ArmorSlots; i++)
            {
                ArmorRuntime rt = p.Armors[i];
                GUILayout.Label(rt.IsEmpty ? "-" : rt.Def.Name + " " + QualityLabel(rt.Quality), _label);
            }

            GUILayout.EndHorizontal();
        }

        void DrawSpawnSection(GameContext game, PlayerModel p)
        {
            GUILayout.Label("生成敌人", _label);

            int drawn = 0;
            GUILayout.BeginHorizontal();
            foreach (System.Collections.Generic.KeyValuePair<string, EnemyDef> kv in game.Cfg.Enemies)
            {
                if (drawn > 0 && drawn % 3 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }

                if (GUILayout.Button(kv.Value.Name))
                {
                    Vector2 pos = Rng.RingPoint(p.Pos, 4f, 6f);
                    _ctx.Driver.Spawn.Spawn(kv.Value, pos, null);
                }

                drawn++;
            }

            GUILayout.EndHorizontal();
        }

        static string QualityLabel(Quality q)
        {
            switch (q)
            {
                case Quality.Blue: return "蓝";
                case Quality.Yellow: return "黄";
                case Quality.Orange: return "橙";
                default: return "白";
            }
        }
    }
}
