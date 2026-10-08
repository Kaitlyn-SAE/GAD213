using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using TheaterStrategy;
using TheaterStrategy.Tactical;

[RequireComponent(typeof(LineRenderer))]
public class TacticalRTSCommander : MonoBehaviour
{
    public static TacticalRTSCommander Instance { get; private set; }

    [Header("Faction Control")]
    public Faction playerFaction = Faction.SATO;

    [Header("Formations")]
    public TacticalFormation activeFormation = TacticalFormation.Line;
    [SerializeField] private float unitSpacing = 5.0f;

    [Header("Raycast & Layers")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private LayerMask unitLayer = ~0;

    [Header("Selection Box Visuals")]
    [SerializeField] private Color boxFillColor = new Color(0.2f, 0.8f, 0.4f, 0.15f);
    [SerializeField] private Color boxBorderColor = new Color(0.2f, 0.9f, 0.4f, 0.85f);
    [SerializeField] private float borderThickness = 2f;

    private readonly List<TacticalVehicle> _selectedUnits = new List<TacticalVehicle>();
    private Camera _rtsCamera;
    private LineRenderer _vectorPreviewLine;

    private Vector2 _dragSelectStart;
    private bool _isBoxSelecting = false;
    private Vector3 _vectorDragStartGround;
    private bool _isVectorDragging = false;
    private bool _attackMoveModeActive = false;

    public IReadOnlyList<TacticalVehicle> SelectedUnits => _selectedUnits;

    private void Awake()
    {
        Instance = this;
        SetupVectorPreviewLine();
    }

    private void SetupVectorPreviewLine()
    {
        _vectorPreviewLine = GetComponent<LineRenderer>();
        _vectorPreviewLine.startWidth = 0.6f;
        _vectorPreviewLine.endWidth = 0.6f;
        _vectorPreviewLine.material = new Material(Shader.Find("Sprites/Default"));
        _vectorPreviewLine.startColor = new Color(0.2f, 0.9f, 0.4f, 0.8f);
        _vectorPreviewLine.endColor = new Color(1.0f, 0.85f, 0.2f, 0.8f);
        _vectorPreviewLine.positionCount = 0;
    }

    private void OnEnable()
    {
        _rtsCamera = GetComponent<Camera>();
        if (_rtsCamera == null) _rtsCamera = Camera.main;
        ClearSelection();
    }

    private void OnDisable()
    {
        ClearSelection();
        _isBoxSelecting = false;
        _isVectorDragging = false;
        if (_vectorPreviewLine != null) _vectorPreviewLine.positionCount = 0;
    }

    private void Update()
    {
        if (TacticalTransitionManager.Instance != null && !TacticalTransitionManager.Instance.IsInTacticalBattle) return;
        if (_rtsCamera == null) _rtsCamera = Camera.main;
        if (_rtsCamera == null) return;

        HandleHotkeys();
        HandleSelectionInput();
        HandleCommandInput();
    }

    private void HandleHotkeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) { activeFormation = TacticalFormation.Line; Debug.Log("<color=cyan>[Formation] Line Abreast</color>"); }
        if (kb.digit2Key.wasPressedThisFrame) { activeFormation = TacticalFormation.Column; Debug.Log("<color=cyan>[Formation] Column (Convoy)</color>"); }
        if (kb.digit3Key.wasPressedThisFrame) { activeFormation = TacticalFormation.Wedge; Debug.Log("<color=cyan>[Formation] Wedge</color>"); }
        if (kb.digit4Key.wasPressedThisFrame) { activeFormation = TacticalFormation.Box; Debug.Log("<color=cyan>[Formation] Box</color>"); }

        if (kb.tKey.wasPressedThisFrame && _selectedUnits.Count > 0)
        {
            _attackMoveModeActive = true;
            Debug.Log("<color=yellow>[Order] ATTACK-MOVE: Left-click destination.</color>");
        }
    }

    private void HandleSelectionInput()
    {
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse == null) return;

        if (_attackMoveModeActive)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                Ray ray = _rtsCamera.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, 2000f, groundLayer))
                {
                    bool shift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
                    DispatchMoveOrders(hit.point, Vector3.zero, OrderType.AttackMove, shift);
                }
                _attackMoveModeActive = false;
            }
            if (mouse.rightButton.wasPressedThisFrame) _attackMoveModeActive = false; 
            return;
        }

        Vector2 mousePos = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            _dragSelectStart = mousePos;
            _isBoxSelecting = true;
        }

        if (mouse.leftButton.wasReleasedThisFrame && _isBoxSelecting)
        {
            _isBoxSelecting = false;
            bool isShift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            float dist = Vector2.Distance(_dragSelectStart, mousePos);

            if (dist < 10f)
            {
                Ray ray = _rtsCamera.ScreenPointToRay(mousePos);
                if (Physics.Raycast(ray, out RaycastHit hit, 2000f, unitLayer))
                {
                    var vehicle = hit.collider.GetComponentInParent<TacticalVehicle>();
                    if (vehicle != null && vehicle.faction == playerFaction)
                    {
                        if (isShift) ToggleSelectUnit(vehicle);
                        else { ClearSelection(); SelectUnit(vehicle); }
                        return;
                    }
                }
                if (!isShift) ClearSelection();
            }
            else
            {
                if (!isShift) ClearSelection();
                Rect rect = GetScreenRect(_dragSelectStart, mousePos);

                if (BattlefieldSpawner.Instance != null)
                {
                    foreach (var unit in BattlefieldSpawner.Instance.ActiveUnits)
                    {
                        if (unit == null || unit.faction != playerFaction) continue;
                        Vector3 screenPt = _rtsCamera.WorldToScreenPoint(unit.transform.position);
                        if (screenPt.z > 0 && rect.Contains(new Vector2(screenPt.x, screenPt.y)))
                        {
                            SelectUnit(unit);
                        }
                    }
                }
            }
        }
    }

    private void HandleCommandInput()
    {
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse == null || _selectedUnits.Count == 0) return;

        if (mouse.rightButton.wasPressedThisFrame)
        {
            Ray ray = _rtsCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 2000f, groundLayer))
            {
                _vectorDragStartGround = hit.point;
                _isVectorDragging = true;
            }
        }

        if (mouse.rightButton.isPressed && _isVectorDragging)
        {
            Ray ray = _rtsCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 2000f, groundLayer))
            {
                Vector3 currentGround = hit.point;
                if (Vector3.Distance(_vectorDragStartGround, currentGround) > 1.5f)
                {
                    _vectorPreviewLine.positionCount = 2;
                    _vectorPreviewLine.SetPosition(0, _vectorDragStartGround + Vector3.up * 0.3f);
                    _vectorPreviewLine.SetPosition(1, currentGround + Vector3.up * 0.3f);
                }
            }
        }

        if (mouse.rightButton.wasReleasedThisFrame && _isVectorDragging)
        {
            _isVectorDragging = false;
            _vectorPreviewLine.positionCount = 0;

            Ray ray = _rtsCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 2000f, groundLayer))
            {
                Vector3 endGround = hit.point;
                float dragLength = Vector3.Distance(_vectorDragStartGround, endGround);

                bool isReverse = kb != null && kb.rKey.isPressed;
                bool isAttackMove = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);

                OrderType orderType = OrderType.Move;
                if (isReverse) orderType = OrderType.ReverseMove;
                else if (isAttackMove) orderType = OrderType.AttackMove;

                bool isShift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);

                if (dragLength > 2.0f)
                {
                    Vector3 facingDir = (endGround - _vectorDragStartGround).normalized;
                    facingDir.y = 0;
                    DispatchVectorMove(_vectorDragStartGround, facingDir, orderType, isShift);
                }
                else
                {
                    DispatchMoveOrders(hit.point, Vector3.zero, orderType, isShift);
                }
            }
        }
    }

    private void DispatchVectorMove(Vector3 centerPoint, Vector3 facingDirection, OrderType orderType, bool queueOrder)
    {
        int count = _selectedUnits.Count;
        Vector3 lineAcross = Vector3.Cross(Vector3.up, facingDirection).normalized;

        for (int i = 0; i < count; i++)
        {
            var unit = _selectedUnits[i];
            if (unit == null) continue;

            float offsetAcross = (i - (count - 1) * 0.5f) * unitSpacing;
            Vector3 targetPos = centerPoint + (lineAcross * offsetAcross);

            if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 6f, NavMesh.AllAreas))
            {
                targetPos = navHit.position;
            }

            var order = new TacticalOrder
            {
                orderType = orderType,
                destination = targetPos,
                finalFacingDirection = facingDirection
            };

            unit.GiveOrder(order, queueOrder);
        }

        Debug.Log($"<color=lime>[Vector Command] Aligned {count} vehicles along defensive line facing {facingDirection}.</color>");
    }

    private void DispatchMoveOrders(Vector3 destination, Vector3 facing, OrderType orderType, bool queueOrder)
    {
        int count = _selectedUnits.Count;
        Vector3 forward = _rtsCamera.transform.forward; forward.y = 0; forward.Normalize();
        Vector3 right = _rtsCamera.transform.right; right.y = 0; right.Normalize();

        for (int i = 0; i < count; i++)
        {
            var unit = _selectedUnits[i];
            if (unit == null) continue;

            Vector3 targetPos = destination + CalculateFormationOffset(i, count, forward, right);

            if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 6f, NavMesh.AllAreas))
            {
                targetPos = navHit.position;
            }

            var order = new TacticalOrder
            {
                orderType = orderType,
                destination = targetPos,
                finalFacingDirection = facing
            };

            unit.GiveOrder(order, queueOrder);
        }

        Debug.Log($"<color=lime>[{orderType}] Commanded {count} vehicle(s) in {activeFormation} formation.</color>");
    }

    private Vector3 CalculateFormationOffset(int index, int totalCount, Vector3 forward, Vector3 right)
    {
        switch (activeFormation)
        {
            case TacticalFormation.Line:
                float lineX = (index - (totalCount - 1) * 0.5f) * unitSpacing;
                return right * lineX;

            case TacticalFormation.Column:
                float colZ = -index * unitSpacing;
                return forward * colZ;

            case TacticalFormation.Wedge:
                int rank = (index + 1) / 2;
                float side = (index % 2 == 1) ? 1f : -1f;
                if (index == 0) return Vector3.zero;
                return (right * (side * rank * unitSpacing * 0.7f)) - (forward * (rank * unitSpacing * 0.8f));

            case TacticalFormation.Box:
            default:
                int cols = Mathf.CeilToInt(Mathf.Sqrt(totalCount));
                int r = index / cols;
                int c = index % cols;
                return (right * ((c - (cols - 1) * 0.5f) * unitSpacing)) - (forward * (r * unitSpacing));
        }
    }

    public void SelectUnit(TacticalVehicle unit)
    {
        if (unit != null && !_selectedUnits.Contains(unit))
        {
            _selectedUnits.Add(unit);
            unit.SetSelected(true);
        }
    }

    public void DeselectUnit(TacticalVehicle unit)
    {
        if (unit != null && _selectedUnits.Remove(unit))
        {
            unit.SetSelected(false);
        }
    }

    public void ToggleSelectUnit(TacticalVehicle unit)
    {
        if (_selectedUnits.Contains(unit)) DeselectUnit(unit);
        else SelectUnit(unit);
    }

    public void ClearSelection()
    {
        foreach (var u in _selectedUnits) if (u != null) u.SetSelected(false);
        _selectedUnits.Clear();
    }

    private Rect GetScreenRect(Vector2 p1, Vector2 p2)
    {
        float x = Mathf.Min(p1.x, p2.x);
        float y = Mathf.Min(p1.y, p2.y);
        return new Rect(x, y, Mathf.Abs(p1.x - p2.x), Mathf.Abs(p1.y - p2.y));
    }

    private void OnGUI()
    {
        if (!_isBoxSelecting) return;

        Vector2 current = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        Vector2 start = new Vector2(_dragSelectStart.x, Screen.height - _dragSelectStart.y);
        Vector2 cur = new Vector2(current.x, Screen.height - current.y);

        Rect box = new Rect(Mathf.Min(start.x, cur.x), Mathf.Min(start.y, cur.y), Mathf.Abs(start.x - cur.x), Mathf.Abs(start.y - cur.y));

        DrawScreenRect(box, boxFillColor);
        DrawScreenRectBorder(box, borderThickness, boxBorderColor);
    }

    private static Texture2D _whiteTex;
    private static Texture2D WhiteTex
    {
        get
        {
            if (_whiteTex == null) { _whiteTex = new Texture2D(1, 1); _whiteTex.SetPixel(0, 0, Color.white); _whiteTex.Apply(); }
            return _whiteTex;
        }
    }

    private static void DrawScreenRect(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(rect, WhiteTex); GUI.color = Color.white; }
    private static void DrawScreenRectBorder(Rect rect, float thickness, Color color)
    {
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        DrawScreenRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        DrawScreenRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        DrawScreenRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
    }
}