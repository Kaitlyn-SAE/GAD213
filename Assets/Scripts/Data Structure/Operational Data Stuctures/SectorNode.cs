using System.Collections.Generic;
using UnityEngine;
using TheaterStrategy;

[SelectionBase]
public class SectorNode : MonoBehaviour
{
    [Header("Hierarchy")]
    public StateNode parentState;

    [Header("Sector Identification")]
    public string sectorName = "Sector Alpha";
    public SectorType sectorType = SectorType.Chokepoint;
    public Faction controllingFaction = Faction.NATO;

    [Header("Logistics & Strategic Value")]
    public bool isSupplySource = false;
    public SupplyStatus currentSupply = SupplyStatus.Supplied;
    [Range(1, 10)] public int strategicValue = 1;

    [Header("Capture Mechanics")]
    public float captureRadius = 35f;
    [Range(0f, 100f)] public float captureProgress = 100f;

    [Header("Polygonal Boundary Points (Local Space)")]
    [Tooltip("Perimeter points ordered clockwise around the node center")]
    public List<Vector3> localBoundaryPoints = new List<Vector3>();

    [Header("Adjacent Sectors (Graph Edges)")]
    public List<SectorNode> neighboringSectors = new List<SectorNode>();

    public Vector3 Position => transform.position;

    [ContextMenu("Generate Default Hexagon Boundary")]
    public void GenerateDefaultHexagon()
    {
        localBoundaryPoints.Clear();
        int sides = 6;
        float radius = captureRadius > 10f ? captureRadius : 35f;

        for (int i = 0; i < sides; i++)
        {
            float angle = i * (360f / sides) * Mathf.Deg2Rad;
            Vector3 point = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
            localBoundaryPoints.Add(point);
        }
    }

    private void OnValidate()
    {
        if (localBoundaryPoints.Count == 0)
        {
            GenerateDefaultHexagon();
        }

        foreach (var neighbor in neighboringSectors)
        {
            if (neighbor != null && !neighbor.neighboringSectors.Contains(this))
            {
                neighbor.neighboringSectors.Add(this);
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = GetFactionColor(controllingFaction);
        Gizmos.DrawSphere(transform.position + Vector3.up * 2f, 3f);

        if (localBoundaryPoints.Count >= 3)
        {
            Gizmos.color = GetFactionColor(controllingFaction);
            for (int i = 0; i < localBoundaryPoints.Count; i++)
            {
                Vector3 p1 = transform.TransformPoint(localBoundaryPoints[i]) + Vector3.up * 0.2f;
                Vector3 p2 = transform.TransformPoint(localBoundaryPoints[(i + 1) % localBoundaryPoints.Count]) + Vector3.up * 0.2f;
                Gizmos.DrawLine(p1, p2);
            }
        }
    }

    public static Color GetFactionColor(Faction faction)
    {
        return faction switch
        {
            Faction.NATO => new Color(0.2f, 0.5f, 1.0f, 1f),    
            Faction.ACMF => new Color(0.9f, 0.2f, 0.2f, 1f),    
            Faction.SATO => new Color(0.2f, 0.8f, 0.6f, 1f),    
            _ => new Color(0.6f, 0.6f, 0.6f, 1f)            
        };
    }
}