using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using TheaterStrategy;

public class TacticalTransitionManager : MonoBehaviour
{
    public static TacticalTransitionManager Instance { get; private set; }

    [Header("Registered RTS Environments")]
    [SerializeField] private List<TacticalBattlefieldScene> battlefieldVariants = new List<TacticalBattlefieldScene>();

    [Header("Camera Rigs")]
    [SerializeField] private GameObject strategicCameraRig;
    [SerializeField] private GameObject tacticalRTSCamera;

    [Header("UI Header Reference")]
    [SerializeField] private TextMeshProUGUI hudBannerText;

    [Header("Glitch Effect Duration")]
    [SerializeField] private float glitchDuration = 0.6f;

    private bool _isInTacticalBattle = false;
    private bool _isTransitioning = false;
    private TacticalBattlefieldScene _activeBattlefield = null;
    private MacroCombatManager.ActiveBattle _currentBattleData = null;

    private Canvas _glitchCanvas;
    private RawImage _staticImage;
    private CanvasGroup _glitchGroup;
    private Texture2D _noiseTexture;

    public bool IsInTacticalBattle => _isInTacticalBattle;
    public bool IsTransitioning => _isTransitioning;

    private void Awake()
    {
        Instance = this;
        CreateGlitchOverlayUI();

        foreach (var battlefield in battlefieldVariants)
        {
            if (battlefield.environmentRoot != null)
            {
                battlefield.environmentRoot.SetActive(false);
            }
        }

        if (tacticalRTSCamera != null) tacticalRTSCamera.SetActive(false);
    }

    private void CreateGlitchOverlayUI()
    {
        var canvasObj = new GameObject("[GlitchOverlay_UI]");
        canvasObj.transform.SetParent(transform);

        _glitchCanvas = canvasObj.AddComponent<Canvas>();
        _glitchCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _glitchCanvas.sortingOrder = 9999;

        _glitchGroup = canvasObj.AddComponent<CanvasGroup>();
        _glitchGroup.alpha = 0f;
        _glitchGroup.blocksRaycasts = false;

        var imgObj = new GameObject("StaticNoise");
        imgObj.transform.SetParent(canvasObj.transform, false);

        var rt = imgObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        _staticImage = imgObj.AddComponent<RawImage>();
        _staticImage.color = new Color(0.9f, 0.95f, 1f, 0.95f);

        _noiseTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        _noiseTexture.filterMode = FilterMode.Point;
        _staticImage.texture = _noiseTexture;
        UpdateNoiseTexture();
    }

    private void UpdateNoiseTexture()
    {
        Color32[] pixels = new Color32[128 * 128];
        for (int i = 0; i < pixels.Length; i++)
        {
            byte val = (byte)Random.Range(20, 240);
            pixels[i] = new Color32(val, val, val, 255);
        }
        _noiseTexture.SetPixels32(pixels);
        _noiseTexture.Apply();
    }

    private void Update()
    {
        if (_isInTacticalBattle && !_isTransitioning)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                ExitTacticalBattlefield();
            }
        }
    }

    public void EnterTacticalBattlefield(MacroCombatManager.ActiveBattle battle)
    {
        if (_isInTacticalBattle || _isTransitioning || battle == null) return;

        _currentBattleData = battle;

        _activeBattlefield = battlefieldVariants.Count > 0 ? battlefieldVariants[0] : null;

        if (_activeBattlefield == null || _activeBattlefield.environmentRoot == null)
        {
            Debug.LogError("[TacticalTransitionManager] No battlefield environment registered!");
            return;
        }

        StartCoroutine(TransitionRoutine(true));
    }

    public void EnterTacticalBattlefield(HOI4MapManager.RuntimeProvince contestedProvince)
    {
        if (MacroCombatManager.Instance != null && contestedProvince != null)
        {
            var battle = MacroCombatManager.Instance.GetBattle(contestedProvince.id);
            if (battle != null)
            {
                EnterTacticalBattlefield(battle);
                return;
            }
        }

        Debug.LogWarning("[TacticalTransitionManager] No active battle found in that province!");
    }

    public void ExitTacticalBattlefield()
    {
        if (!_isInTacticalBattle || _isTransitioning) return;
        StartCoroutine(TransitionRoutine(false));
    }



    private IEnumerator TransitionRoutine(bool enteringTactical)
    {
        _isTransitioning = true;

        float elapsed = 0f;
        float halfDuration = glitchDuration * 0.5f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            UpdateNoiseTexture();
            _glitchGroup.alpha = Mathf.Clamp01(elapsed / halfDuration);
            yield return null;
        }

        _glitchGroup.alpha = 1.0f;

        if (enteringTactical)
        {
            _isInTacticalBattle = true;

            if (strategicCameraRig != null) strategicCameraRig.SetActive(false);
            if (_activeBattlefield.environmentRoot != null) _activeBattlefield.environmentRoot.SetActive(true);

            if (tacticalRTSCamera != null)
            {
                tacticalRTSCamera.SetActive(true);
                if (_activeBattlefield.tacticalCameraSpawnPoint != null)
                {
                    tacticalRTSCamera.transform.position = _activeBattlefield.tacticalCameraSpawnPoint.position;
                    tacticalRTSCamera.transform.rotation = _activeBattlefield.tacticalCameraSpawnPoint.rotation;
                }
            }

            if (BattlefieldSpawner.Instance != null && _currentBattleData != null)
            {
                BattlefieldSpawner.Instance.SpawnBattleForces(_currentBattleData, _activeBattlefield);
            }

            if (hudBannerText != null)
            {
                hudBannerText.text = $"SYS // TACTICAL VIEW [{_currentBattleData.attackerFaction} vs {_currentBattleData.defenderFaction}]";
                hudBannerText.color = new Color(0.2f, 0.9f, 0.4f, 1f);
            }

            Debug.Log($"<color=cyan>[C4ISR Feed] Connected to Tactical Drone Feed: {_activeBattlefield.battlefieldName}</color>");
        }
        else
        {
            _isInTacticalBattle = false;

            if (BattlefieldSpawner.Instance != null)
            {
                BattlefieldSpawner.Instance.ClearBattlefield();
            }

            if (tacticalRTSCamera != null) tacticalRTSCamera.SetActive(false);
            if (_activeBattlefield != null && _activeBattlefield.environmentRoot != null)
            {
                _activeBattlefield.environmentRoot.SetActive(false);
            }

            if (strategicCameraRig != null)
            {
                strategicCameraRig.SetActive(true);

                var camController = strategicCameraRig.GetComponent<StrategicCameraController>();
                if (camController != null)
                {
                    camController.ResetZoomAfterBattle();
                }
            }

            if (hudBannerText != null)
            {
                hudBannerText.text = "SYS // OPERATIONAL VIEW [BRIGADE SECTOR]";
                hudBannerText.color = new Color(1.0f, 0.75f, 0.1f, 1f);
            }

            Debug.Log("<color=orange>[C4ISR Feed] Restored Theater Satellite Feed.</color>");
        }

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            UpdateNoiseTexture();
            _glitchGroup.alpha = Mathf.Clamp01(1f - (elapsed / halfDuration));
            yield return null;
        }

        _glitchGroup.alpha = 0f;
        _isTransitioning = false;
    }
}