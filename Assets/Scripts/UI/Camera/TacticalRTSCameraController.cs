using UnityEngine;
using UnityEngine.InputSystem;

public class TacticalRTSCameraController : MonoBehaviour
{
    [Header("RTS Movement")]
    [SerializeField] private float moveSpeed = 40f;

    [Header("Local RTS Zoom Bounds")]
    [SerializeField] private float minHeight = 12f;  
    [SerializeField] private float maxHeight = 90f;  
    [SerializeField] private float zoomSensitivity = 6f;

    [Header("Rotation & Pitch (Hold MMB)")]
    [SerializeField] private float mouseSensitivity = 0.25f;
    [SerializeField] private float minPitch = 25f;
    [SerializeField] private float maxPitch = 80f;

    private float _currentPitch = 45f;
    private float _currentYaw = 0f;
    private Vector3 _targetPosition;

    private void OnEnable()
    {
        _targetPosition = transform.position;
        _currentPitch = transform.eulerAngles.x;
        _currentYaw = transform.eulerAngles.y;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        float h = 0f;
        float v = 0f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        forward.y = 0; right.y = 0;
        forward.Normalize(); right.Normalize();

        Vector3 move = (forward * v + right * h).normalized * (moveSpeed * Time.unscaledDeltaTime);
        _targetPosition += move;

        if (mouse.middleButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            _currentYaw += delta.x * mouseSensitivity;
            _currentPitch -= delta.y * mouseSensitivity;
            _currentPitch = Mathf.Clamp(_currentPitch, minPitch, maxPitch);
        }

        transform.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);

        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            _targetPosition.y -= Mathf.Sign(scroll) * zoomSensitivity;
            _targetPosition.y = Mathf.Clamp(_targetPosition.y, minHeight, maxHeight);
        }

        transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.unscaledDeltaTime * 10f);
    }
}