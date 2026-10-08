using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TheaterStrategy;

public class HOI4MapManager : MonoBehaviour
{
    public static HOI4MapManager Instance { get; private set; }

    [System.Serializable]
    public class RuntimeProvince
    {
        public int id;
        public Color32 colorKey;
        public string provinceName;
        public StateDatabase.StateDataEntry parentState;
        public Faction controllingFaction = Faction.Neutral;
        public Vector2 pixelCentroid;
        public Vector3 worldPosition;
        public HashSet<int> neighborIDs = new HashSet<int>();
    }

    [System.Serializable]
    public struct StraitCrossing
    {
        public string crossingName;
        public int provinceA_ID;
        public int provinceB_ID;
    }

    [Header("Map Textures")]
    [SerializeField] private Texture2D provinceMapTexture;
    [SerializeField] private Texture2D stateMapTexture;

    [Header("Water & Exclusions")]
    [SerializeField] private Color32 seaColor = new Color32(68, 107, 163, 255);
    [SerializeField] private bool ignoreSea = true;

    [Header("World Dimensions")]
    [SerializeField] private Collider mapCollider;
    [SerializeField] private bool autoFitToCollider = true;
    [SerializeField] private Vector2 mapWorldSize = new Vector2(500f, 500f);
    [SerializeField] private Vector3 mapWorldOrigin = Vector3.zero;

    [Header("Manual Strait / Ferry Connections")]
    [SerializeField] private List<StraitCrossing> straitCrossings = new List<StraitCrossing>();

    [Header("Gizmo & Pathfinding Settings")]
    [SerializeField] private bool drawCentroidDots = true;
    [SerializeField] private float dotSize = 1.0f;
    [SerializeField] private bool drawAllNeighborLines = false;
    [SerializeField] private float maxValidNeighborDistance = 150f;

    private readonly Dictionary<int, RuntimeProvince> _provincesByColorKey = new Dictionary<int, RuntimeProvince>();
    private readonly Dictionary<int, RuntimeProvince> _provincesByID = new Dictionary<int, RuntimeProvince>();
    private RuntimeProvince _hoveredProvince;
    private RuntimeProvince _selectedProvince;

    public event Action<RuntimeProvince> OnProvinceSelected;
    public event Action<RuntimeProvince> OnProvinceRightClicked;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (StateDatabase.Instance != null)
        {
            StateDatabase.Instance.EnsureLoaded();
        }
        else
        {
            var db = FindAnyObjectByType<StateDatabase>();
            if (db != null) db.EnsureLoaded();
        }

        SyncColliderBounds();
        ParseMaps(); 
    }

    private void SyncColliderBounds()
    {
        if (autoFitToCollider && mapCollider != null)
        {
            mapWorldSize = new Vector2(mapCollider.bounds.size.x, mapCollider.bounds.size.z);
            mapWorldOrigin = mapCollider.bounds.center;
        }
    }

    private void Update()
    {
        HandleMouseInteraction();
    }

    [ContextMenu("Parse Maps")]
    public void ParseMaps()
    {
        SyncColliderBounds();

        Texture2D primaryTexture = provinceMapTexture != null ? provinceMapTexture : stateMapTexture;
        if (primaryTexture == null)
        {
            Debug.LogError("[HOI4MapManager] Assign at least a State Map or Province Map Texture!");
            return;
        }

        _provincesByColorKey.Clear();
        _provincesByID.Clear();

        int width = primaryTexture.width;
        int height = primaryTexture.height;
        Color32[] primaryPixels = primaryTexture.GetPixels32();

        Dictionary<int, long> sumX = new Dictionary<int, long>();
        Dictionary<int, long> sumY = new Dictionary<int, long>();
        Dictionary<int, int> pixelCounts = new Dictionary<int, int>();
        Dictionary<int, Vector2> samplePixels = new Dictionary<int, Vector2>();

        int nextID = 1;

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = rowOffset + x;
                Color32 c = primaryPixels[index];

                if (ignoreSea && IsSeaColor(c)) continue;

                int key = ColorToKey(c);

                if (!_provincesByColorKey.TryGetValue(key, out var province))
                {
                    province = new RuntimeProvince
                    {
                        id = nextID++,
                        colorKey = c,
                        provinceName = $"Sector_{nextID - 1}"
                    };

                    _provincesByColorKey.Add(key, province);
                    _provincesByID.Add(province.id, province);

                    sumX[key] = 0;
                    sumY[key] = 0;
                    pixelCounts[key] = 0;
                    samplePixels[key] = new Vector2(x, y);
                }

                sumX[key] += x;
                sumY[key] += y;
                pixelCounts[key]++;
            }
        }

        foreach (var kvp in _provincesByColorKey)
        {
            int key = kvp.Key;
            var prov = kvp.Value;
            int count = pixelCounts[key];

            float avgX = (float)sumX[key] / count;
            float avgY = (float)sumY[key] / count;

            int cx = Mathf.Clamp(Mathf.RoundToInt(avgX), 0, width - 1);
            int cy = Mathf.Clamp(Mathf.RoundToInt(avgY), 0, height - 1);

            Color32 sampledColor = primaryPixels[cy * width + cx];
            if (ColorToKey(sampledColor) != key)
            {
                Vector2 fallback = samplePixels[key];
                avgX = fallback.x;
                avgY = fallback.y;
                cx = (int)avgX;
                cy = (int)avgY;
            }

            prov.pixelCentroid = new Vector2(avgX, avgY);

            float normalizedX = avgX / width;
            float normalizedZ = avgY / height;

            prov.worldPosition = new Vector3(
                mapWorldOrigin.x + (normalizedX - 0.5f) * mapWorldSize.x,
                mapWorldOrigin.y + 0.5f,
                mapWorldOrigin.z + (normalizedZ - 0.5f) * mapWorldSize.y
            );

            if (stateMapTexture != null && StateDatabase.Instance != null)
            {
                int sx = Mathf.Clamp(Mathf.FloorToInt(normalizedX * stateMapTexture.width), 0, stateMapTexture.width - 1);
                int sy = Mathf.Clamp(Mathf.FloorToInt(normalizedZ * stateMapTexture.height), 0, stateMapTexture.height - 1);
                Color32 stateColor = stateMapTexture.GetPixel(sx, sy);

                var matchedState = StateDatabase.Instance.GetStateByColor(stateColor);
                if (matchedState != null)
                {
                    prov.parentState = matchedState;
                    prov.provinceName = $"{matchedState.stateName} - Sector {prov.id}";
                    prov.controllingFaction = matchedState.startingFaction;
                }
            }
        }

        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                Color32 c = primaryPixels[rowOffset + x];
                if (ignoreSea && IsSeaColor(c)) continue;

                int currentKey = ColorToKey(c);

                if (x + 1 < width) CheckAndRegister(currentKey, primaryPixels[rowOffset + (x + 1)]);
                if (y + 1 < height) CheckAndRegister(currentKey, primaryPixels[(y + 1) * width + x]);
                if (x + 1 < width && y + 1 < height) CheckAndRegister(currentKey, primaryPixels[(y + 1) * width + (x + 1)]);
                if (x - 1 >= 0 && y + 1 < height) CheckAndRegister(currentKey, primaryPixels[(y + 1) * width + (x - 1)]);
            }
        }

        foreach (var strait in straitCrossings)
        {
            if (_provincesByID.TryGetValue(strait.provinceA_ID, out var pA) &&
                _provincesByID.TryGetValue(strait.provinceB_ID, out var pB))
            {
                pA.neighborIDs.Add(pB.id);
                pB.neighborIDs.Add(pA.id);
                Debug.Log($"<color=cyan>[Strait Connected] {strait.crossingName} (#{pA.id} <-> #{pB.id})</color>");
            }
        }

        Debug.Log($"<color=cyan>[HOI4MapManager] Parsed {width}x{height} map! Loaded {_provincesByID.Count} land provinces with land-bound centroids.</color>");

        var territoryRenderer = MapTerritoryRenderer.Instance ?? FindAnyObjectByType<MapTerritoryRenderer>();
        if (territoryRenderer != null)
        {
            territoryRenderer.EnsureInitialized();
            territoryRenderer.BakeProvinceIDTexture(
                width,
                height,
                primaryPixels,
                color => _provincesByColorKey.TryGetValue(ColorToKey(color), out var p) ? p.id : 0
            );

            territoryRenderer.RefreshAllProvinces(_provincesByID.Values);
        }
        else
        {
            Debug.LogError("[HOI4MapManager] Could not find MapTerritoryRenderer in scene!");
        }
    }

    private void CheckAndRegister(int currentKey, Color32 neighborColor)
    {
        if (!ignoreSea || !IsSeaColor(neighborColor))
        {
            int neighborKey = ColorToKey(neighborColor);
            if (currentKey != neighborKey) RegisterNeighbors(currentKey, neighborKey);
        }
    }

    public List<RuntimeProvince> FindProvincePath(int startProvinceID, int targetProvinceID)
    {
        if (!_provincesByID.TryGetValue(startProvinceID, out var startNode) ||
            !_provincesByID.TryGetValue(targetProvinceID, out var targetNode))
        {
            return null;
        }

        var openSet = new List<RuntimeProvince> { startNode };
        var closedSet = new HashSet<int>();
        var cameFrom = new Dictionary<int, RuntimeProvince>();
        var gScore = new Dictionary<int, float> { [startNode.id] = 0f };
        var fScore = new Dictionary<int, float>
        {
            [startNode.id] = Vector3.Distance(startNode.worldPosition, targetNode.worldPosition)
        };

        while (openSet.Count > 0)
        {
            RuntimeProvince current = openSet[0];
            float lowestF = fScore.GetValueOrDefault(current.id, float.MaxValue);

            for (int i = 1; i < openSet.Count; i++)
            {
                float f = fScore.GetValueOrDefault(openSet[i].id, float.MaxValue);
                if (f < lowestF)
                {
                    lowestF = f;
                    current = openSet[i];
                }
            }

            if (current.id == targetNode.id)
            {
                return ReconstructPath(cameFrom, current);
            }

            openSet.Remove(current);
            closedSet.Add(current.id);

            foreach (int neighborID in current.neighborIDs)
            {
                if (closedSet.Contains(neighborID)) continue;
                if (!_provincesByID.TryGetValue(neighborID, out var neighbor)) continue;

                float dist = Vector3.Distance(current.worldPosition, neighbor.worldPosition);
                if (dist > maxValidNeighborDistance) continue;

                float tentativeG = gScore.GetValueOrDefault(current.id, float.MaxValue) + dist;

                if (tentativeG < gScore.GetValueOrDefault(neighbor.id, float.MaxValue))
                {
                    cameFrom[neighbor.id] = current;
                    gScore[neighbor.id] = tentativeG;
                    fScore[neighbor.id] = tentativeG + Vector3.Distance(neighbor.worldPosition, targetNode.worldPosition);

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        Debug.LogWarning($"[Pathfinding] No valid route from {startNode.provinceName} (#{startNode.id}) to {targetNode.provinceName} (#{targetNode.id}).");
        return null;
    }

    private List<RuntimeProvince> ReconstructPath(Dictionary<int, RuntimeProvince> cameFrom, RuntimeProvince current)
    {
        var totalPath = new List<RuntimeProvince> { current };
        while (cameFrom.TryGetValue(current.id, out var prev))
        {
            current = prev;
            totalPath.Add(current);
        }
        totalPath.Reverse();
        return totalPath;
    }

    public RuntimeProvince GetProvinceByID(int id)
    {
        _provincesByID.TryGetValue(id, out var prov);
        return prov;
    }

    public RuntimeProvince GetClosestProvince(Vector3 worldPos)
    {
        RuntimeProvince closest = null;
        float minDist = float.MaxValue;

        foreach (var kvp in _provincesByID)
        {
            float d = Vector3.Distance(worldPos, kvp.Value.worldPosition);
            if (d < minDist)
            {
                minDist = d;
                closest = kvp.Value;
            }
        }
        return closest;
    }

    private void HandleMouseInteraction()
    {
        var mouse = Mouse.current;
        if (mouse == null || Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit) && hit.collider == mapCollider)
        {
            Texture2D primaryTexture = provinceMapTexture != null ? provinceMapTexture : stateMapTexture;
            if (primaryTexture == null) return;

            Vector2 uv = hit.textureCoord;
            int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * primaryTexture.width), 0, primaryTexture.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * primaryTexture.height), 0, primaryTexture.height - 1);

            Color32 pixelColor = primaryTexture.GetPixel(x, y);
            int key = ColorToKey(pixelColor);

            _provincesByColorKey.TryGetValue(key, out _hoveredProvince);

            if (_hoveredProvince != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    _selectedProvince = _hoveredProvince;

                    _selectedProvince.controllingFaction = _selectedProvince.controllingFaction switch
                    {
                        Faction.Neutral => Faction.SATO,
                        Faction.SATO => Faction.NATO,
                        Faction.NATO => Faction.ACMF,
                        Faction.ACMF => Faction.Neutral,
                        _ => Faction.Neutral
                    };

                    if (MapTerritoryRenderer.Instance != null)
                    {
                        MapTerritoryRenderer.Instance.SetProvinceFaction(_selectedProvince.id, _selectedProvince.controllingFaction);
                    }

                    string stateInfo = _selectedProvince.parentState != null
                        ? $"State: {_selectedProvince.parentState.stateName} ({_selectedProvince.parentState.country}) | Faction: {_selectedProvince.controllingFaction}"
                        : "State: Unassigned";

                    Debug.Log($"<color=yellow>[Selected #{_selectedProvince.id}] {_selectedProvince.provinceName} | {stateInfo} | Direct Neighbors: {_selectedProvince.neighborIDs.Count}</color>");
                    OnProvinceSelected?.Invoke(_selectedProvince);
                }

                if (mouse.rightButton.wasPressedThisFrame)
                {
                    Debug.Log($"<color=lime>[Move Order Issued] Target: {_hoveredProvince.provinceName} (#{_hoveredProvince.id})</color>");
                    OnProvinceRightClicked?.Invoke(_hoveredProvince);
                }
            }
            return;
        }

        _hoveredProvince = null;
    }

    private bool IsSeaColor(Color32 c) => c.r == seaColor.r && c.g == seaColor.g && c.b == seaColor.b;

    private void RegisterNeighbors(int keyA, int keyB)
    {
        if (_provincesByColorKey.TryGetValue(keyA, out var provA) &&
            _provincesByColorKey.TryGetValue(keyB, out var provB))
        {
            provA.neighborIDs.Add(provB.id);
            provB.neighborIDs.Add(provA.id);
        }
    }

    public static int ColorToKey(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

    private void OnDrawGizmos()
    {
        if (_provincesByID == null || _provincesByID.Count == 0) return;

        if (drawCentroidDots)
        {
            foreach (var kvp in _provincesByID)
            {
                var prov = kvp.Value;
                Gizmos.color = prov == _selectedProvince ? Color.yellow : new Color(0.2f, 0.8f, 1f, 0.4f);
                Gizmos.DrawSphere(prov.worldPosition, dotSize);
            }
        }

        if (_selectedProvince != null)
        {
            Gizmos.color = Color.yellow;
            foreach (int neighborID in _selectedProvince.neighborIDs)
            {
                if (_provincesByID.TryGetValue(neighborID, out var neighborProv))
                {
                    if (Vector3.Distance(_selectedProvince.worldPosition, neighborProv.worldPosition) <= maxValidNeighborDistance)
                    {
                        Gizmos.DrawLine(_selectedProvince.worldPosition, neighborProv.worldPosition);
                    }
                }
            }
        }
    }
}