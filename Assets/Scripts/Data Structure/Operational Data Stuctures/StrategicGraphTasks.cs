using System.Collections.Generic;
using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using TheaterStrategy;

namespace TheaterStrategy.AI
{
    [Opsive.Shared.Utility.Description("Returns Success if the StrategicDivision has a pending move order.")]
    public class HasMacroMoveOrder : Conditional
    {
        private StrategicDivision _division;

        public override void OnAwake()
        {
            _division = gameObject.GetComponent<StrategicDivision>();
        }

        public override TaskStatus OnUpdate()
        {
            if (_division == null) return TaskStatus.Failure;
            return _division.hasMoveOrder ? TaskStatus.Success : TaskStatus.Failure;
        }
    }

    [Opsive.Shared.Utility.Description("Handles marching across the province graph, halting and igniting combat upon encountering enemy territory.")]
    public class MarchAlongGraph : Action
    {
        private StrategicDivision _division;
        private List<HOI4MapManager.RuntimeProvince> _currentPath;
        private int _currentPathIndex;
        private int _cachedTargetID = -1;

        private Vector3 _startLegPosition;
        private Vector3 _targetLegPosition;

        public override void OnAwake()
        {
            _division = gameObject.GetComponent<StrategicDivision>();
        }

        public override TaskStatus OnUpdate()
        {
            if (_division == null) return TaskStatus.Failure;

            if (!_division.hasMoveOrder)
            {
                return TaskStatus.Running;
            }

            if (_division.targetProvinceID != _cachedTargetID)
            {
                RecalculatePath();
            }

            if (_currentPath == null || _currentPath.Count == 0 || _currentPathIndex >= _currentPath.Count)
            {
                _division.hasMoveOrder = false;
                _division.ClearPathVisual();
                return TaskStatus.Running;
            }

            var nextProvince = _currentPath[_currentPathIndex];

            if (nextProvince.controllingFaction != Faction.Neutral &&
                nextProvince.controllingFaction != _division.faction)
            {
                if (MacroCombatManager.Instance != null)
                {
                    MacroCombatManager.Instance.StartBattle(nextProvince, nextProvince.controllingFaction, _division.faction);
                }

                _division.hasMoveOrder = false;
                _division.ClearPathVisual();
                _cachedTargetID = -1;

                Debug.Log($"<color=red><b>[CONTACT WITH ENEMY]</b> {_division.divisionName} engaged hostile forces in {nextProvince.provinceName}! Zoom in to command 3D battle.</color>");
                return TaskStatus.Running;
            }

            float timeScale = GameTimeManager.Instance != null
                ? GameTimeManager.Instance.EffectiveTimeScale
                : 1.0f;

            if (timeScale > 0f)
            {
                float hoursPassedThisFrame = Time.unscaledDeltaTime * timeScale;
                _division.legProgress += hoursPassedThisFrame / _division.hoursRequiredForCurrentLeg;

                float t = Mathf.Clamp01(_division.legProgress);
                transform.position = Vector3.Lerp(_startLegPosition, _targetLegPosition, t);

                _division.DrawPathVisual(_currentPath, _currentPathIndex);

                if (_division.legProgress >= 1.0f)
                {
                    var arrivedProvince = _currentPath[_currentPathIndex];
                    _division.currentProvinceID = arrivedProvince.id;
                    transform.position = _targetLegPosition;

                    if (_division.captureTerritoryOnMarch)
                    {
                        arrivedProvince.controllingFaction = _division.faction;
                        if (MapTerritoryRenderer.Instance != null)
                        {
                            MapTerritoryRenderer.Instance.SetProvinceFaction(arrivedProvince.id, _division.faction);
                        }
                    }

                    _currentPathIndex++;

                    if (_currentPathIndex >= _currentPath.Count)
                    {
                        _division.hasMoveOrder = false;
                        _division.ClearPathVisual();
                        _cachedTargetID = -1;
                        Debug.Log($"<color=lime>[{_division.divisionName}] Arrived at {arrivedProvince.provinceName}!</color>");
                        return TaskStatus.Running;
                    }
                    else
                    {
                        SetupCurrentLeg();
                    }
                }
            }

            return TaskStatus.Running;
        }

        private void RecalculatePath()
        {
            _cachedTargetID = _division.targetProvinceID;
            _currentPath = HOI4MapManager.Instance.FindProvincePath(_division.currentProvinceID, _cachedTargetID);

            if (_currentPath != null && _currentPath.Count > 1)
            {
                _currentPathIndex = 1;
                SetupCurrentLeg();
                Debug.Log($"<color=cyan>[{_division.divisionName}] Calculated path across {_currentPath.Count} provinces.</color>");
            }
            else
            {
                _currentPathIndex = 0;
            }
        }

        private void SetupCurrentLeg()
        {
            if (_currentPath == null || _currentPathIndex >= _currentPath.Count) return;

            var currentProv = _currentPath[_currentPathIndex - 1];
            var nextProv = _currentPath[_currentPathIndex];

            _startLegPosition = currentProv.worldPosition + Vector3.up * 1f;
            _targetLegPosition = nextProv.worldPosition + Vector3.up * 1f;

            _division.legProgress = 0f;
            _division.hoursRequiredForCurrentLeg = _division.CalculateHoursBetweenProvinces(currentProv, nextProv);
        }

        public override void OnEnd()
        {
            if (_division != null && !_division.hasMoveOrder)
            {
                _division.ClearPathVisual();
            }
        }
    }
}