using System.Collections.Generic;
using UnityEngine;
using TheaterStrategy;

public class TheaterMapManager : MonoBehaviour
{
    public static TheaterMapManager Instance { get; private set; }

    [Header("States In Theater")]
    [SerializeField] private List<StateNode> allStates = new List<StateNode>();

    [Header("Sectors In Theater")]
    [SerializeField] private List<SectorNode> allSectors = new List<SectorNode>();

    [Header("Frontline Visuals")]
    [SerializeField] private Material frontlineLineMaterial;
    [SerializeField] private float frontlineLineWidth = 3.5f;

    public struct FrontlineEdge
    {
        public SectorNode SectorA;
        public SectorNode SectorB;
        public Vector3 Midpoint;
    }

    private readonly List<FrontlineEdge> _activeFrontlines = new List<FrontlineEdge>();
    private readonly List<LineRenderer> _lineRenderersPool = new List<LineRenderer>();
    private GameObject _frontlinesContainer;

    private void Awake()
    {
        Instance = this;
        _frontlinesContainer = new GameObject("[ActiveFrontlines_Visuals]");
        _frontlinesContainer.transform.SetParent(transform);

        if (allSectors.Count == 0)
        {
            allSectors.AddRange(FindObjectsByType<SectorNode>(FindObjectsSortMode.None));
        }
    }

    private void Start()
    {
        RebuildTheaterState();
    }

    [ContextMenu("Rebuild Theater State")]
    public void RebuildTheaterState()
    {
        FindActiveFrontlines();
        UpdateSupplyNetwork(Faction.NATO);
        UpdateSupplyNetwork(Faction.ACMF);
        UpdateSupplyNetwork(Faction.SATO);
        RenderFrontlines();
        UpdateStates();

        foreach (var sector in allSectors)
        {
            RefreshSectorVisuals(sector);
        }
    }

    private void UpdateStates()
    {
        if (allStates.Count == 0)
        {
            allStates.AddRange(FindObjectsByType<StateNode>(FindObjectsSortMode.None));
        }

        foreach (var state in allStates)
        {
            state.EvaluateStateDominance();
        }
    }

    private void FindActiveFrontlines()
    {
        _activeFrontlines.Clear();
        var processedPairs = new HashSet<(SectorNode, SectorNode)>();

        foreach (var sector in allSectors)
        {
            foreach (var neighbor in sector.neighboringSectors)
            {
                if (neighbor == null) continue;

                if (processedPairs.Contains((sector, neighbor)) || processedPairs.Contains((neighbor, sector)))
                    continue;

                processedPairs.Add((sector, neighbor));

                if (sector.controllingFaction != neighbor.controllingFaction &&
                    sector.controllingFaction != Faction.Neutral &&
                    neighbor.controllingFaction != Faction.Neutral)
                {
                    _activeFrontlines.Add(new FrontlineEdge
                    {
                        SectorA = sector,
                        SectorB = neighbor,
                        Midpoint = (sector.Position + neighbor.Position) * 0.5f
                    });
                }
            }
        }
    }

    private void UpdateSupplyNetwork(Faction faction)
    {
        if (faction == Faction.Neutral) return;

        var supplySources = allSectors.FindAll(s => s.controllingFaction == faction && s.isSupplySource);
        var reachableSectors = new HashSet<SectorNode>();
        var queue = new Queue<SectorNode>(supplySources);

        foreach (var source in supplySources)
        {
            reachableSectors.Add(source);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var neighbor in current.neighboringSectors)
            {
                if (neighbor.controllingFaction == faction && !reachableSectors.Contains(neighbor))
                {
                    reachableSectors.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        foreach (var s in allSectors)
        {
            if (s.controllingFaction == faction)
            {
                if (reachableSectors.Contains(s))
                    s.currentSupply = SupplyStatus.Supplied;
                else
                    s.currentSupply = SupplyStatus.Encircled; 
            }
        }
    }

    private void RenderFrontlines()
    {
        foreach (var lr in _lineRenderersPool) lr.gameObject.SetActive(false);

        for (int i = 0; i < _activeFrontlines.Count; i++)
        {
            LineRenderer lr;
            if (i < _lineRenderersPool.Count)
            {
                lr = _lineRenderersPool[i];
                lr.gameObject.SetActive(true);
            }
            else
            {
                var lineObj = new GameObject($"Frontline_{i}");
                lineObj.transform.SetParent(_frontlinesContainer.transform);
                lr = lineObj.AddComponent<LineRenderer>();
                lr.material = frontlineLineMaterial != null
                    ? frontlineLineMaterial
                    : new Material(Shader.Find("Sprites/Default"));
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                _lineRenderersPool.Add(lr);
            }

            var edge = _activeFrontlines[i];

            Vector3 dir = (edge.SectorB.Position - edge.SectorA.Position).normalized;
            Vector3 perpendicular = Vector3.Cross(dir, Vector3.up).normalized;
            float ribbonLength = 40f;

            Vector3 startPoint = edge.Midpoint + perpendicular * ribbonLength + Vector3.up * 1f;
            Vector3 endPoint = edge.Midpoint - perpendicular * ribbonLength + Vector3.up * 1f;

            lr.startWidth = frontlineLineWidth;
            lr.endWidth = frontlineLineWidth;
            lr.positionCount = 2;
            lr.SetPosition(0, startPoint);
            lr.SetPosition(1, endPoint);

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(SectorNode.GetFactionColor(edge.SectorA.controllingFaction), 0.0f),
                        new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0.5f),
                        new GradientColorKey(SectorNode.GetFactionColor(edge.SectorB.controllingFaction), 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
            );
            lr.colorGradient = gradient;
        }
    }

    public void RefreshSectorVisuals(SectorNode sector)
    {
        var renderer = sector.GetComponent<ProvinceRenderer>();
        if (renderer != null)
        {
            renderer.UpdateColors();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        foreach (var edge in _activeFrontlines)
        {
            Gizmos.DrawWireCube(edge.Midpoint + Vector3.up * 3f, new Vector3(8f, 2f, 8f));
        }
    }
}