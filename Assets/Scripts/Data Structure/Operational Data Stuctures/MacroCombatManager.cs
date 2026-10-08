using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using TheaterStrategy;

public class MacroCombatManager : MonoBehaviour
{
    public static MacroCombatManager Instance { get; private set; }

    [System.Serializable]
    public class ActiveBattle
    {
        public HOI4MapManager.RuntimeProvince province;
        public Faction defenderFaction;
        public Faction attackerFaction;
        public GameObject battleIconInstance;
    }

    [Header("Visual Feedback")]
    [Tooltip("World-space icon or prefab spawned at contested province centroid")]
    [SerializeField] private GameObject combatMarkerPrefab;

    private readonly Dictionary<int, ActiveBattle> _activeBattles = new Dictionary<int, ActiveBattle>();

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        HandleDebugShortcut();
    }

    private void HandleDebugShortcut()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null || Camera.main == null) return;

        if (kb.bKey.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                var prov = HOI4MapManager.Instance != null
                    ? HOI4MapManager.Instance.GetClosestProvince(hit.point)
                    : null;

                if (prov != null)
                {
                    if (IsProvinceContested(prov.id))
                    {
                        EndBattle(prov.id);
                        Debug.Log($"<color=orange>[Combat] Ended battle in {prov.provinceName}</color>");
                    }
                    else
                    {
                        StartBattle(prov, Faction.ACMF, Faction.SATO);
                        Debug.Log($"<color=red><b>[COMBAT IGNITED]</b> Active battle started in {prov.provinceName}! Zoom in to engage!</color>");
                    }
                }
            }
        }
    }

    public void StartBattle(HOI4MapManager.RuntimeProvince province, Faction defender, Faction attacker)
    {
        if (province == null || _activeBattles.ContainsKey(province.id)) return;

        GameObject markerObj = null;
        if (combatMarkerPrefab != null)
        {
            markerObj = Instantiate(combatMarkerPrefab, province.worldPosition + Vector3.up * 3f, Quaternion.identity);
        }
        else
        {
            markerObj = new GameObject($"BattleMarker_{province.id}");
            markerObj.transform.position = province.worldPosition + Vector3.up * 3f;

            var canvas = markerObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = markerObj.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(80, 80);
            markerObj.transform.localScale = Vector3.one * 0.08f;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(markerObj.transform, false);
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "⚔";
            tmp.fontSize = 60;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.red;

            markerObj.AddComponent<BillboardToCamera>();
        }

        var battle = new ActiveBattle
        {
            province = province,
            defenderFaction = defender,
            attackerFaction = attacker,
            battleIconInstance = markerObj
        };

        _activeBattles[province.id] = battle;
    }

    public void EndBattle(int provinceID)
    {
        if (_activeBattles.TryGetValue(provinceID, out var battle))
        {
            if (battle.battleIconInstance != null)
            {
                Destroy(battle.battleIconInstance);
            }
            _activeBattles.Remove(provinceID);
        }
    }

    public bool IsProvinceContested(int provinceID) => _activeBattles.ContainsKey(provinceID);

    public ActiveBattle GetBattle(int provinceID)
    {
        _activeBattles.TryGetValue(provinceID, out var b);
        return b;
    }

    public ActiveBattle GetBattleNearPosition(Vector3 worldPos, float radius = 25f)
    {
        foreach (var kvp in _activeBattles)
        {
            if (Vector3.Distance(worldPos, kvp.Value.province.worldPosition) <= radius)
            {
                return kvp.Value;
            }
        }
        return null;
    }
}

public class BillboardToCamera : MonoBehaviour
{
    private Camera _cam;
    private void Start() => _cam = Camera.main;
    private void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam != null) transform.rotation = _cam.transform.rotation;
    }
}