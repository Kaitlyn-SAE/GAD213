using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TheaterStrategy;

public class StrategicSelectionManager : MonoBehaviour
{
    public static StrategicSelectionManager Instance { get; private set; }

    [Header("Raycast Setup")]
    [SerializeField] private LayerMask clickableLayers = ~0;

    private readonly List<StrategicDivision> _selectedDivisions = new List<StrategicDivision>();
    private Camera _cam;
    private StrategicCameraController _camController;

    public IReadOnlyList<StrategicDivision> SelectedDivisions => _selectedDivisions;

    private void Awake()
    {
        Instance = this;
        _cam = Camera.main;
        _camController = FindAnyObjectByType<StrategicCameraController>();
    }

    private void Start()
    {
        if (HOI4MapManager.Instance != null)
        {
            HOI4MapManager.Instance.OnProvinceRightClicked += HandleProvinceRightClicked;
        }
    }

    private void OnDestroy()
    {
        if (HOI4MapManager.Instance != null)
        {
            HOI4MapManager.Instance.OnProvinceRightClicked -= HandleProvinceRightClicked;
        }
    }

    private void Update()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        if (TacticalTransitionManager.Instance != null && TacticalTransitionManager.Instance.IsInTacticalBattle)
            return;

        HandleMacroSelectionInput();
    }

    private void HandleMacroSelectionInput()
    {
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            Ray ray = _cam.ScreenPointToRay(mouse.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, 5000f, clickableLayers))
            {
                StrategicDivision division = hit.collider.GetComponentInParent<StrategicDivision>();
                StrategicCounterDisplay counterHit = hit.collider.GetComponent<StrategicCounterDisplay>();
                if (counterHit != null && division == null) division = counterHit.OwnerDivision;

                if (division != null)
                {
                    bool isShiftHeld = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                    bool isMergedStack = counterHit != null && counterHit.CurrentStackCount > 1;

                    if (isMergedStack && DivisionClusterManager.Instance != null)
                    {
                        var cluster = DivisionClusterManager.Instance.GetClusterForDivision(division);
                        if (!isShiftHeld) ClearSelection();
                        foreach (var d in cluster) SelectDivision(d);
                    }
                    else
                    {
                        if (isShiftHeld)
                        {
                            if (_selectedDivisions.Contains(division)) DeselectDivision(division);
                            else SelectDivision(division);
                        }
                        else
                        {
                            ClearSelection();
                            SelectDivision(division);
                        }
                    }
                    return;
                }
            }

            bool shiftHeld = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            if (!shiftHeld) ClearSelection();
        }
    }

    private void HandleProvinceRightClicked(HOI4MapManager.RuntimeProvince targetProvince)
    {
        if (TacticalTransitionManager.Instance != null && TacticalTransitionManager.Instance.IsInTacticalBattle)
            return;

        if (_selectedDivisions.Count == 0 || targetProvince == null) return;

        Debug.Log($"<color=lime>[Command Dispatch] Ordering {_selectedDivisions.Count} division(s) to march to {targetProvince.provinceName} (#{targetProvince.id})</color>");
        foreach (var div in _selectedDivisions)
        {
            if (div != null)
            {
                div.IssueMoveOrder(targetProvince);
            }
        }
    }

    public void SelectDivision(StrategicDivision division)
    {
        if (division != null && !_selectedDivisions.Contains(division))
        {
            _selectedDivisions.Add(division);
            division.SetSelected(true);
        }
    }

    public void DeselectDivision(StrategicDivision division)
    {
        if (division != null && _selectedDivisions.Remove(division))
        {
            division.SetSelected(false);
        }
    }

    public void ClearSelection()
    {
        foreach (var div in _selectedDivisions)
        {
            if (div != null) div.SetSelected(false);
        }
        _selectedDivisions.Clear();
    }

    public void DeregisterDivision(StrategicDivision division)
    {
        DeselectDivision(division);
    }
}