using OfficeHell.Config;
using UnityEngine;

namespace OfficeHell.View
{
    /// <summary>Imported world-space aura, quality-light and floor-mark artwork.</summary>
    public static class WorldFxCatalog
    {
        const string Root = "Slice/";

        static Sprite _circleBlue;
        static Sprite _circleRed;
        static Sprite _circleYellow;
        static Sprite _coffeeStain;
        static Sprite _greenLight;
        static Sprite _blueLight;
        static Sprite _purpleLight;
        static Sprite _orangeLight;

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

        public static Sprite GreenLight
        {
            get { return Load(ref _greenLight, "GreenLight"); }
        }

        public static Sprite BlueLight
        {
            get { return Load(ref _blueLight, "BlueLight"); }
        }

        public static Sprite PurpleLight
        {
            get { return Load(ref _purpleLight, "PurpleLight"); }
        }

        public static Sprite OrangeLight
        {
            get { return Load(ref _orangeLight, "OrangeLight"); }
        }

        public static Sprite QualityLight(Quality quality)
        {
            switch (quality)
            {
                case Quality.Blue: return BlueLight;
                case Quality.Purple: return PurpleLight;
                case Quality.Orange: return OrangeLight;
                default: return GreenLight;
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
