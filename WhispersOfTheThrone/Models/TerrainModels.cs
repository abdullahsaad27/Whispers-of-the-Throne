namespace WhispersOfTheThrone.Models
{
    public enum TerrainType
    {
        Plains,
        Mountains,
        Desert,
        Forest,
        Hills,
        Coastal,
        RiverCrossing
    }

    public sealed class TerrainProvince
    {
        public string ProvinceName { get; set; } = "";
        public TerrainType TerrainType { get; set; } = TerrainType.Plains;
    }

    public sealed class TerrainModifier
    {
        public TerrainType TerrainType { get; set; }
        public int AttackerPenalty { get; set; }
        public int DefenderBonus { get; set; }
        public int MovementCostDays { get; set; }
        public float SupplyLimitModifier { get; set; } = 1.0f;
        public float CombatWidthModifier { get; set; } = 1.0f;
    }

    public sealed class TerrainCommanderTrait
    {
        public string CharacterId { get; set; } = "";
        public TerrainType PreferredTerrain { get; set; }
        public int Bonus { get; set; } = 10;
    }
}
