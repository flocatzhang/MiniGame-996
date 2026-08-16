using UnityEngine;

namespace OfficeHell.View
{
    /// <summary>One mapping for item art shared by cards, HUD slots and world entities.</summary>
    public static class GameIconCatalog
    {
        const string Root = "Icon/card/";

        static Sprite _stapler;
        static Sprite _keyboard;
        static Sprite _workCard;
        static Sprite _earphone;
        static Sprite _plaidShirt;
        static Sprite _slippers;
        static Sprite _magnet;
        static Sprite _staples;
        static Sprite _friendlyProjectile;
        static Sprite _enemyProjectile;
        static Sprite _orbitWeapon;
        static Sprite _coffee;

        public static Sprite FriendlyProjectile
        {
            get { return Load(ref _friendlyProjectile, "Thumbtacks"); }
        }

        public static Sprite EnemyProjectile
        {
            get { return Load(ref _enemyProjectile, "Crumpled paper"); }
        }

        public static Sprite OrbitWeapon
        {
            get { return Load(ref _orbitWeapon, "WorkCardUse"); }
        }

        public static Sprite Coffee
        {
            get { return Load(ref _coffee, "Coffee"); }
        }

        public static Sprite Item(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            switch (key)
            {
                case "stapler": return Load(ref _stapler, "Stapler");
                case "keyboard": return Load(ref _keyboard, "Keyboard");
                case "badge": return Load(ref _workCard, "WorkCard");
                case "headphone": return Load(ref _earphone, "Earphone");
                case "hoodie": return Load(ref _plaidShirt, "PlaidShirt");
                case "slippers": return Load(ref _slippers, "Slippers");
                case "c_magnet": return Load(ref _magnet, "Magnet");
                case "c_atk":
                case "c_atk_pct":
                    return Load(ref _staples, "Staples");
                default: return null;
            }
        }

        static Sprite Load(ref Sprite cached, string name)
        {
            if (cached == null)
            {
                cached = Resources.Load<Sprite>(Root + name);
            }

            return cached;
        }
    }
}
