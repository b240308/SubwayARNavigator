using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ARPanelManager : MonoBehaviour
{
    public static ARPanelManager Instance;

    [Header("UI Panels")]   //b
    public GameObject Canvas_Start; //b //시작 패널 Canvas
    public GameObject Canvas_Voice; //b //음성 입력 패널 Canvas
    public GameObject Canvas_Arrived; //b   // [추가] 도착 완료 패널 Canvas

    [Header("Facility UI Text")]    //b
    public TMP_Text titleText;
    public TMP_Text elevatorText;
    public TMP_Text escalatorText;
    public TMP_Text toiletText;
    //public TMP_Text congestionText;
    public TMP_Text errorText;

    private Coroutine loadingCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    //private void Start() => ClearUI();    //d
    private void Start()    //b
    {
        ClearUI();  
        StartCoroutine(StartFlowSequence());
    }

    // 1단계 -> 2단계 패널 전환 흐름 관리 코루틴
    private IEnumerator StartFlowSequence() //b
    {
        // 1. 초기 패널 상태 설정
        if (Canvas_Start != null) Canvas_Start.SetActive(true);
        if (Canvas_Voice != null) Canvas_Voice.SetActive(false);

        // 2. StartPanel 3초간 유지
        yield return new WaitForSeconds(5.0f);

        // 3. StartPanel 끄고 VoicePanel 켜기
        if (Canvas_Start != null) Canvas_Start.SetActive(false);
        if (Canvas_Voice != null) Canvas_Voice.SetActive(true);

        // 4. VoiceInputManager에 음성 안내 및 녹음 시작 요청
        if (VoiceInputManager.Instance != null)
        {
            VoiceInputManager.Instance.StartVoiceGuidanceAndRecord();
        }
    }

    // 길 안내 답계 진입 시 UI 패널을 가려주는 메서드 //b
    public void HideVoicePanel()
    {
        if (Canvas_Voice != null) Canvas_Voice.SetActive(false);
        if (Canvas_Start != null) Canvas_Start.SetActive(false);
    }

    // 도착 시 외부(경로/앵커 스크립트)에서 호출할 함수
    public void OnArrived()
    {
        StartCoroutine(ArrivedRoutine());
    }

    // 3초 대기 후 Canvas_Arrived 켜는 코루틴
    private IEnumerator ArrivedRoutine()
    {
        // 1. (필요 시) 기존 안내 패널이나 Voice 패널 끄기
        HideVoicePanel();

        // 2. 3초 동안 대기 (음성 안내 재생 & "목적지에 도착했습니다" 문구가 보이는 시간)
        yield return new WaitForSeconds(3.0f);

        // 3. Canvas_Arrived 패널 활성화
        if (Canvas_Arrived != null)
        {
            Canvas_Arrived.SetActive(true);
        }
    }

    public void SetLoading()
    {
        if (loadingCoroutine != null)
            StopCoroutine(loadingCoroutine);

        // 로딩 시작 시 에러 메시지 초기화   //b
        if (errorText != null) errorText.text = "";

        loadingCoroutine = StartCoroutine(LoadingAnim());
    }

    public void StopLoading()
    {
        if (loadingCoroutine != null)
            StopCoroutine(loadingCoroutine);

        loadingCoroutine = null;
    }

    private IEnumerator LoadingAnim()
    {
        int dot = 0;

        while (true)
        {
            dot = (dot + 1) % 4;
            titleText.text = $"검색 중{new string('.', dot)}";
            yield return new WaitForSeconds(0.4f);
        }
    }

    // 정상 데이터 출력 시 //b
    public void UpdatePanel(List<FacilityInfo> list)
    {
        StopLoading();

        if (list == null || list.Count == 0)
        {
            ShowError("시설 없음");
            return;
        }

        // 정상 데이터가 돌아왔으므로 에러 텍스트는 숨김 (빈 문자열)    //b
        if (errorText != null) errorText.text = "";

        string station = list[0].stationName;
        int exit = list[0].exitNumber;

        bool elv = false, esc = false;
        List<string> toilets = new();

        foreach (var f in list)
        {
            if (f.facilityType == "ELV") elv = true;
            if (f.facilityType == "ESC") esc = true;
            if (f.facilityType == "TOI") toilets.Add(f.facilityName);
        }

        // 정상 정보 입력
        if (titleText != null) titleText.text = $"{station} {exit}번 출구";
        if (elevatorText != null) elevatorText.text = elv ? "엘리베이터 O" : "엘리베이터 X";
        if (escalatorText != null) escalatorText.text = esc ? "에스컬레이터 O" : "에스컬레이터 X";
        if (toiletText != null) toiletText.text = toilets.Count > 0 ? toilets[0] : "화장실 없음";
        //if (congestionText != null) congestionText.text = "";
    }

    public void ShowError(string msg)
    {
        // errorText.text = msg;    //d

        // 에러가 났으므로 일반 시설 텍스트들은 전부 지움   //b
        if (titleText != null) titleText.text = "";
        if (elevatorText != null) elevatorText.text = "";
        if (escalatorText != null) escalatorText.text = "";
        if (toiletText != null) toiletText.text = "";
        //if (congestionText != null) congestionText.text = "";

        // 에러 텍스트만 표시   //b
        if (errorText != null) errorText.text = msg;
    }

    void ClearUI()
    {
        if (titleText != null) titleText.text = "";
        if (elevatorText != null) elevatorText.text = "";
        if (escalatorText != null) escalatorText.text = "";
        if (toiletText != null) toiletText.text = "";
        //if (congestionText != null) congestionText.text = "";
        if (errorText != null) errorText.text = "";
    }
}