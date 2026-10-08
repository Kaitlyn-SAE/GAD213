using UnityEngine;
using TheaterStrategy;

namespace TheaterStrategy.MilitaryUI
{
    public enum UnitType
    {
        Infantry,     
        Armor,        
        Mechanized,   
        Artillery,    
        Headquarters  
    }

    [System.Serializable]
    public struct CounterStyle
    {
        public Color frameColor;
        public Color backgroundColor;
        public string frameShapeSymbol; 
    }

    public static class SymbologyHelper
    {
        public static string GetUnitTypeSymbol(UnitType type)
        {
            return type switch
            {
                UnitType.Infantry => "✕",
                UnitType.Armor => "⬭",
                UnitType.Mechanized => "✕⬭",
                UnitType.Artillery => "●",
                UnitType.Headquarters => "⚑ HQ",
                _ => "?"
            };
        }


        public static CounterStyle GetFactionStyle(Faction faction)
        {
            return faction switch
            {
                Faction.ACMF => new CounterStyle
                {
                    frameColor = new Color(0.95f, 0.2f, 0.2f, 1f),      
                    backgroundColor = new Color(0.35f, 0.05f, 0.05f, 0.85f),
                    frameShapeSymbol = "◇" 
                },
                Faction.SATO => new CounterStyle
                {
                    frameColor = new Color(0.2f, 0.9f, 0.7f, 1f),      
                    backgroundColor = new Color(0.05f, 0.25f, 0.2f, 0.85f),
                    frameShapeSymbol = "▭" 
                },
                _ => new CounterStyle 
                {
                    frameColor = new Color(0.25f, 0.55f, 1.0f, 1f),    
                    backgroundColor = new Color(0.05f, 0.15f, 0.35f, 0.85f),
                    frameShapeSymbol = "▭" 
                }
            };
        }
    }
}