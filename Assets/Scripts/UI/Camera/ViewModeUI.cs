using UnityEngine;
using TMPro;

public class ViewModeUI : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI viewText;
    [SerializeField] private TextMeshProUGUI clockText; 

    [Header("Color Schemes")]
    [SerializeField] private Color tacticalColor = new Color(0.2f, 0.9f, 0.4f, 1f);
    [SerializeField] private Color operationalColor = new Color(1.0f, 0.75f, 0.1f, 1f);
    [SerializeField] private Color strategicColor = new Color(1.0f, 0.25f, 0.25f, 1f);

    private void OnEnable()
    {
        StrategicCameraController.OnViewStateChanged += HandleViewStateChanged;
    }

    private void OnDisable()
    {
        StrategicCameraController.OnViewStateChanged -= HandleViewStateChanged;
    }

    private void Start()
    {
        var cameraController = FindAnyObjectByType<StrategicCameraController>();
        if (cameraController != null)
        {
            HandleViewStateChanged(cameraController.CurrentState);
        }
    }

    private void Update()
    {
        if (clockText != null && GameTimeManager.Instance != null)
        {
            clockText.text = GameTimeManager.Instance.GetFormattedDate();
        }
    }

    private void HandleViewStateChanged(StrategicCameraController.ViewState newState)
    {
        if (viewText == null) return;

        switch (newState)
        {
            case StrategicCameraController.ViewState.Tactical:
                viewText.text = "SYS // TACTICAL VIEW [PLATOON/SQUAD]";
                viewText.color = tacticalColor;
                break;

            case StrategicCameraController.ViewState.Operational:
                viewText.text = "SYS // OPERATIONAL VIEW [BRIGADE SECTOR]";
                viewText.color = operationalColor;
                break;

            case StrategicCameraController.ViewState.Strategic:
                viewText.text = "SYS // THEATER OVERVIEW [SATELLITE C4ISR]";
                viewText.color = strategicColor;
                break;
        }
    }
}