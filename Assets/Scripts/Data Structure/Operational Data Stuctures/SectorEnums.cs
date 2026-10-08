namespace TheaterStrategy
{
    public enum Faction
    {
        Neutral,
        NATO,
        SATO,
        ACMF
    }

    public enum SectorType
    {
        LogisticsHub,   // Main supply distribution point
        UrbanCenter,    // High victory value, defensive garrison bonus
        Airfield,       // Unlocks off-map Close Air Support (CAS)
        RadarStation,   // Dispels fog of war across neighboring sectors
        Chokepoint      // Fortified mountain pass or bridgehead
    }

    public enum SupplyStatus
    {
        Supplied,       // Full ammo/fuel regen
        Strained,       // Long line, reduced combat effectiveness
        Encircled       // Severed from logistics network, taking attrition
    }
}