using System.Collections.Generic;
using UnityEngine;
using TheaterStrategy;

[RequireComponent(typeof(LineRenderer))]
public class StrategicDivision : MonoBehaviour
{
    [Header("Division Identity")]
    public string divisionName = "1st SATO Mechanized Brigade";
    public Faction faction = Faction.SATO;
    public Sprite divisionSymbolSprite; 
    public float baseMovementSpeed = 12f;
    public bool captureTerritoryOnMarch = true;

    [Header("HOI4 Combat Stats")]
    [Range(0f, 100f)] public float currentOrganization = 100f; 
    public float maxOrganization = 100f;

    [Range(0f, 100f)] public float currentSupply = 100f;       
    public float maxSupply = 100f;

    [Header("Graph State")]
    public int currentProvinceID = -1;
    public int targetProvinceID = -1;
    public bool hasMoveOrder = false;

    [Header("Discrete March Progress (0.0 to 1.0)")]
    [Range(0f, 1f)] public float legProgress = 0f;
    public float hoursRequiredForCurrentLeg = 8f;

    [Header("Selection State")]
    public bool isSelected = false;

    private LineRenderer _pathLine;
    private StrategicCounterDisplay _display;

    private void Awake()
    {
        _pathLine = GetComponent<LineRenderer>();
        _pathLine.startWidth = 0.8f;
        _pathLine.endWidth = 0.8f;
        _pathLine.material = new Material(Shader.Find("Sprites/Default"));
        _pathLine.startColor = Color.yellow;
        _pathLine.endColor = Color.green;
        _pathLine.positionCount = 0;

        _display = GetComponentInChildren<StrategicCounterDisplay>();
    }

    private void Start()
    {
        InitializeStartingProvince();

        if (DivisionClusterManager.Instance != null)
        {
            DivisionClusterManager.Instance.RegisterDivision(this);
        }

        UpdateVisuals();
    }

    private void Update()
    {
        if (_display != null)
        {
            _display.SetStats(currentOrganization / maxOrganization, currentSupply / maxSupply);
        }
    }

    public void InitializeStartingProvince()
    {
        if (HOI4MapManager.Instance != null)
        {
            var startProv = HOI4MapManager.Instance.GetClosestProvince(transform.position);
            if (startProv != null)
            {
                currentProvinceID = startProv.id;
                transform.position = startProv.worldPosition + Vector3.up * 1f;
            }
        }
    }

    private void OnDestroy()
    {
        if (DivisionClusterManager.Instance != null)
        {
            DivisionClusterManager.Instance.UnregisterDivision(this);
        }

        if (StrategicSelectionManager.Instance != null)
        {
            StrategicSelectionManager.Instance.DeregisterDivision(this);
        }
    }
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (_display != null)
        {
            _display.SetSelected(selected);
        }
    }

    public void UpdateVisuals()
    {
        if (_display != null)
        {
            _display.SetSymbol(divisionSymbolSprite);
            _display.SetDesignation(divisionName);
            _display.SetStats(currentOrganization / maxOrganization, currentSupply / maxSupply);
        }
    }

    public void IssueMoveOrder(HOI4MapManager.RuntimeProvince destination)
    {
        if (destination == null) return;

        if (currentProvinceID == -1)
        {
            InitializeStartingProvince();
        }

        if (destination.id == currentProvinceID)
        {
            Debug.Log($"[{divisionName}] Already at destination province #{destination.id}.");
            return;
        }

        targetProvinceID = destination.id;
        hasMoveOrder = true;
        legProgress = 0f;

        Debug.Log($"<color=cyan>[{divisionName}] Order confirmed: Moving from #{currentProvinceID} -> #{targetProvinceID}</color>");
    }

    public float CalculateHoursBetweenProvinces(HOI4MapManager.RuntimeProvince from, HOI4MapManager.RuntimeProvince to)
    {
        float dist = Vector3.Distance(from.worldPosition, to.worldPosition);
        float infraMultiplier = 1.0f;

        float supplyDebuff = Mathf.Clamp(currentSupply / maxSupply, 0.3f, 1.0f);
        float effectiveSpeed = baseMovementSpeed * infraMultiplier * supplyDebuff;

        return Mathf.Max(2f, (dist / effectiveSpeed) * 10f);
    }

    public void DrawPathVisual(List<HOI4MapManager.RuntimeProvince> path, int currentStepIndex)
    {
        if (path == null || currentStepIndex >= path.Count)
        {
            _pathLine.positionCount = 0;
            return;
        }

        int remaining = path.Count - currentStepIndex + 1;
        _pathLine.positionCount = remaining;
        _pathLine.SetPosition(0, transform.position + Vector3.up * 0.5f);

        int lineIdx = 1;
        for (int i = currentStepIndex; i < path.Count; i++)
        {
            _pathLine.SetPosition(lineIdx++, path[i].worldPosition + Vector3.up * 0.5f);
        }
    }

    public void ClearPathVisual()
    {
        _pathLine.positionCount = 0;
    }
}