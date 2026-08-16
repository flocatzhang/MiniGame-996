using OfficeHell.Config;
using OfficeHell.Model;

namespace OfficeHell.Systems
{
    /// <summary>The fixed three-weapon opening kit shared by the real game and headless runs.</summary>
    public static class StartingLoadout
    {
        public const int WeaponCount = 3;

        public static int Equip(PlayerModel player, ConfigManager config)
        {
            if (player == null || config == null)
            {
                return 0;
            }

            int equipped = 0;
            for (int slot = 0; slot < WeaponCount; slot++)
            {
                WeaponDef weapon = config.Weapon(WeaponId(slot));
                if (weapon != null && player.Equip(slot, weapon, Quality.Green))
                {
                    equipped++;
                }
            }

            return equipped;
        }

        public static string WeaponId(int slot)
        {
            switch (slot)
            {
                case 0: return "stapler";
                case 1: return "keyboard";
                case 2: return "badge";
                default: return string.Empty;
            }
        }
    }
}
