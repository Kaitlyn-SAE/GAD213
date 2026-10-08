using System.Collections.Generic;
using UnityEngine;
using TheaterStrategy;

[RequireComponent(typeof(SectorNode))]
public class ProvinceRenderer : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private float borderLineWidth = 1.2f;
    [Range(0.05f, 0.8f)][SerializeField] private float territoryAlpha = 0.25f; 
    [SerializeField] private float elevationOffset = 0.15f; 

    private SectorNode _sector;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private LineRenderer _lineRenderer;
    private Material _fillMaterial;

    private void Awake()
    {
        _sector = GetComponent<SectorNode>();
        SetupVisualComponents();
    }

    private void Start()
    {
        RebuildProvinceVisuals();
    }

    private void SetupVisualComponents()
    {
        _meshFilter = gameObject.GetComponent<MeshFilter>();
        if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();

        _meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

        _fillMaterial = new Material(unlitShader);
        _meshRenderer.material = _fillMaterial;
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;

        _lineRenderer = gameObject.GetComponent<LineRenderer>();
        if (_lineRenderer == null) _lineRenderer = gameObject.AddComponent<LineRenderer>();

        _lineRenderer.material = new Material(unlitShader);
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;
        _lineRenderer.loop = true;
        _lineRenderer.useWorldSpace = true;
    }

    [ContextMenu("Rebuild Province Visuals")]
    public void RebuildProvinceVisuals()
    {
        if (_sector == null) _sector = GetComponent<SectorNode>();
        if (_sector.localBoundaryPoints.Count < 3) return;

        UpdateColors();
        GenerateTerritoryMesh();
        GenerateBorderLine();
    }

    public void UpdateColors()
    {
        Color factionBaseColor = SectorNode.GetFactionColor(_sector.controllingFaction);

        Color fillColor = new Color(factionBaseColor.r, factionBaseColor.g, factionBaseColor.b, territoryAlpha);
        if (_fillMaterial != null) _fillMaterial.color = fillColor;

        Color borderColor = new Color(factionBaseColor.r, factionBaseColor.g, factionBaseColor.b, 0.9f);
        if (_lineRenderer != null)
        {
            _lineRenderer.startColor = borderColor;
            _lineRenderer.endColor = borderColor;
        }
    }

    private void GenerateTerritoryMesh()
    {
        Mesh mesh = new Mesh { name = $"{_sector.sectorName}_Mesh" };
        var points = _sector.localBoundaryPoints;
        int count = points.Count;

        Vector3[] vertices = new Vector3[count + 1];
        vertices[0] = new Vector3(0, elevationOffset, 0);

        for (int i = 0; i < count; i++)
        {
            vertices[i + 1] = new Vector3(points[i].x, elevationOffset, points[i].z);
        }

        int[] triangles = new int[count * 3];
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = next + 1;
        }

        Vector2[] uvs = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x, vertices[i].z);
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        _meshFilter.mesh = mesh;
    }

    private void GenerateBorderLine()
    {
        var points = _sector.localBoundaryPoints;
        _lineRenderer.positionCount = points.Count;
        _lineRenderer.startWidth = borderLineWidth;
        _lineRenderer.endWidth = borderLineWidth;

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 worldPos = transform.TransformPoint(points[i]) + Vector3.up * (elevationOffset + 0.05f);
            _lineRenderer.SetPosition(i, worldPos);
        }
    }
}