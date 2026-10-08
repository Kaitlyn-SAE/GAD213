using System;
using UnityEngine;
using UnityEngine.InputSystem;
using TheaterStrategy;

public class StrategicCameraController : MonoBehaviour
{
    public enum ViewState { Tactical, Operational, Strategic }

    [Header("Hierarchy References")]
    [SerializeField] private Transform pitchPivot;
    [SerializeField] private Camera cam;

    [Header("Pan Settings")]
    [SerializeField] private float basePanSpeed = 30f;
    [SerializeField] private float panSmoothing = 10f;

    [Header("Zoom Bounds")]
    [SerializeField] private float minHeight = 35f;     
    [SerializeField] private float maxHeight = 1500f;   
    [SerializeField] private float zoomSensitivity = 0.05f;
    [SerializeField] private float zoomDampening = 7.5f;
    private float _battleCooldownTimer = 0f;

    [Header("Thresholds")]
    [SerializeField] private float operationalThreshold = 0.45f;

    [Header("Transition Settings")]
    [Tooltip("Radius around camera center to detect an active battle when zooming in")]
    [SerializeField] private float battleDetectionRadius = 60f;

    public static event Action<ViewState> OnViewStateChanged;

    private float _targetZoom = 0.2f;
    private float _currentZoom = 0.2f;
    private Vector3 _targetPosition;
    private ViewState _currentViewState = ViewState.Operational;

    public ViewState CurrentState => _currentViewState;
    public float CurrentZoomNormalized => _currentZoom;

    private void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>();
        if (pitchPivot == null && cam != null) pitchPivot = cam.transform.parent;
    }

    private void Start()
    {
        _targetPosition = transform.position;
        _targetZoom = Mathf.InverseLerp(minHeight, maxHeight, transform.position.y);
        _currentZoom = _targetZoom;

        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        if (pitchPivot != null) pitchPivot.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void Update()
    {
        if (_battleCooldownTimer > 0f)
        {
            _battleCooldownTimer -= Time.unscaledDeltaTime;
        }

        HandlePanAndZoom();
        UpdateTransform();
        EvaluateViewState();
        CheckBattleZoomTransition();
    }

    private void HandlePanAndZoom()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        float h = 0f;
        float v = 0f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) v += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) v -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) h += 1f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) h -= 1f;

        float heightScale = Mathf.Lerp(1f, 30f, _currentZoom);
        Vector3 moveDir = new Vector3(h, 0, v).normalized;
        _targetPosition += moveDir * (basePanSpeed * heightScale * Time.unscaledDeltaTime);

        float scrollRaw = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scrollRaw) > 0.01f)
        {
            float scroll = Mathf.Sign(scrollRaw);

            if (scroll > 0)
            {
                Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

                if (groundPlane.Raycast(ray, out float hitDist))
                {
                    Vector3 hitPoint = ray.GetPoint(hitDist);
                    _targetPosition.x = Mathf.Lerp(_targetPosition.x, hitPoint.x, 0.12f);
                    _targetPosition.z = Mathf.Lerp(_targetPosition.z, hitPoint.z, 0.12f);
                }
            }

            _targetZoom -= scroll * zoomSensitivity;
            _targetZoom = Mathf.Clamp01(_targetZoom);
        }
    }

    private void UpdateTransform()
    {
        _currentZoom = Mathf.Lerp(_currentZoom, _targetZoom, Time.unscaledDeltaTime * zoomDampening);

        float targetY = Mathf.Lerp(minHeight, maxHeight, _currentZoom * _currentZoom);
        _targetPosition.y = targetY;

        transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.unscaledDeltaTime * panSmoothing);

        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        if (pitchPivot != null) pitchPivot.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private void EvaluateViewState()
    {
        ViewState newState = (_currentZoom >= operationalThreshold) ? ViewState.Strategic : ViewState.Operational;

        if (newState != _currentViewState)
        {
            _currentViewState = newState;
            OnViewStateChanged?.Invoke(_currentViewState);
        }
    }

    public void ResetZoomAfterBattle()
    {
        _targetZoom = 0.35f; 
        _currentZoom = 0.35f;
        _battleCooldownTimer = 2.0f; 
    }

    private void CheckBattleZoomTransition()
    {
        if (_battleCooldownTimer > 0f) return;

        if (_currentZoom <= 0.05f &&
            TacticalTransitionManager.Instance != null &&
            !TacticalTransitionManager.Instance.IsInTacticalBattle &&
            !TacticalTransitionManager.Instance.IsTransitioning)
        {
            if (MacroCombatManager.Instance != null && cam != null)
            {
                Ray ray = cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
                Plane ground = new Plane(Vector3.up, Vector3.zero);

                if (ground.Raycast(ray, out float dist))
                {
                    Vector3 lookTarget = ray.GetPoint(dist);
                    var battle = MacroCombatManager.Instance.GetBattleNearPosition(lookTarget, battleDetectionRadius);

                    if (battle != null)
                    {
                        TacticalTransitionManager.Instance.EnterTacticalBattlefield(battle.province);
                    }
                }
            }
        }
    }
}