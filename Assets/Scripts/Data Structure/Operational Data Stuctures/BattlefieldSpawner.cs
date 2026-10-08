using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TheaterStrategy;

public class BattlefieldSpawner : MonoBehaviour
{
    public static BattlefieldSpawner Instance { get; private set; }

    [Header("Unit Prefab Variants")]
    [Tooltip("Default MBT/Tank prefab")]
    [SerializeField] private GameObject tankPrefab;
    [Tooltip("Default IFV/APC prefab")]
    [SerializeField] private GameObject ifvPrefab;

    [Header("Platoon Composition (Per Side)")]
    [SerializeField] private int tanksPerSide = 2;
    [SerializeField] private int ifvsPerSide = 2;
    [SerializeField] private float formationSpacing = 6.0f;

    private readonly List<TacticalVehicle> _activeSpawnedUnits = new List<TacticalVehicle>();
    private Transform _spawnedUnitsContainer;

    public IReadOnlyList<TacticalVehicle> ActiveUnits => _activeSpawnedUnits;

    private void Awake()
    {
        Instance = this;
        var containerObj = new GameObject("[Spawned_Tactical_Units]");
        _spawnedUnitsContainer = containerObj.transform;
        _spawnedUnitsContainer.SetParent(transform);
    }

    public void SpawnBattleForces(MacroCombatManager.ActiveBattle battle, TacticalBattlefieldScene battlefield)
    {
        ClearBattlefield();

        if (battle == null || battlefield == null)
        {
            Debug.LogError("[BattlefieldSpawner] Cannot spawn forces: Battle or Battlefield data is null!");
            return;
        }

        if (battlefield.friendlySpawnPoint != null)
        {
            SpawnFormation(
                battlefield.friendlySpawnPoint,
                battle.attackerFaction,
                "Attacker",
                isPlayerControlled: true
            );
        }

        if (battlefield.enemySpawnPoint != null)
        {
            SpawnFormation(
                battlefield.enemySpawnPoint,
                battle.defenderFaction,
                "Defender",
                isPlayerControlled: false
            );
        }

        Debug.Log($"<color=lime>[Battlefield Spawner] Deployed {_activeSpawnedUnits.Count} tactical vehicles into 3D combat!</color>");
    }

    private void SpawnFormation(Transform spawnAnchor, Faction faction, string labelPrefix, bool isPlayerControlled)
    {
        var unitPrefabs = new List<GameObject>();

        for (int i = 0; i < tanksPerSide; i++) unitPrefabs.Add(tankPrefab);
        for (int i = 0; i < ifvsPerSide; i++) unitPrefabs.Add(ifvPrefab);

        int totalUnits = unitPrefabs.Count;
        Vector3 forward = spawnAnchor.forward;
        Vector3 right = spawnAnchor.right;

        for (int i = 0; i < totalUnits; i++)
        {
            var prefabToSpawn = unitPrefabs[i] != null ? unitPrefabs[i] : tankPrefab;
            if (prefabToSpawn == null) continue;

            float row = i / 2;
            float col = (i % 2) - 0.5f;

            Vector3 offset = (right * (col * formationSpacing)) - (forward * (row * formationSpacing));
            Vector3 targetSpawnPos = spawnAnchor.position + offset;

            if (NavMesh.SamplePosition(targetSpawnPos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                targetSpawnPos = hit.position;
            }

            GameObject unitObj = Instantiate(prefabToSpawn, targetSpawnPos, spawnAnchor.rotation, _spawnedUnitsContainer);
            unitObj.name = $"{labelPrefix}_{faction}_{unitObj.name}";

            var vehicleComp = unitObj.GetComponent<TacticalVehicle>();
            if (vehicleComp == null) vehicleComp = unitObj.AddComponent<TacticalVehicle>();

            vehicleComp.InitializeVehicle(faction);
            _activeSpawnedUnits.Add(vehicleComp);
        }
    }

    public void ClearBattlefield()
    {
        foreach (var unit in _activeSpawnedUnits)
        {
            if (unit != null)
            {
                Destroy(unit.gameObject);
            }
        }

        _activeSpawnedUnits.Clear();
    }
}