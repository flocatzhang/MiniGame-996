using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// Picks spawn points on an ellipse that follows the player.
    ///
    /// Ellipse rather than circle because the screen is 16:9. On a circular band the left and right
    /// points sit five units further from the frustum than the top and bottom ones, and the player
    /// reads that as "the side mobs are late while the top ones appear out of nowhere".
    ///
    /// The shortest semi axis is the protection radius, so nothing can ever spawn in the player's lap.
    /// </summary>
    public sealed class SpawnBand
    {
        readonly GameContext _ctx;
        readonly List<int> _grid = new List<int>(64);
        readonly List<Vector2> _burst = new List<Vector2>(16);
        readonly List<int> _sectors = new List<int>(8);
        readonly List<float> _weights = new List<float>(32);

        public SpawnBand(GameContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>Call once per group so the same sector is never used twice inside one burst.</summary>
        public void BeginBurst()
        {
            _burst.Clear();
            _sectors.Clear();
        }

        public Vector2 NextPoint(Vector2 center)
        {
            SpawnBandDef band = _ctx.Cfg.Band;
            ArenaDef arena = _ctx.Cfg.Arena;

            int sector = PickSector(band);
            Vector2 point;

            for (int attempt = 0; attempt < band.Retries; attempt++)
            {
                point = Sample(center, band, sector);
                if (InsideArena(point, arena, band) && FarEnough(point, band))
                {
                    _burst.Add(point);
                    return point;
                }
            }

            // Retries exhausted. Falling back to the nearest usable sector rather than skipping is the
            // whole point: "spawning failed, never mind" teaches the player that hugging a wall makes
            // enemies stop coming, and that hole gets found and exploited within one playthrough.
            int fallback = NearestUsableSector(center, band, arena, sector);
            for (int attempt = 0; attempt < band.Retries; attempt++)
            {
                point = Sample(center, band, fallback);
                if (InsideArena(point, arena, band))
                {
                    _burst.Add(point);
                    return point;
                }
            }

            point = Clamp(Sample(center, band, fallback), arena, band);
            _burst.Add(point);
            return point;
        }

        /// <summary>
        /// Side weights beat top and bottom weights because the widescreen field is 21 units across
        /// and only 12 tall: an enemy entering from above is six units from the player's face.
        /// </summary>
        int PickSector(SpawnBandDef band)
        {
            _weights.Clear();
            for (int i = 0; i < band.Sectors; i++)
            {
                _weights.Add(_sectors.Contains(i) ? 0f : SectorWeight(band, i));
            }

            int pick = Rng.WeightedPick(_weights);
            if (pick < 0)
            {
                // Every sector already used by this burst, which only happens when the group is
                // larger than the sector count. Reuse is fine at that point.
                _sectors.Clear();
                pick = Random.Range(0, band.Sectors);
            }

            _sectors.Add(pick);
            return pick;
        }

        static float SectorWeight(SpawnBandDef band, int sector)
        {
            float angle = SectorCenterRad(band, sector);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            if (Mathf.Abs(cos) >= Mathf.Abs(sin))
            {
                return cos >= 0f ? band.WeightRight : band.WeightLeft;
            }

            return sin >= 0f ? band.WeightUp : band.WeightDown;
        }

        static float SectorCenterRad(SpawnBandDef band, int sector)
        {
            float step = Mathf.PI * 2f / band.Sectors;
            return step * (sector + 0.5f);
        }

        Vector2 Sample(Vector2 center, SpawnBandDef band, int sector)
        {
            float step = Mathf.PI * 2f / band.Sectors;
            float angle = step * sector + Random.value * step;

            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            Vector2 onBand = new Vector2(band.SemiX * cos, band.SemiY * sin);

            // Outward push along the ellipse normal, so several enemies from the same sector do not
            // line up in a single row at exactly the same distance.
            Vector2 normal = new Vector2(cos / Mathf.Max(0.01f, band.SemiX), sin / Mathf.Max(0.01f, band.SemiY));
            if (normal.sqrMagnitude > 0.0001f)
            {
                onBand += normal.normalized * Random.Range(0f, band.OutwardPush);
            }

            return center + onBand;
        }

        static bool InsideArena(Vector2 p, ArenaDef arena, SpawnBandDef band)
        {
            return Mathf.Abs(p.x) <= arena.HalfWidth - band.EdgeMargin &&
                   Mathf.Abs(p.y) <= arena.HalfHeight - band.EdgeMargin;
        }

        static Vector2 Clamp(Vector2 p, ArenaDef arena, SpawnBandDef band)
        {
            p.x = Mathf.Clamp(p.x, -(arena.HalfWidth - band.EdgeMargin), arena.HalfWidth - band.EdgeMargin);
            p.y = Mathf.Clamp(p.y, -(arena.HalfHeight - band.EdgeMargin), arena.HalfHeight - band.EdgeMargin);
            return p;
        }

        bool FarEnough(Vector2 p, SpawnBandDef band)
        {
            float min = band.MinSeparation;
            if (min <= 0f)
            {
                return true;
            }

            float minSqr = min * min;

            for (int i = 0; i < _burst.Count; i++)
            {
                if ((_burst[i] - p).sqrMagnitude < minSqr)
                {
                    return false;
                }
            }

            // The grid was built last frame, which is why the burst list above exists as well.
            _ctx.Grid.QueryCircle(p, min, _grid);
            for (int i = 0; i < _grid.Count; i++)
            {
                int idx = _grid[i];
                if (idx < 0 || idx >= _ctx.Run.Enemies.Count)
                {
                    continue;
                }

                Model.EnemyModel e = _ctx.Run.Enemies[idx];
                if (!e.IsDead && (e.Pos - p).sqrMagnitude < minSqr)
                {
                    return false;
                }
            }

            return true;
        }

        int NearestUsableSector(Vector2 center, SpawnBandDef band, ArenaDef arena, int from)
        {
            for (int step = 1; step <= band.Sectors / 2; step++)
            {
                int a = ((from + step) % band.Sectors + band.Sectors) % band.Sectors;
                if (SectorUsable(center, band, arena, a))
                {
                    return a;
                }

                int b = ((from - step) % band.Sectors + band.Sectors) % band.Sectors;
                if (SectorUsable(center, band, arena, b))
                {
                    return b;
                }
            }

            return from;
        }

        static bool SectorUsable(Vector2 center, SpawnBandDef band, ArenaDef arena, int sector)
        {
            float angle = SectorCenterRad(band, sector);
            Vector2 p = center + new Vector2(band.SemiX * Mathf.Cos(angle), band.SemiY * Mathf.Sin(angle));
            return InsideArena(p, arena, band);
        }
    }
}
