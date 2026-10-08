using UnityEngine;

namespace TheaterStrategy
{
    public enum TacticalBiome
    {
        UrbanCity,          // Ruined skyscrapers, tight streets (e.g. Beijing, Seoul)
        WoodedHills,        // Forests, ridges, dirt roads (e.g. Manchuria, Korea)
        RiverChokepoint,    // Bridges, wetlands, crossing berms
        CoastalLanding      // Beaches, seawalls, docks
    }

    [System.Serializable]
    public class TacticalBattlefieldScene
    {
        public string battlefieldName = "Dense Urban Ruins";
        public TacticalBiome biome = TacticalBiome.UrbanCity;
        public GameObject environmentRoot; // Parent containing the 3D terrain, props, and NavMesh
        public Transform tacticalCameraSpawnPoint;
        public Transform friendlySpawnPoint;
        public Transform enemySpawnPoint;
    }
}