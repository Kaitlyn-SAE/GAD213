using System.Collections.Generic;
using UnityEngine;
using TheaterStrategy;

public class StateNode : MonoBehaviour
{
    [Header("State Identity")]
    public string stateName = "Northern Corridor";
    public Faction dominantFaction = Faction.Neutral;

    [Header("State Logistics & Industry")]
    [Range(1, 5)] public int infrastructureLevel = 3; 
    public int ammoProductionPerMinute = 50;
    public int fuelProductionPerMinute = 30;

    [Header("Administrative Structure")]
    public SectorNode stateCapital; 
    public List<SectorNode> provinces = new List<SectorNode>();


    public void EvaluateStateDominance()
    {
        if (provinces.Count == 0) return;

        Dictionary<Faction, int> factionScore = new Dictionary<Faction, int>
        {
            { Faction.NATO, 0 },
            { Faction.ACMF, 0 },
            { Faction.SATO, 0 },
            { Faction.Neutral, 0 }
        };

        foreach (var province in provinces)
        {
            if (province == null) continue;

            int weight = province.strategicValue;
            if (province == stateCapital) weight *= 2; 

            factionScore[province.controllingFaction] += weight;
        }

        Faction leadingFaction = Faction.Neutral;
        int highestScore = 0;

        foreach (var kvp in factionScore)
        {
            if (kvp.Value > highestScore)
            {
                highestScore = kvp.Value;
                leadingFaction = kvp.Key;
            }
        }

        dominantFaction = leadingFaction;
    }

    private void OnValidate()
    {
        foreach (var province in provinces)
        {
            if (province != null && province.parentState != this)
            {
                province.parentState = this;
            }
        }
    }
}