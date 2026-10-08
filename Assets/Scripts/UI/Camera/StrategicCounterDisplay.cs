using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TheaterStrategy;

[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(BoxCollider))]
public class StrategicCounterDisplay : MonoBehaviour
{
    [Header("UI Element Hierarchy")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private Image selectionOutline;
    [SerializeField] private Image unitSymbolImage;
    [SerializeField] private TextMeshProUGUI designationText;
    [SerializeField] private TextMeshProUGUI stackCountText;

    [Header("HOI4-Style Stat Sliders (0 to 1)")]
    [SerializeField] private Slider moraleSlider;
    [SerializeField] private Slider supplySlider;

    [Header("Dynamic Zoom Scaling")]
    [SerializeField] private float minWorldScale = 0.02f;
    [SerializeField] private float maxWorldScale = 0.12f;

    [Header("Selection Styling")]
    [SerializeField] private Color selectedColor = new Color(1.0f, 0.85f, 0.2f, 1f);

    public StrategicDivision OwnerDivision { get; private set; }
    public int CurrentStackCount { get; private set; } = 1;

    private CanvasGroup _canvasGroup;
    private Canvas _canvas;
    private RectTransform _rectTransform;
    private BoxCollider _boxCollider;
    private Camera _cam;
    private StrategicCameraController _cameraController;
    private Color _originalFrameColor;
    private bool _isHiddenByCluster = false;

    private readonly Vector3[] _worldCorners = new Vector3[4];

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvas = GetComponent<Canvas>();
        _rectTransform = GetComponent<RectTransform>();
        _boxCollider = GetComponent<BoxCollider>();
        _cam = Camera.main;
        _cameraController = FindAnyObjectByType<StrategicCameraController>();

        OwnerDivision = GetComponentInParent<StrategicDivision>();

        if (frameImage != null) _originalFrameColor = frameImage.color;
        if (selectionOutline != null) selectionOutline.gameObject.SetActive(false);

        SetupHitbox();
    }

    private void SetupHitbox()
    {
        if (_boxCollider != null && _rectTransform != null)
        {
            _boxCollider.size = new Vector3(_rectTransform.rect.width, _rectTransform.rect.height, 2f);
            _boxCollider.center = Vector3.zero;
        }
    }

    private void LateUpdate()
    {
        if (_isHiddenByCluster)
        {
            _canvasGroup.alpha = 0f;
            if (_boxCollider != null) _boxCollider.enabled = false;
            return;
        }

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        if (_boxCollider != null && !_boxCollider.enabled) _boxCollider.enabled = true;

        transform.rotation = _cam.transform.rotation;

        float zoomFactor = _cameraController != null ? _cameraController.CurrentZoomNormalized : 0.5f;
        float currentScale = Mathf.Lerp(minWorldScale, maxWorldScale, zoomFactor);
        transform.localScale = Vector3.one * currentScale;

        if (_cameraController != null && _cameraController.CurrentState == StrategicCameraController.ViewState.Tactical)
        {
            _canvasGroup.alpha = Mathf.InverseLerp(0.02f, 0.15f, zoomFactor);
        }
        else
        {
            _canvasGroup.alpha = 1.0f;
        }
    }

    public float GetActualWorldHeight()
    {
        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        _rectTransform.GetWorldCorners(_worldCorners);
        return Vector3.Distance(_worldCorners[0], _worldCorners[1]);
    }

    public void SetSortingOrder(int order)
    {
        if (_canvas == null) _canvas = GetComponent<Canvas>();
        if (_canvas != null)
        {
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = order;
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (selectionOutline != null) selectionOutline.gameObject.SetActive(isSelected);
        if (frameImage != null) frameImage.color = isSelected ? selectedColor : _originalFrameColor;
    }

    public void SetSymbol(Sprite sprite)
    {
        if (unitSymbolImage != null && sprite != null) unitSymbolImage.sprite = sprite;
    }

    public void SetDesignation(string name)
    {
        if (designationText != null) designationText.text = name;
    }

    public void SetStats(float organizationRatio, float supplyRatio)
    {
        if (moraleSlider != null) moraleSlider.value = Mathf.Clamp01(organizationRatio);
        if (supplySlider != null) supplySlider.value = Mathf.Clamp01(supplyRatio);
    }

    public void SetStackCount(int count)
    {
        CurrentStackCount = count;
        if (stackCountText == null) return;

        if (count > 1)
        {
            stackCountText.gameObject.SetActive(true);
            stackCountText.text = $"x{count}";
        }
        else
        {
            stackCountText.gameObject.SetActive(false);
        }
    }

    public void SetVisible(bool isVisible)
    {
        _isHiddenByCluster = !isVisible;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = isVisible ? 1.0f : 0f;
            _canvasGroup.blocksRaycasts = isVisible;
        }

        if (_boxCollider != null)
        {
            _boxCollider.enabled = isVisible;
        }
    }
}