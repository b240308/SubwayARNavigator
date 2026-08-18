using UnityEngine;
using TMPro;

public class ArrivalPanelUI : MonoBehaviour
{
    public static ArrivalPanelUI Instance;

    [Header("UI TextMeshPro 연결")]
    public TMP_Text stationInfoText; // 1. 역/출구 정보 텍스트 (예: 서울역 3번 출구)
    public TMP_Text distanceText;    // 2. 총 이동 거리 텍스트 (예: 124m)
    public TMP_Text timeText;        // 3. 소요 시간 텍스트 (예: 2분 15초 / 1시간 5분)

    [Header("카메라 (미할당 시 MainCamera 사용)")]
    public Transform cameraTransform;

    private string currentStationInfo = "목적지 정보 없음";
    private Vector3 lastPosition;
    private float totalDistance = 0f;
    private float startTime;
    private bool isTracking = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        // AR이 시작되는 시점을 감지하여 측정 시작
        ARStateManager.OnARReady += StartTracking;
    }

    private void OnDisable()
    {
        ARStateManager.OnARReady -= StartTracking;
    }

    private void Start()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        // 백업용 자동 시작
        StartTracking();
    }

    /// <summary>
    /// VoiceInputManager 등 외부에서 파싱된 역/출구 텍스트 전달받아 저장
    /// </summary>
    public void SetDestinationInfo(string station, int exit)
    {
        if (exit > 0)
        {
            currentStationInfo = $"{station}역 {exit}번 출구";
        }
        else
        {
            currentStationInfo = $"{station}역";
        }
    }

    /// <summary>
    /// 추적 및 시간/거리 측정 시작
    /// </summary>
    public void StartTracking()
    {
        if (cameraTransform != null)
        {
            lastPosition = cameraTransform.position;
        }
        startTime = Time.time;
        totalDistance = 0f;
        isTracking = true;
    }

    private void Update()
    {
        if (!isTracking || cameraTransform == null) return;

        // 프레임 간 이동 거리 계산 및 미세한 센서 떨림 방지(1cm 이상 움직일 때만)
        float moveDelta = Vector3.Distance(cameraTransform.position, lastPosition);
        if (moveDelta > 0.01f)
        {
            totalDistance += moveDelta;
            lastPosition = cameraTransform.position;
        }
    }

    /// <summary>
    /// 마지막 판넬을 활성화하기 직전에 호출하여 TextMeshPro 내용 업데이트
    /// </summary>
    public void UpdateArrivalUI()
    {
        isTracking = false; // 추적 중지
        float elapsedTime = Time.time - startTime;

        // 1. 역 & 출구 텍스트
        if (stationInfoText != null)
        {
            stationInfoText.text = currentStationInfo;
        }

        // 2. 총 이동 거리 (m 단위, 정수 표기)
        if (distanceText != null)
        {
            distanceText.text = $"{Mathf.RoundToInt(totalDistance)}m";
        }

        // 3. 소요 시간 (초 / 분 / 시간 조건부 표기)
        if (timeText != null)
        {
            int totalSeconds = Mathf.FloorToInt(elapsedTime);
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;

            if (hours > 0)
            {
                timeText.text = $"{hours}시간 {minutes}분";
            }
            else if (minutes > 0)
            {
                timeText.text = $"{minutes}분 {seconds}초";
            }
            else
            {
                timeText.text = $"{seconds}초";
            }
        }
    }
}