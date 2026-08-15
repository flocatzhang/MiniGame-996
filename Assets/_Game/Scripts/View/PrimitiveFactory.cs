using System.Collections.Generic;
using OfficeHell.Config;
using UnityEngine;

namespace OfficeHell.View
{
    /// <summary>
    /// Generates fallback and effect sprites at runtime. Imported character and environment art
    /// may fail independently without removing the primitive path needed for a playable build.
    /// </summary>
    public static class PrimitiveFactory
    {
        const int Size = 64;

        static readonly Dictionary<ViewShape, Sprite> Cache = new Dictionary<ViewShape, Sprite>(8);
        static Sprite _pixel;
        static Sprite _ring;
        static Sprite _lootBeam;
        static Sprite _lootGlow;
        static Sprite _lootSpark;

        public static Sprite Get(ViewShape shape)
        {
            Sprite s;
            if (Cache.TryGetValue(shape, out s) && s != null)
            {
                return s;
            }

            s = Build(shape);
            Cache[shape] = s;
            return s;
        }

        /// <summary>Solid one pixel sprite, used for bars, beams and the screen flash.</summary>
        public static Sprite Pixel
        {
            get
            {
                if (_pixel == null)
                {
                    Texture2D tex = NewTexture(2, 2);
                    tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                    tex.Apply();
                    _pixel = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
                    _pixel.name = "px";
                }

                return _pixel;
            }
        }

        /// <summary>Hollow circle, used for telegraphs and aoe pulses.</summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring == null)
                {
                    _ring = BuildRing();
                }

                return _ring;
            }
        }

        /// <summary>Soft vertical mask with a bottom pivot, shared by every loot beam layer.</summary>
        public static Sprite LootBeam
        {
            get
            {
                if (_lootBeam == null)
                {
                    _lootBeam = BuildLootBeam();
                }

                return _lootBeam;
            }
        }

        /// <summary>Radial falloff used for the pool-friendly ground glow.</summary>
        public static Sprite LootGlow
        {
            get
            {
                if (_lootGlow == null)
                {
                    _lootGlow = BuildSoftDisc("loot_glow", 64);
                }

                return _lootGlow;
            }
        }

        /// <summary>Small radial falloff used by the single particle system on each loot view.</summary>
        public static Sprite LootSpark
        {
            get
            {
                if (_lootSpark == null)
                {
                    _lootSpark = BuildSoftDisc("loot_spark", 16);
                }

                return _lootSpark;
            }
        }

        static Sprite Build(ViewShape shape)
        {
            Texture2D tex = NewTexture(Size, Size);
            Color32[] pixels = new Color32[Size * Size];
            Color32 opaque = new Color32(255, 255, 255, 255);
            Color32 clear = new Color32(255, 255, 255, 0);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // Normalized to [-1, 1] with a small margin so edges are not clipped.
                    float nx = (x + 0.5f) / Size * 2f - 1f;
                    float ny = (y + 0.5f) / Size * 2f - 1f;
                    pixels[y * Size + x] = Inside(shape, nx, ny) ? opaque : clear;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = "shape_" + shape;
            return sprite;
        }

        static bool Inside(ViewShape shape, float x, float y)
        {
            const float Edge = 0.94f;

            switch (shape)
            {
                case ViewShape.Circle:
                    return x * x + y * y <= Edge * Edge;

                case ViewShape.Diamond:
                    return Mathf.Abs(x) + Mathf.Abs(y) <= Edge;

                case ViewShape.Triangle:
                {
                    // Upward pointing triangle inscribed in the square.
                    float t = (y + Edge) / (2f * Edge);
                    if (t < 0f || t > 1f)
                    {
                        return false;
                    }

                    float halfWidth = Edge * (1f - t);
                    return Mathf.Abs(x) <= halfWidth;
                }

                case ViewShape.Hex:
                {
                    float ax = Mathf.Abs(x);
                    float ay = Mathf.Abs(y);
                    return ax <= Edge * 0.866f && ay <= Edge - ax * 0.577f + Edge * 0.5f && ay <= Edge;
                }

                default:
                    return Mathf.Abs(x) <= Edge && Mathf.Abs(y) <= Edge;
            }
        }

        static Sprite BuildRing()
        {
            Texture2D tex = NewTexture(Size, Size);
            Color32[] pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float nx = (x + 0.5f) / Size * 2f - 1f;
                    float ny = (y + 0.5f) / Size * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    byte a = (byte)(r <= 0.98f && r >= 0.78f ? 255 : 0);
                    pixels[y * Size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            sprite.name = "ring";
            return sprite;
        }

        static Sprite BuildLootBeam()
        {
            Texture2D tex = NewTexture(Size, Size);
            Color32[] pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float v = (y + 0.5f) / Size;
                float bottomFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.08f));
                float topFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((v - 0.52f) / 0.48f));

                for (int x = 0; x < Size; x++)
                {
                    float nx = Mathf.Abs((x + 0.5f) / Size * 2f - 1f);
                    float sideFade = Mathf.Pow(Mathf.Clamp01(1f - nx), 2.4f);
                    byte a = (byte)Mathf.RoundToInt(255f * sideFade * bottomFade * topFade);
                    pixels[y * Size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0f), Size);
            sprite.name = "loot_beam";
            return sprite;
        }

        static Sprite BuildSoftDisc(string name, int size)
        {
            Texture2D tex = NewTexture(size, size);
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    float falloff = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny)), 2f);
                    byte a = (byte)Mathf.RoundToInt(255f * falloff);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = name;
            return sprite;
        }

        static Texture2D NewTexture(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.DontSave;
            return tex;
        }
    }
}
