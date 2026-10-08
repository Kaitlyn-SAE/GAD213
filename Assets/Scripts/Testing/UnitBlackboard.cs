using UnityEngine;
using UnityEngine.AI;

public class UnitBlackboard : MonoBehaviour
{
    [Header("Faction / ID")]
    public string unitName = "ACMF Type-99 MBT";
    public bool isPlayerControlled = true;

    [Header("Status")]
    public float currentHealth = 100f;
    public float maxHealth = 100f;
    public bool isUnderFire = false;

    [Header("Orders & Navigation")]
    public bool hasMoveOrder = false;
    public Vector3 targetDestination;
    public float stoppingDistance = 2.0f;

    [HideInInspector] public NavMeshAgent navAgent;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(UnityEngine.InputSystem.Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                targetDestination = hit.point;
                hasMoveOrder = true;
            }
        }
    }
}