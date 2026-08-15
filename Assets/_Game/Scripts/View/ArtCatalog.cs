using System;
using System.Collections.Generic;
using UnityEngine;

namespace OfficeHell.View
{
    /// <summary>
    /// Central resource lookup for shipped art. Every caller keeps a primitive fallback, so a bad
    /// import is visible but can never stop a run from booting.
    /// </summary>
    public static class ArtCatalog
    {
        const string Root = "OfficeHellArt/";
        const string Characters = Root + "Characters/";

        static readonly Dictionary<string, Sprite[]> FrameCache = new Dictionary<string, Sprite[]>(12);
        static readonly Sprite[] EmptyFrames = new Sprite[0];

        static Sprite _map;
        static Sprite _logo;
        static Sprite _pie;

        public static Sprite Map
        {
            get
            {
                if (_map == null)
                {
                    _map = Resources.Load<Sprite>(Root + "Environment/OfficeMap");
                }

                return _map;
            }
        }

        public static Sprite Logo
        {
            get
            {
                if (_logo == null)
                {
                    _logo = Resources.Load<Sprite>(Root + "Branding/LogoMain");
                }

                return _logo;
            }
        }

        public static Sprite Pie
        {
            get
            {
                if (_pie == null)
                {
                    _pie = Resources.Load<Sprite>(Root + "Effects/Pie");
                }

                return _pie;
            }
        }

        public static Sprite[] Frames(string spriteSet)
        {
            if (string.IsNullOrEmpty(spriteSet))
            {
                return EmptyFrames;
            }

            Sprite[] cached;
            if (FrameCache.TryGetValue(spriteSet, out cached))
            {
                return cached;
            }

            Sprite[] loaded = Resources.LoadAll<Sprite>(Characters + spriteSet);
            if (loaded == null || loaded.Length == 0)
            {
                FrameCache[spriteSet] = EmptyFrames;
                return EmptyFrames;
            }

            Array.Sort(loaded, CompareSpriteNames);
            FrameCache[spriteSet] = loaded;
            return loaded;
        }

        static int CompareSpriteNames(Sprite a, Sprite b)
        {
            string left = a != null ? a.name : string.Empty;
            string right = b != null ? b.name : string.Empty;
            return string.CompareOrdinal(left, right);
        }
    }
}
