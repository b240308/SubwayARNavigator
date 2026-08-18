using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public enum NavigationDirection
{
    None,
    Straight,   // 직진
    TurnLeft,   // 좌회전
    TurnRight,  // 우회전
    OffRoute,   // 경로 이탈
    Arrived     // 도착
}

public class RouteDirectionUI : MonoBehaviour
{
    public static RouteDirectionUI Instance { get; private set; }

    [Header("Dependencies")]
    public AREarthManager earthManager;

    [Header("UI Component")]
    public TextMeshProUGUI guidanceText; // 화면 텍스트 UI

    [Header("Audio UX Settings")]
    public AudioSource audioSource;     // 음성 재생용 AudioSource
    public AudioClip clipStraight;     // "직진하세요"
    public AudioClip clipTurnLeft;     // "왼쪽으로 가세요"
    public AudioClip clipTurnRight;    // "오른쪽으로 가세요"
    public AudioClip clipOffRoute;     // "경로를 이탈했습니다"
    public AudioClip clipArrived;      // "목적지에 도착했습니다"

    [Header("Distance Settings")]
    public float triggerDistance = 8.0f;   // 방향 안내 및 음성 출력 거리 (m)
    public float nodePassDistance = 2.5f;  // 다음 노드로 넘어가는 판단 거리 (m)
    public float offRouteThreshold = 15.0f;// 경로 이탈 판단 거리 (m)

    private List<Vector2> routePoints = new List<Vector2>();
    private int currentTargetIndex = 0;
    private bool isNavigating = false;

    // 음성 중복 재생 방지용 변수
    private NavigationDirection lastAnnouncedDirection = NavigationDirection.None;
    private int lastAnnouncedIndex = -1;
    private bool isOffRouteAnnounced = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void StartGuidance(List<Vector2> points)
    {
        if (points == null || points.Count < 2) return;

        routePoints = new List<Vector2>(points);
        currentTargetIndex = 1; // 0번은 출발지, 1번부터 첫 타겟
        isNavigating = true;

        // 초기화
        lastAnnouncedDirection = NavigationDirection.None;
        lastAnnouncedIndex = -1;
        isOffRouteAnnounced = false;

        StartCoroutine(GuidanceLoop());
    }

    private IEnumerator GuidanceLoop()
    {
        var wait = new WaitForSeconds(0.3f); // 0.3초 주기 체크

        while (isNavigating && currentTargetIndex < routePoints.Count)
        {
            if (earthManager != null && earthManager.EarthTrackingState == TrackingState.Tracking)
            {
                UpdateNavigationState();
            }
            yield return wait;
        }
    }

    private void UpdateNavigationState()
    {
        var cameraPose = earthManager.CameraGeospatialPose;
        Vector2 currentGps = new Vector2((float)cameraPose.Latitude, (float)cameraPose.Longitude);
        Vector2 targetGps = routePoints[currentTargetIndex];

        float distanceToTarget = CalculateDistance(currentGps, targetGps);

        // 1. 경로 이탈 검사 (가장 가까운 노드와의 거리가 thresholds보다 멀 때)
        float minDistanceToRoute = GetMinDistanceToRoute(currentGps);
        if (minDistanceToRoute > offRouteThreshold)
        {
            if (!isOffRouteAnnounced)
            {
                TriggerGuidance(NavigationDirection.OffRoute, minDistanceToRoute);
                isOffRouteAnnounced = true;
            }
            return;
        }
        else
        {
            // 경로 복귀 시 이탈 플래그 리셋
            isOffRouteAnnounced = false;
        }

        // 2. 방향 계산
        NavigationDirection currentDir = CalculateTurnDirection(currentTargetIndex);

        // 3. 안내 범위 진입 및 음성/UI 출력
        if (distanceToTarget <= triggerDistance)
        {
            // 노드가 바꼈거나, 새로운 방향일 때만 1회 음성 출력
            if (lastAnnouncedIndex != currentTargetIndex || lastAnnouncedDirection != currentDir)
            {
                TriggerGuidance(currentDir, distanceToTarget);
                lastAnnouncedIndex = currentTargetIndex;
                lastAnnouncedDirection = currentDir;
            }
            else
            {
                // UI 텍스트만 실시간 거리 갱신
                UpdateUITextOnly(currentDir, distanceToTarget);
            }
        }
        else
        {
            UpdateUITextOnly(NavigationDirection.Straight, distanceToTarget);
        }

        // 4. 노드 통과 (다음 목적지로 이동)
        if (distanceToTarget <= nodePassDistance)
        {
            currentTargetIndex++;

            if (currentTargetIndex >= routePoints.Count)
            {   
                // 1."목적지에 도착했습니다." 텍스트 & 음성 출력
                TriggerGuidance(NavigationDirection.Arrived, 0);
                isNavigating = false;

                // 2. 마지막 판넬의 텍스트들(역/출구 이름, 총 이동거리, 소요시간)을 최종 계산해 업데이트
                if (ArrivalPanelUI.Instance != null)
                {
                    ArrivalPanelUI.Instance.UpdateArrivalUI();
                }

                // 3. ARPanelManager에게 알려서 3초 뒤 'Canvas_Arrived 켜기
                if (ARPanelManager.Instance != null)
                {
                    ARPanelManager.Instance.OnArrived();
                }
            }
        }
    }

    /// <summary>
    /// UI 텍스트 변경 + 음성 1회 재생
    /// </summary>
    private void TriggerGuidance(NavigationDirection dir, float distance)
    {
        UpdateUITextOnly(dir, distance);
        PlayVoice(dir);
    }

    /// <summary>
    /// UI 텍스트 표시
    /// </summary>
    private void UpdateUITextOnly(NavigationDirection dir, float distance)
    {
        if (guidanceText == null) return;

        string message = "";
        switch (dir)
        {
            case NavigationDirection.TurnLeft:
                message = $"<color=#FF0055><b>◀ 왼쪽으로 가세요</b></color>\n<size=80%>{(int)distance}m 앞</size>";
                break;

            case NavigationDirection.TurnRight:
                message = $"<color=#00FFCC><b>오른쪽으로 가세요 ▶</b></color>\n<size=80%>{(int)distance}m 앞</size>";
                break;

            case NavigationDirection.Straight:
                message = $"<color=#FFFF00><b>▲ 직진하세요</b></color>\n<size=80%>{(int)distance}m 앞</size>";
                break;

            case NavigationDirection.OffRoute:
                message = "<color=#FF3333><b>⚠ 경로를 이탈했습니다!</b></color>";
                break;

            case NavigationDirection.Arrived:
                message = "<color=#88F8A1><b>★ 목적지에 도착했습니다 ★</b></color>";
                break;
        }

        guidanceText.text = message;
    }

    /// <summary>
    /// 방향에 맞는 음성 파일 1회 재생
    /// </summary>
    private void PlayVoice(NavigationDirection dir)
    {
        if (audioSource == null) return;

        AudioClip clipToPlay = null;
        switch (dir)
        {
            case NavigationDirection.Straight: clipToPlay = clipStraight; break;
            case NavigationDirection.TurnLeft: clipToPlay = clipTurnLeft; break;
            case NavigationDirection.TurnRight: clipToPlay = clipTurnRight; break;
            case NavigationDirection.OffRoute: clipToPlay = clipOffRoute; break;
            case NavigationDirection.Arrived: clipToPlay = clipArrived; break;
        }

        if (clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay);
        }
    }

    private NavigationDirection CalculateTurnDirection(int targetIndex)
    {
        if (targetIndex >= routePoints.Count - 1) return NavigationDirection.Straight;

        Vector2 p0 = routePoints[targetIndex - 1];
        Vector2 p1 = routePoints[targetIndex];
        Vector2 p2 = routePoints[targetIndex + 1];

        Vector2 dir1 = (p1 - p0).normalized;
        Vector2 dir2 = (p2 - p1).normalized;

        float angle = Vector2.SignedAngle(dir1, dir2);

        if (angle > 20.0f) return NavigationDirection.TurnLeft;
        if (angle < -20.0f) return NavigationDirection.TurnRight;

        return NavigationDirection.Straight;
    }

    private float GetMinDistanceToRoute(Vector2 currentGps)
    {
        float minDst = float.MaxValue;
        for (int i = 0; i < routePoints.Count; i++)
        {
            float dst = CalculateDistance(currentGps, routePoints[i]);
            if (dst < minDst) minDst = dst;
        }
        return minDst;
    }

    private float CalculateDistance(Vector2 g1, Vector2 g2)
    {
        float R = 6371000f;
        float dLat = (g2.x - g1.x) * Mathf.Deg2Rad;
        float dLon = (g2.y - g1.y) * Mathf.Deg2Rad;

        float a = Mathf.Sin(dLat / 2) * Mathf.Sin(dLat / 2) +
                  Mathf.Cos(g1.x * Mathf.Deg2Rad) * Mathf.Cos(g2.x * Mathf.Deg2Rad) *
                  Mathf.Sin(dLon / 2) * Mathf.Sin(dLon / 2);

        float c = 2 * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1 - a));
        return R * c;
    }
}