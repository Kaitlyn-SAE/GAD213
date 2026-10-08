using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance { get; private set; }

    public enum GameSpeed
    {
        Paused = 0,
        Speed1 = 1,  // Normal (1 sec real time = 1 in-game hour)
        Speed2 = 2,  // 2x
        Speed3 = 5,  // 5x
        Speed4 = 10, // 10x
        Speed5 = 25  // 25x Fast Forward
    }

    [Header("Current Time State")]
    [SerializeField] private GameSpeed currentSpeed = GameSpeed.Speed1;
    [SerializeField] private bool isLockedToRealTime = false; 

    [Header("In-Game Calendar (Starting 2032 pre-Flash)")]
    public int year = 2032;
    public int month = 10;
    public int day = 12;
    public int hour = 6;
    [SerializeField] private float secondsPerHour = 1.0f; 

    private float _hourTimer = 0f;
    private GameSpeed _savedSpeedBeforeTactical = GameSpeed.Speed1;

    public static event Action<int, int, int, int> OnHourPassed; 
    public static event Action<GameSpeed> OnSpeedChanged;

    public float EffectiveTimeScale => isLockedToRealTime
        ? (currentSpeed == GameSpeed.Paused ? 0f : 1f)
        : (float)currentSpeed;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        StrategicCameraController.OnViewStateChanged += HandleViewStateChanged;
    }

    private void OnDisable()
    {
        StrategicCameraController.OnViewStateChanged -= HandleViewStateChanged;
    }

    private void Update()
    {
        HandleKeyboardShortcuts();
        AdvanceCalendar();
    }

    private void HandleViewStateChanged(StrategicCameraController.ViewState viewState)
    {
        if (viewState == StrategicCameraController.ViewState.Tactical)
        {
            if (!isLockedToRealTime)
            {
                _savedSpeedBeforeTactical = currentSpeed;
                isLockedToRealTime = true;
                if (currentSpeed != GameSpeed.Paused)
                {
                    SetSpeed(GameSpeed.Speed1);
                }
                Debug.Log("<color=orange>[Time Controller] Tactical View: Time Speed LOCKED to 1.0x Real-Time.</color>");
            }
        }
        else
        {
            if (isLockedToRealTime)
            {
                isLockedToRealTime = false;
                SetSpeed(_savedSpeedBeforeTactical);
                Debug.Log("<color=cyan>[Time Controller] Operational View: Time Controls Restored.</color>");
            }
        }
    }

    private void HandleKeyboardShortcuts()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame)
        {
            if (currentSpeed == GameSpeed.Paused)
            {
                SetSpeed(isLockedToRealTime ? GameSpeed.Speed1 : (_savedSpeedBeforeTactical == GameSpeed.Paused ? GameSpeed.Speed1 : _savedSpeedBeforeTactical));
            }
            else
            {
                _savedSpeedBeforeTactical = currentSpeed;
                SetSpeed(GameSpeed.Paused);
            }
        }

        if (!isLockedToRealTime)
        {
            if (kb.digit1Key.wasPressedThisFrame) SetSpeed(GameSpeed.Speed1);
            if (kb.digit2Key.wasPressedThisFrame) SetSpeed(GameSpeed.Speed2);
            if (kb.digit3Key.wasPressedThisFrame) SetSpeed(GameSpeed.Speed3);
            if (kb.digit4Key.wasPressedThisFrame) SetSpeed(GameSpeed.Speed4);
            if (kb.digit5Key.wasPressedThisFrame) SetSpeed(GameSpeed.Speed5);
        }
    }

    public void SetSpeed(GameSpeed speed)
    {
        if (isLockedToRealTime && speed > GameSpeed.Speed1)
        {
            Debug.LogWarning("[Time Controller] Cannot speed up time while in Tactical View!");
            return;
        }

        currentSpeed = speed;
        OnSpeedChanged?.Invoke(currentSpeed);
    }

    private void AdvanceCalendar()
    {
        if (currentSpeed == GameSpeed.Paused) return;

        float delta = Time.unscaledDeltaTime * EffectiveTimeScale;
        _hourTimer += delta;

        while (_hourTimer >= secondsPerHour)
        {
            _hourTimer -= secondsPerHour;
            hour++;

            if (hour >= 24)
            {
                hour = 0;
                day++;
                if (day > 30) 
                {
                    day = 1;
                    month++;
                    if (month > 12)
                    {
                        month = 1;
                        year++;
                    }
                }
            }

            OnHourPassed?.Invoke(year, month, day, hour);
        }
    }

    public string GetFormattedDate()
    {
        return $"{hour:D2}:00 - {day:D2}/{month:D2}/{year} [Speed: {currentSpeed}]";
    }
}