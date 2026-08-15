using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;

namespace OfficeHell.Systems
{
    /// <summary>Shared handles every system needs. Constructed once by GameApp.</summary>
    public sealed class GameContext
    {
        public ConfigManager Cfg;
        public RunModel Run;
        public EventBus Bus;
        public SpatialGrid Grid;

        /// <summary>Needed by behaviours that summon, such as the boss calling a meeting.</summary>
        public SpawnSystem Spawner;

        /// <summary>
        /// Precise distance test against a grid query result, skipping dead entries.
        /// Returns the index of the closest live enemy or -1.
        /// </summary>
        public int ClosestEnemy(UnityEngine.Vector2 from, float radius, List<int> scratch)
        {
            Grid.QueryCircle(from, radius, scratch);

            int best = -1;
            float bestSqr = radius * radius;
            for (int i = 0; i < scratch.Count; i++)
            {
                int idx = scratch[i];
                if (idx < 0 || idx >= Run.Enemies.Count)
                {
                    continue;
                }

                EnemyModel e = Run.Enemies[idx];
                if (e.IsDead)
                {
                    continue;
                }

                float sqr = (e.Pos - from).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = idx;
                }
            }

            return best;
        }
    }
}
