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
