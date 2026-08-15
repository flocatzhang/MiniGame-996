using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// Fixed muzzle positions for the six weapon slots, on a 0.5 unit circle around the player.
    ///
    /// The order is by symmetry, not clockwise: one weapon sits on the right, two are mirrored left
    /// and right, four form a square, six close the ring. Filling clockwise instead would crowd
    /// three weapons into the right half and read as broken.
    ///
    /// The offsets are world space and never mirror with facing. A muzzle that flipped sides would
    /// teleport projectiles across the body, and since aiming is automatic the side does not matter.
    /// </summary>
    public static class WeaponSlotOffsets
    {
        public const float Radius = 0.5f;

        static readonly Vector2[] Offsets =
        {
            new Vector2(0.50f, 0f),
            new Vector2(-0.50f, 0f),
            new Vector2(0.25f, 0.43f),
            new Vector2(-0.25f, 0.43f),
            new Vector2(0.25f, -0.43f),
            new Vector2(-0.25f, -0.43f),
        };

        public static Vector2 Get(int slot)
        {
            if (slot < 0 || slot >= Offsets.Length)
            {
                return Vector2.zero;
            }

            return Offsets[slot];
        }

        public static Vector2 Muzzle(Vector2 playerPos, int slot)
        {
            return playerPos + Get(slot);
        }
    }
}
