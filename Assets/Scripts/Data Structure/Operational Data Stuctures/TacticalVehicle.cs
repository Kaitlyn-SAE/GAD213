using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TheaterStrategy;
using TheaterStrategy.Tactical;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(BoxCollider))]
public class TacticalVehicle : MonoBehaviour
{
    [Header("Identity & Team")]
    public string vehicleName = "Type-10 MBT";
    public Faction faction = Faction.SATO;
    public bool isSelected = false;

    [Header("Combat Stats")]
    public float currentHealth = 250f;
    public float maxHealth = 250f;
    public float attackRange = 30f;
    public float attackDamage = 45f;

    [Header("Steering & Reverse Gear")]
    [SerializeField] private float baseMoveSpeed = 12f;
    [SerializeField] private float reverseSpeedMultiplier = 0.65f;
    [SerializeField] private float rotationSpeed = 120f;
    [SerializeField] private float stoppingThreshold = 1.0f;

    [Header("Visual Elements")]
    [SerializeField] private GameObject selectionRing;
    [SerializeField] private MeshRenderer bodyRenderer;

    private NavMeshAgent _agent;
    private Color _factionBaseColor;

    private readonly Queue<TacticalOrder> _orderQueue = new Queue<TacticalOrder>();
    private TacticalOrder _currentOrder;
    private bool _hasActiveOrder = false;
    private bool _isReversing = false;
    private bool _isAttackMoving = false;
    private bool _isAligningFacing = false;

    public bool HasActiveOrder => _hasActiveOrder;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (bodyRenderer == null) bodyRenderer = GetComponent<MeshRenderer>();
        if (selectionRing != null) selectionRing.SetActive(false);

        if (_agent != null)
        {
            _agent.speed = baseMoveSpeed;
            _agent.stoppingDistance = stoppingThreshold;
        }
    }

    public void InitializeVehicle(Faction unitFaction)
    {
        faction = unitFaction;

        _factionBaseColor = faction switch
        {
            Faction.ACMF => new Color(0.9f, 0.2f, 0.2f),
            Faction.SATO => new Color(0.1f, 0.85f, 0.65f),
            Faction.NATO => new Color(0.25f, 0.55f, 1.0f),
            _ => Color.gray
        };

        if (bodyRenderer != null) bodyRenderer.material.color = _factionBaseColor;

        if (_agent != null && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            _agent.Warp(hit.position);
        }
    }

    private void Update()
    {
        if (!_agent.enabled || !_agent.isOnNavMesh) return;

        UpdateTerrainSpeed();

        if (_hasActiveOrder)
        {
            ExecuteCurrentOrder();
        }
    }

    public void GiveOrder(TacticalOrder order, bool queueOrder)
    {
        if (!queueOrder)
        {
            _orderQueue.Clear();
            StartOrder(order);
        }
        else
        {
            if (!_hasActiveOrder)
            {
                StartOrder(order);
            }
            else
            {
                _orderQueue.Enqueue(order);
            }
        }
    }

    private void StartOrder(TacticalOrder order)
    {
        _currentOrder = order;
        _hasActiveOrder = true;
        _isAligningFacing = false;

        _isReversing = (order.orderType == OrderType.ReverseMove);
        _isAttackMoving = (order.orderType == OrderType.AttackMove);

        _agent.isStopped = false;
        _agent.speed = _isReversing ? (baseMoveSpeed * reverseSpeedMultiplier) : baseMoveSpeed;

        _agent.updateRotation = !_isReversing;
        _agent.SetDestination(order.destination);
    }

    private void ExecuteCurrentOrder()
    {
        if (_isAttackMoving)
        {
            var target = ScanForHostileTarget();
            if (target != null)
            {
                _agent.isStopped = true;
                Vector3 toTarget = (target.transform.position - transform.position);
                toTarget.y = 0;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation,
                        Quaternion.LookRotation(toTarget),
                        rotationSpeed * Time.deltaTime
                    );
                }
                return;
            }
            else
            {
                _agent.isStopped = false;
            }
        }

        if (_isReversing)
        {
            Vector3 vel = _agent.desiredVelocity;
            if (vel.sqrMagnitude > 0.1f)
            {
                Vector3 faceThreat = -vel.normalized;
                faceThreat.y = 0;
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(faceThreat),
                    rotationSpeed * Time.deltaTime
                );
            }
        }

        if (!_agent.pathPending && _agent.remainingDistance <= stoppingThreshold)
        {
            if (_currentOrder.finalFacingDirection.sqrMagnitude > 0.01f && !_isAligningFacing)
            {
                _isAligningFacing = true;
                _agent.isStopped = true;
                _agent.updateRotation = false;
            }

            if (_isAligningFacing)
            {
                Quaternion targetRot = Quaternion.LookRotation(_currentOrder.finalFacingDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);

                if (Quaternion.Angle(transform.rotation, targetRot) < 2f)
                {
                    CompleteOrder();
                }
            }
            else
            {
                CompleteOrder();
            }
        }
    }

    private void CompleteOrder()
    {
        _agent.isStopped = true;
        _agent.updateRotation = true;

        if (_orderQueue.Count > 0)
        {
            StartOrder(_orderQueue.Dequeue());
        }
        else
        {
            _hasActiveOrder = false;
            _isReversing = false;
            _isAttackMoving = false;
            _isAligningFacing = false;
        }
    }

    private TacticalVehicle ScanForHostileTarget()
    {
        if (BattlefieldSpawner.Instance == null) return null;

        TacticalVehicle closest = null;
        float minDist = attackRange;

        foreach (var unit in BattlefieldSpawner.Instance.ActiveUnits)
        {
            if (unit == null || unit == this || unit.faction == faction) continue;

            float dist = Vector3.Distance(transform.position, unit.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = unit;
            }
        }
        return closest;
    }

    private void UpdateTerrainSpeed()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
        {
            float terrainMult = (hit.mask == 8) ? 1.4f : 1.0f;
            float targetSpeed = (_isReversing ? baseMoveSpeed * reverseSpeedMultiplier : baseMoveSpeed) * terrainMult;
            _agent.speed = targetSpeed;
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectionRing != null) selectionRing.SetActive(selected);
        else if (bodyRenderer != null) bodyRenderer.material.color = selected ? new Color(1.0f, 0.85f, 0.2f) : _factionBaseColor;
    }
}