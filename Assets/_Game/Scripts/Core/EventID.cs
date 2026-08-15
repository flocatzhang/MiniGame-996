namespace OfficeHell.Core
{
    public enum EventID
    {
        None = 0,

        ConfigReloaded,

        GameStateChanged,
        RunStarted,
        RunEnded,
        DayStarted,
        DayCleared,

        EnemySpawned,
        EnemyDamaged,
        EnemyKilled,

        PlayerDamaged,
        PlayerDodged,
        PlayerHealed,
        PlayerShielded,
        PlayerShieldBroken,

        /// <summary>Orange hoodie refused a hit outright. F0 is the shove radius.</summary>
        PlayerGuarded,
        PlayerDied,
        PlayerLevelUp,
        PlayerRankUp,

        LootDropped,
        LootPicked,

        WeaponEquipped,
        ArmorEquipped,
        EquipDeclined,
        WeaponFired,
        SlamLanded,
        SelectAll,
        OrbitRebuilt,
        SkillCast,
        CoffeeDrunk,

        BossSpawned,
        BossTelegraph,
        BossPieCast,
        BossPhaseChanged,
        BossClockedOut,

        CardsOffered,
        CardPicked,
    }
}
