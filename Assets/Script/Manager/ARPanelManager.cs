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

    [Header("Facility UI Text")]    //b
    public TMP_Text titleText;
    public TMP_Text elevatorText;
    public TMP_Text escalatorText;
    public TMP_Text toiletText;
    public TMP_Text congestionText;
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
        yield return new WaitForSeconds(3.0f);

        // 3. StartPanel 끄고 VoicePanel 켜기
        if (Canvas_Start != null) Canvas_Start.SetActive(false);
        if (Canvas_Voice != null) Canvas_Voice.SetActive(true);

        // 4. VoiceInputManager에 음성 안내 및 녹음 시작 요청
        if (VoiceInputManager.Instance != null)
        {
            VoiceInputManager.Instance.StartVoiceGuidanceAndRecord();
        }
    }

    public void SetLoading()
    {
        if (loadingCoroutine != null)
            StopCoroutine(loadingCoroutine);

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

    public void UpdatePanel(List<FacilityInfo> list)
    {
        StopLoading();

        if (list == null || list.Count == 0)
        {
            ShowError("시설 없음");
            return;
        }

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

        titleText.text = $"{station} {exit}번 출구";
        elevatorText.text = elv ? "엘리베이터 O" : "엘리베이터 X";
        escalatorText.text = esc ? "에스컬레이터 O" : "에스컬레이터 X";
        toiletText.text = toilets.Count > 0 ? toilets[0] : "화장실 없음";
    }

    public void ShowError(string msg)
    {
        errorText.text = msg;
    }

    void ClearUI()
    {
        titleText.text = "";
        elevatorText.text = "";
        escalatorText.text = "";
        toiletText.text = "";
        congestionText.text = "";
    }
}