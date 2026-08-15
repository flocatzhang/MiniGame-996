using System.Collections.Generic;
using UnityEngine;

namespace OfficeHell.Core
{
    /// <summary>Random helpers shared by spawning and loot rolling.</summary>
    public static class Rng
    {
        public static float Range(float min, float max)
        {
            return Random.Range(min, max);
        }

        public static bool Chance01(float probability)
        {
            return Random.value < probability;
        }

        public static bool ChancePercent(float percent)
        {
            return Random.value * 100f < percent;
        }

        /// <summary>
        /// Area uniform point inside an annulus. Lerping the radius directly packs the inner ring,
        /// so the radius has to come out of the squared range.
        /// </summary>
        public static Vector2 RingPoint(Vector2 center, float minRadius, float maxRadius)
        {
            float angle = Random.value * Mathf.PI * 2f;
            return center + AtRadius(angle, minRadius, maxRadius);
        }

        /// <summary>Area uniform point inside an arc slice, used for the "flood in from one side" feel.</summary>
        public static Vector2 ArcPoint(Vector2 center, float minRadius, float maxRadius, float arcCenterRad, float arcDegree)
        {
            float half = arcDegree * 0.5f * Mathf.Deg2Rad;
            float angle = arcCenterRad + Random.Range(-half, half);
            return center + AtRadius(angle, minRadius, maxRadius);
        }

        static Vector2 AtRadius(float angleRad, float minRadius, float maxRadius)
        {
            float r = Mathf.Sqrt(Mathf.Lerp(minRadius * minRadius, maxRadius * maxRadius, Random.value));
            return new Vector2(Mathf.Cos(angleRad) * r, Mathf.Sin(angleRad) * r);
        }

        /// <summary>Returns the index of the picked entry, or -1 when the total weight is not positive.</summary>
        public static int WeightedPick(List<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] > 0f)
                {
                    total += weights[i];
                }
            }

            if (total <= 0f)
            {
                return -1;
            }

            float roll = Random.value * total;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f)
                {
                    continue;
                }

                roll -= weights[i];
                if (roll <= 0f)
                {
                    return i;
                }
            }

            return weights.Count - 1;
        }
    }
}
