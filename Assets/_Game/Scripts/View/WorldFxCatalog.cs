using UnityEngine;

namespace OfficeHell.View
{
    /// <summary>Imported world-space aura and floor-mark artwork.</summary>
    public static class WorldFxCatalog
    {
        const string Root = "Slice/";

        static Sprite _circleBlue;
        static Sprite _circleRed;
        static Sprite _circleYellow;
        static Sprite _coffeeStain;

        public static Sprite CircleBlue
        {
            get { return Load(ref _circleBlue, "Circle_Blue"); }
        }

        public static Sprite CircleRed
        {
            get { return Load(ref _circleRed, "Circle_Red"); }
        }

        public static Sprite CircleYellow
        {
            get { return Load(ref _circleYellow, "Circle_Yellow"); }
        }

        public static Sprite CoffeeStain
        {
            get { return Load(ref _coffeeStain, "CoffeeStains"); }
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
