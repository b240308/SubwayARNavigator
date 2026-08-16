using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// 앵커를 쓰지 않고 AREarthManager.Convert()로 각 노드의 GPS를 로컬 좌표로 변환,
// LineRenderer로 경로를 그린다. VPS 정밀화로 인한 드리프트를 막기 위해 주기적으로 갱신.
public class RouteAnchorSpawner : MonoBehaviour
{
    public AREarthManager earthManager;
    public LineRenderer lineRenderer;   // 인스펙터에서 할당 (anchorManager / arrowPrefab 대체)

    [Header("Geospatial gate")]
    public double horizontalAccuracyThreshold = 10.0; // m, 이 이하일 때만 그리기 시작
    public float trackingTimeout = 30f;               // sec, 트래킹 대기 한계

    [Header("Route rendering")]
    public float refreshInterval = 1.0f;  // 좌표 재변환 주기 (sec)
    public float groundOffset = 1.4f;      // 카메라(눈높이) → 지면 보정 (m)

    [Header("Color Settings")]  // 화살표 컬러 설정 #88F8A1   //b
    [SerializeField]
    private Color routeColor = new Color(0.533f, 0.973f, 0.631f, 1.0f); // #88F8A1 (기본값)

    private List<Vector2> cachedRoute;     // x=latitude, y=longitude

    private bool isARReady = false;
    private bool hasRoute = false;
    private bool isRunning = false;

    private Coroutine routeCo;

    void Awake()    //b
    {
        // Hex Color #88F8A1 파싱 및 LineRenderer 색상 세팅
        if (ColorUtility.TryParseHtmlString("#88F8A1", out Color parsedColor))
        {
            routeColor = parsedColor;
        }

        ApplyLineRendererColor();
    }

    void OnEnable()
    {
        ARStateManager.OnARReady += OnARReady;
    }

    void OnDisable()
    {
        ARStateManager.OnARReady -= OnARReady;
        if (routeCo != null) StopCoroutine(routeCo);
        isRunning = false;
    }

    /// <summary>
    /// LineRenderer 색상을 #88F8A1(Unlit 쨍한 색감)으로 세팅합니다.
    /// </summary>
    private void ApplyLineRendererColor()   //b
    {
        if (lineRenderer == null) return;

        // 조명 영향을 받아 탁해지는 현상을 방지하기 위해 Unlit/Sprites 머티리얼 보정
        if (lineRenderer.sharedMaterial == null || lineRenderer.sharedMaterial.HasProperty("_Color"))
        {
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        // LineRenderer 전체 단색 설정
        lineRenderer.startColor = routeColor;
        lineRenderer.endColor = routeColor;

        // Gradient 적용
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(routeColor, 0.0f), new GradientColorKey(routeColor, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(routeColor.a, 0.0f), new GradientAlphaKey(routeColor.a, 1.0f) }
        );
        lineRenderer.colorGradient = gradient;
    }

    public void SpawnRoute(List<Vector2> routePoints)
    {
        DebugUI.Instance?.Log("ROUTE RECEIVED");

        if (routePoints == null || routePoints.Count == 0)
        {
            DebugUI.Instance?.Log("ROUTE INVALID");
            return;
        }

        cachedRoute = new List<Vector2>(routePoints);
        hasRoute = true;

        // [방향 안내 UI 연결] 경로를 받으면 RouteDirectionUI에도 경로 전달   //b
        if (RouteDirectionUI.Instance != null)
        {
            RouteDirectionUI.Instance.StartGuidance(cachedRoute);
        }

        TryStart();
    }

    private void OnARReady()
    {
        DebugUI.Instance?.Log("AR READY");
        isARReady = true;

        TryStart();
    }

    private void TryStart()
    {
        DebugUI.Instance?.Log("TRY START");

        if (!isARReady) { DebugUI.Instance?.Log("WAIT : AR NOT READY"); return; }
        if (!hasRoute) { DebugUI.Instance?.Log("WAIT : NO ROUTE"); return; }
        if (isRunning) { DebugUI.Instance?.Log("SKIP : ALREADY RUNNING"); return; }

        if (earthManager == null) { DebugUI.Instance?.Log("ERROR : earthManager NULL"); return; }
        if (lineRenderer == null) { DebugUI.Instance?.Log("ERROR : lineRenderer NULL"); return; }
        if (cachedRoute == null) { DebugUI.Instance?.Log("ERROR : cachedRoute NULL"); return; }

        // 색상 확실하게 적용 재확인   //b
        ApplyLineRendererColor();

        routeCo = StartCoroutine(RunRouteLine());
    }

    private IEnumerator RunRouteLine()
    {
        isRunning = true;
        DebugUI.Instance?.Log("WAIT EARTH TRACKING...");

        // 1) Earth 트래킹 + 정확도 확보 대기
        float elapsed = 0f;
        bool ready = false;

        while (elapsed < trackingTimeout)
        {
            if (earthManager.EarthState == EarthState.Enabled &&
                earthManager.EarthTrackingState == TrackingState.Tracking)
            {
                var pose = earthManager.CameraGeospatialPose;
                DebugUI.Instance?.Log($"ACC H={pose.HorizontalAccuracy:F1}");

                if (pose.HorizontalAccuracy <= horizontalAccuracyThreshold)
                {
                    ready = true;
                    break;
                }
            }
            else
            {
                DebugUI.Instance?.Log(
                    $"EARTH STATE={earthManager.EarthState} TRACK={earthManager.EarthTrackingState}");
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!ready)
        {
            DebugUI.Instance?.Log("EARTH TRACKING TIMEOUT - ABORT");
            isRunning = false;
            yield break;
        }

        DebugUI.Instance?.Log("ROUTE LINE START | COUNT : " + cachedRoute.Count);

        // 2) 주기적으로 좌표 재변환 → 선 갱신 (드리프트 보정)
        var wait = new WaitForSeconds(refreshInterval);
        while (isRunning)
        {
            if (earthManager.EarthTrackingState == TrackingState.Tracking)
            {
                UpdateLine();
            }
            yield return wait;
        }
    }

    private void UpdateLine()
    {
        // Vector2엔 고도가 없으므로 카메라 고도에서 눈높이만큼 빼서 지면 기준으로 통일
        double groundAlt = earthManager.CameraGeospatialPose.Altitude - groundOffset;

        var pts = new Vector3[cachedRoute.Count];

        for (int i = 0; i < cachedRoute.Count; i++)
        {
            var geo = new GeospatialPose
            {
                Latitude = cachedRoute[i].x,
                Longitude = cachedRoute[i].y,
                Altitude = groundAlt,
                EunRotation = Quaternion.identity
            };

            // 앵커 없이 GPS → Unity 월드 좌표
            Pose local = earthManager.Convert(geo);
            pts[i] = local.position;
        }

        lineRenderer.positionCount = pts.Length;
        lineRenderer.SetPositions(pts);

        DebugUI.Instance?.Log($"LR SIZE = {lineRenderer.positionCount}");

        // 첫 5개
        for (int i = 0; i < Mathf.Min(pts.Length, 5); i++)
        {
            DebugUI.Instance?.Log($"PT[{i}] = ({pts[i].x:F2}, {pts[i].y:F2}, {pts[i].z:F2})");
        }

        // 마지막 5개
        for (int i = Mathf.Max(0, pts.Length - 5); i < pts.Length; i++)
        {
            DebugUI.Instance?.Log($"PT[{i}] = ({pts[i].x:F2}, {pts[i].y:F2}, {pts[i].z:F2})");
        }
    }
}