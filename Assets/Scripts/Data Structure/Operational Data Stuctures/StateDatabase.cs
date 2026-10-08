using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using TheaterStrategy;

public class StateDatabase : MonoBehaviour
{
    public static StateDatabase Instance { get; private set; }

    [System.Serializable]
    public class StateDataEntry
    {
        public int stateIndex; 
        public string stateName;
        public string country;
        public Faction startingFaction;
        public Color32 colorKey;
        public int integerKey;
    }

    [Header("Data Source")]
    [SerializeField] private TextAsset stateDefinitionFile;

    private readonly Dictionary<int, StateDataEntry> _statesByColorKey = new Dictionary<int, StateDataEntry>();
    private bool _isLoaded = false;

    private void Awake()
    {
        Instance = this;
        EnsureLoaded();
    }

    public void EnsureLoaded()
    {
        Instance = this;
        if (_isLoaded) return;
        LoadStateDefinitions();
    }

    [ContextMenu("Reload Definitions")]
    public void LoadStateDefinitions()
    {
        if (stateDefinitionFile == null)
        {
            Debug.LogError("[StateDatabase] No StateDefinitions.txt assigned!");
            return;
        }

        _statesByColorKey.Clear();

        string[] lines = stateDefinitionFile.text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        string currentCountry = "Unknown";
        Regex linePattern = new Regex(@"R:\s*(\d+)\s*G:\s*(\d+)\s*B:\s*(\d+)\s*-\s*(.+)");

        int nextIndex = 0;

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.EndsWith(":") && !line.StartsWith("R:"))
            {
                currentCountry = line.Replace(":", "").Trim();
                continue;
            }

            Match match = linePattern.Match(line);
            if (match.Success)
            {
                byte r = byte.Parse(match.Groups[1].Value);
                byte g = byte.Parse(match.Groups[2].Value);
                byte b = byte.Parse(match.Groups[3].Value);
                string stateName = match.Groups[4].Value.Trim();

                Color32 color = new Color32(r, g, b, 255);
                int key = (r << 16) | (g << 8) | b;

                Faction faction = DetermineStartingFaction(currentCountry);

                StateDataEntry entry = new StateDataEntry
                {
                    stateIndex = nextIndex++, 
                    stateName = stateName,
                    country = currentCountry,
                    startingFaction = faction,
                    colorKey = color,
                    integerKey = key
                };

                _statesByColorKey[key] = entry;
            }
        }

        _isLoaded = true;
        Debug.Log($"<color=green>[StateDatabase] Loaded {_statesByColorKey.Count} states with unique indices 0 to {nextIndex - 1}!</color>");
    }

    private Faction DetermineStartingFaction(string country)
    {
        switch (country.ToLower())
        {
            case "china":
            case "north korea":
            case "russia":
            case "mongolia":
                return Faction.ACMF;

            case "south korea":
                return Faction.SATO;

            case "japan":
                return Faction.NATO;

            default:
                return Faction.Neutral;
        }
    }

    public StateDataEntry GetStateByColor(Color32 color)
    {
        EnsureLoaded();
        int exactKey = (color.r << 16) | (color.g << 8) | color.b;
        _statesByColorKey.TryGetValue(exactKey, out var exactState);
        return exactState;
    }

    public Dictionary<int, StateDataEntry> GetAllStates() => _statesByColorKey;
}