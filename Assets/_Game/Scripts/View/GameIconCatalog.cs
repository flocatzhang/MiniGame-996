using System.Collections.Generic;
using UnityEngine;

namespace OfficeHell.View
{
    /// <summary>One mapping for item art shared by cards, HUD slots and world entities.</summary>
    public static class GameIconCatalog
    {
        const string Root = "Icon/card/";
        const float KeyboardLootMultiplier = 1.5f;
        const float DefaultLootMultiplier = 3f;

        /// <summary>
        /// Lookup key to asset name. Upgrade cards are keyed by their id in Cards.xml and equipment by
        /// its def id, because the card panel and the HUD slot each only ever have one of those in hand
        /// at draw time.
        ///
        /// A key that is not in here returns null and the card panel falls back to its three letter
        /// label, which is legible but is not art: a new card added to Cards.xml without a row here
        /// ships looking like a placeholder rather than looking broken, so OfficeHellSelfTest walks the
        /// card pool against this table instead of leaving it to be noticed on screen.
        /// </summary>
        static readonly Dictionary<string, string> AssetNames = new Dictionary<string, string>(24)
        {
            { "stapler", "Stapler" },
            { "keyboard", "Keyboard" },
            { "badge", "WorkCard" },
            { "headphone", "Earphone" },
            { "hoodie", "PlaidShirt" },
            { "slippers", "Slippers" },

            { "c_atk", "Money" },
            { "c_atk_pct", "Performance" },
            { "c_haste", "Progress" },
            { "c_crit", "Inspiration" },
            { "c_critdmg", "HitKill" },
            { "c_def", "Calm" },
            { "c_dodge", "Opportunity" },
            { "c_san", "Prepare" },
            { "c_speed", "Run" },
            { "c_luck", "Pray" },
            { "c_magnet", "Seize" },

            { "s_deep", "Fish" },
            { "s_paid", "Vacation" },
            { "s_reverse", "AUP" },
            { "s_extra", "Health" },
            { "s_mass", "Fish3" },
        };

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(28);

        public static Sprite FriendlyProjectile
        {
            get { return Load("Thumbtacks"); }
        }

        public static Sprite EnemyProjectile
        {
            get { return Load("Crumpled paper"); }
        }

        public static Sprite OrbitWeapon
        {
            get { return Load("WorkCardUse"); }
        }

        public static Sprite Coffee
        {
            get { return Load("Coffee"); }
        }

        /// <summary>
        /// Keyboard is broad enough at half the scale used by the other world drops. Keeping the
        /// multiplier here also lets the supporting quality light preserve the same visible border.
        /// </summary>
        public static float LootIconMultiplier(string key)
        {
            return key == "keyboard" ? KeyboardLootMultiplier : DefaultLootMultiplier;
        }

        public static float LootQualityLightRadius(string key, float baseScale)
        {
            float relativeToKeyboard = LootIconMultiplier(key) / KeyboardLootMultiplier;
            return baseScale * 2f * relativeToKeyboard;
        }

        public static Sprite Item(string key)
        {
            string asset;
            if (string.IsNullOrEmpty(key) || !AssetNames.TryGetValue(key, out asset))
            {
                return null;
            }

            return Load(asset);
        }

        static Sprite Load(string name)
        {
            Sprite cached;
            if (Cache.TryGetValue(name, out cached) && cached != null)
            {
                return cached;
            }

            Sprite loaded = Resources.Load<Sprite>(Root + name);
            Cache[name] = loaded;
            return loaded;
        }
    }
}
