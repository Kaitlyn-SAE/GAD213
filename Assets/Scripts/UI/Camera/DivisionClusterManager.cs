using System.Collections.Generic;
using UnityEngine;

public class DivisionClusterManager : MonoBehaviour
{
    public static DivisionClusterManager Instance { get; private set; }

    public enum StackDirection { Downward, Upward }

    [Header("Clustering Settings")]
    [Tooltip("Above this zoom level, divisions in the same province merge into 1 counter")]
    [SerializeField] private float clusterZoomThreshold = 0.35f;

    [Header("HOI4 Stacking Layout")]
    [Tooltip("Downward makes Card 0 sit on the province pin, and subsequent cards hang below it like a list.")]
    [SerializeField] private StackDirection stackDirection = StackDirection.Downward;

    [Tooltip("Subtle gap in world units between cards (0 = seamless flush contact)")]
    [SerializeField] private float gapWorldUnits = 0.2f;

    [Tooltip("Physical distance tolerance to group divisions if province IDs vary slightly")]
    [SerializeField] private float clusterDistanceTolerance = 15.0f;

    private readonly List<StrategicDivision> _allDivisions = new List<StrategicDivision>();
    private StrategicCameraController _camController;
    private Camera _cam;

    private void Awake()
    {
        Instance = this;
        _cam = Camera.main;
        _camController = FindAnyObjectByType<StrategicCameraController>();
    }

    private void Start()
    {
        var foundDivisions = FindObjectsByType<StrategicDivision>(FindObjectsSortMode.None);
        foreach (var div in foundDivisions)
        {
            RegisterDivision(div);
        }
    }

    public void RegisterDivision(StrategicDivision division)
    {
        if (!_allDivisions.Contains(division))
        {
            _allDivisions.Add(division);
        }
    }

    public void UnregisterDivision(StrategicDivision division)
    {
        _allDivisions.Remove(division);
    }

    private void LateUpdate()
    {
        UpdateDivisionClustering();
    }

    private void UpdateDivisionClustering()
    {
        if (_allDivisions.Count == 0) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        float currentZoom = _camController != null ? _camController.CurrentZoomNormalized : 0.5f;
        bool isZoomedOut = currentZoom >= clusterZoomThreshold;

        var clusters = new List<List<StrategicDivision>>();
        var processed = new HashSet<StrategicDivision>();

        for (int i = 0; i < _allDivisions.Count; i++)
        {
            var divA = _allDivisions[i];
            if (divA == null || processed.Contains(divA)) continue;

            var cluster = new List<StrategicDivision> { divA };
            processed.Add(divA);

            for (int j = i + 1; j < _allDivisions.Count; j++)
            {
                var divB = _allDivisions[j];
                if (divB == null || processed.Contains(divB)) continue;

                bool sameProvince = (divA.currentProvinceID != -1 && divA.currentProvinceID == divB.currentProvinceID);
                bool physicallyClose = Vector3.Distance(divA.transform.position, divB.transform.position) <= clusterDistanceTolerance;

                if (sameProvince || physicallyClose)
                {
                    cluster.Add(divB);
                    processed.Add(divB);
                }
            }

            clusters.Add(cluster);
        }

        Vector3 cameraUp = _cam.transform.up;
        Vector3 cameraForward = _cam.transform.forward;

        foreach (var cluster in clusters)
        {
            int stackCount = cluster.Count;
            Vector3 anchorPosition = cluster[0].transform.position + Vector3.up * 1f;

            if (isZoomedOut && stackCount > 1)
            {
                for (int i = 0; i < stackCount; i++)
                {
                    var div = cluster[i];
                    var counter = div.GetComponentInChildren<StrategicCounterDisplay>(true);

                    if (counter != null)
                    {
                        if (i == 0)
                        {
                            counter.SetVisible(true);
                            counter.SetStackCount(stackCount);
                            counter.SetSortingOrder(100);
                            counter.transform.position = anchorPosition;
                        }
                        else
                        {
                            counter.SetVisible(false);
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < stackCount; i++)
                {
                    var div = cluster[i];
                    var counter = div.GetComponentInChildren<StrategicCounterDisplay>(true);

                    if (counter != null)
                    {
                        counter.SetVisible(true);
                        counter.SetStackCount(1);

                        counter.SetSortingOrder(100 + (stackDirection == StackDirection.Downward ? (stackCount - i) : i));

                        if (stackCount > 1)
                        {
                            float cardHeight = counter.GetActualWorldHeight();
                            float stepDistance = cardHeight + gapWorldUnits;

                            float dir = stackDirection == StackDirection.Upward ? 1f : -1f;
                            Vector3 stackOffset = cameraUp * (i * stepDistance * dir);

                            Vector3 depthBias = -cameraForward * (i * 0.02f);

                            counter.transform.position = anchorPosition + stackOffset + depthBias;
                        }
                        else
                        {
                            counter.transform.position = anchorPosition;
                        }
                    }
                }
            }
        }
    }

    public List<StrategicDivision> GetClusterForDivision(StrategicDivision division)
    {
        var result = new List<StrategicDivision>();
        if (division == null) return result;

        foreach (var div in _allDivisions)
        {
            if (div == null) continue;

            bool sameProvince = (div.currentProvinceID != -1 && div.currentProvinceID == division.currentProvinceID);
            bool physicallyClose = Vector3.Distance(div.transform.position, division.transform.position) <= clusterDistanceTolerance;

            if (sameProvince || physicallyClose)
            {
                result.Add(div);
            }
        }

        return result;
    }

}