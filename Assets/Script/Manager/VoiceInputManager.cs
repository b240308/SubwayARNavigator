using System;
using System.Collections;
using UnityEngine;
using TMPro;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;
using System.Text;
using Unity.VisualScripting;


#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class VoiceInputManager : MonoBehaviour
{
    public static VoiceInputManager Instance;   //b //ARPanelManager에서 접근하기 위한 싱글톤

    [Header("Audio Settings")]  //b
    public AudioSource audioSource;       // 음성 안내 출력용 AudioSource
    public AudioClip guidanceClip;        // "목적지를 말씀해주세요" 음성 클립 (.wav / .mp3)

    [Header("UI & Dependencies")]   //b
    public TMP_Text statusText;
    public NaverGeocodingTest geocodingTest;

    private AudioClip recordedClip;

    private const int SAMPLE_RATE = 16000;
    private const int MAX_RECORD_TIME = 10;

    private const float SILENCE_THRESHOLD_TIME = 2f;
    private const float SILENCE_THRESHOLD_VOLUME = 0.02f;

    // 녹음 실패 시 최대 재시도 횟수 (무한 루프 방지) //b
    private const int MAX_RECORD_RETRIES = 10;  // 최대 10회까지 녹음 가능 

    // public TMP_Text statusText;  // 원래 코드

    private bool isRecording = false;
    private bool sttSuccess = false;

    private float[] audioSamples;

    // public NaverGeocodingTest geocodingTest; // 원래 코드

    private void Awake()    //b
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 오디오 소스가 별도로 할당되어 있지 않다면 컴포넌트 자동 찾기/추가    //b
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // 인스펙터 연결이 비어있다면 알아서 내 오브젝트에서 찾거나 없으면 생성함
        if (geocodingTest == null)
        {
            geocodingTest = GetComponent<NaverGeocodingTest>();
            if (geocodingTest == null)
            {
                geocodingTest = gameObject.AddComponent<NaverGeocodingTest>();
            }
        }
    }

    private void Start()
    {
        Debug.Log("[VoiceInputManager] 초기화 완료");

        audioSamples = new float[SAMPLE_RATE / 10];

        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[VoiceInputManager] 마이크 없음");
            return;
        }

        Debug.Log("[VoiceInputManager] 마이크 준비 완료");
    }

    // ==============================
    // 신규 추가 : TTS 음성 안내 수신 후 자동 녹음 시작  //b
    // ==============================
    public void StartVoiceGuidanceAndRecord()
    {
        StartCoroutine(GuidanceAndRecordRoutine());
    }

    private IEnumerator GuidanceAndRecordRoutine()
    {
        UpdateStatus("목적지를 말씀해주세요!", Color.white);

        // 1. AudioSource와 guidanceClip을 이용해 음성 재생
        if (audioSource != null && guidanceClip != null)
        {
            audioSource.clip = guidanceClip;
            audioSource.Play();

            // 2. 음성 파일 길이만큼 정확히 대기
            yield return new WaitForSeconds(guidanceClip.length);
        }
        else
        {
            Debug.LogWarning("[VoiceInputManager] AudioSource 또는 guidanceClip이 할당되지 않았습니다. 대기 없이 진행합니다.");
            yield return new WaitForSeconds(1.0f);
        }

        // 3. 음성이 끝나면 마이크 권한 확인 및 녹음 프로세스 시작
        MicButtonClicked();
    }

    // ==============================
    // 버튼 입력
    // ==============================
    public void MicButtonClicked()
    {
        if (isRecording)
        {
            Debug.LogWarning("[VoiceInputManager] 이미 녹음 중");
            return;
        }

        StartCoroutine(CheckPermissionAndRecord());
    }

    // ==============================
    // 권한 체크
    // ==============================
    private IEnumerator CheckPermissionAndRecord()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);

            float timeout = 0f;

            while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && timeout < 10f)
            {
                yield return new WaitForSeconds(0.1f);
                timeout += 0.1f;
            }
        }

        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            UpdateStatus("마이크 권한 필요", Color.red);
            yield break;
        }
#endif

        // UpdateStatus("듣는 중...", Color.cyan); //b

        yield return StartCoroutine(RecordAndProcess());
    }

    // ==============================
    // 전체 흐름
    // ==============================
    private IEnumerator RecordAndProcess()
    {
        isRecording = true;
        sttSuccess = false;

        // yield return StartCoroutine(RecordAudio());

        // if (recordedClip == null)
        // {
        //    UpdateStatus("녹음 실패", Color.red);
        //    isRecording = false;
        //    yield break;
        //}

        int recordRetryCount = 0;   //b

        // 녹음 실패 시 다시 "듣는 중..."으로 전환 후 재녹음 시도   //b
        while (recordRetryCount < MAX_RECORD_RETRIES)   
        {
            UpdateStatus("듣는 중...", Color.cyan);

            yield return StartCoroutine(RecordAudio());

            if (recordedClip != null)
            {
                // 녹음 성공 시 루프 탈출 후 진행
                break;
            }

            recordRetryCount++;
            Debug.LogWarning($"[VoiceInputManager] 녹음 실패 ({recordRetryCount}/{MAX_RECORD_RETRIES}). 재시도합니다.");

            // 재시도 간격 보장 (0.5초 대기)
            yield return new WaitForSeconds(0.5f);
        }

        // 최대 재시도 후에도 실패한 경우 처리
        if (recordedClip == null)
        {
            UpdateStatus("녹음 실패 (재시도 초과)", Color.red);
            isRecording = false;
            yield break;
        }

        byte[] wavData = WavUtility.FromAudioClip(recordedClip);

        CleanupAudioClip();

        UpdateStatus("인식 중...", Color.yellow);

        yield return StartCoroutine(CallGoogleSTTWithRetry(wavData, 2));

        isRecording = false;
    }

    // ==============================
    // 녹음
    // ==============================
    private IEnumerator RecordAudio()
    {
        if (Microphone.devices.Length == 0)
        {
            recordedClip = null;
            yield break;
        }

        string micName = Microphone.devices[0];

        recordedClip = Microphone.Start(micName, false, MAX_RECORD_TIME, SAMPLE_RATE);

        float timeout = 0f;

        while (Microphone.GetPosition(micName) <= 0)
        {
            timeout += Time.deltaTime;

            if (timeout > 5f)
            {
                recordedClip = null;
                yield break;
            }

            yield return null;
        }

        yield return StartCoroutine(WaitForSilenceOrTimeout(micName, MAX_RECORD_TIME));

        Microphone.End(micName);
    }

    // ==============================
    // 무음 감지
    // ==============================
    private IEnumerator WaitForSilenceOrTimeout(string micName, int maxTime)
    {
        float silenceTime = 0f;
        float startTime = Time.time;

        while (Time.time - startTime < maxTime)
        {
            int position = Microphone.GetPosition(micName);

            if (recordedClip != null && position > 0)
            {
                recordedClip.GetData(audioSamples, Mathf.Max(0, position - audioSamples.Length));

                float rms = 0f;

                foreach (float s in audioSamples)
                    rms += s * s;

                rms = Mathf.Sqrt(rms / audioSamples.Length);

                if (rms < SILENCE_THRESHOLD_VOLUME)
                {
                    silenceTime += Time.deltaTime;

                    if (silenceTime > SILENCE_THRESHOLD_TIME)
                        yield break;
                }
                else
                {
                    silenceTime = 0f;
                }
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    // ==============================
    // STT Retry
    // ==============================
    private IEnumerator CallGoogleSTTWithRetry(byte[] wavData, int maxRetries)
    {
        int retry = 0;

        while (retry <= maxRetries)
        {
            yield return StartCoroutine(CallGoogleSTT(wavData));

            if (sttSuccess)
                yield break;

            retry++;

            yield return new WaitForSeconds(1f);
        }
    }

    // ==============================
    // STT 요청
    // ==============================
    private IEnumerator CallGoogleSTT(byte[] wavData)
    {
        string googleSTTKey = APIKeyLoader.Instance?.GoogleSTT;

        if (string.IsNullOrEmpty(googleSTTKey))
        {
            UpdateStatus("STT 키 없음", Color.red);
            Debug.LogError("[VoiceInputManager] STT 키 없음");
            yield break;
        }

        string audioBase64 = Convert.ToBase64String(wavData);

        string jsonBody = $@"{{
            ""config"": {{
                ""encoding"": ""LINEAR16"",
                ""sampleRateHertz"": {SAMPLE_RATE},
                ""languageCode"": ""ko-KR""
            }},
            ""audio"": {{
                ""content"": ""{audioBase64}""
            }}
        }}";

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        string url =
            $"https://speech.googleapis.com/v1/speech:recognize?key={googleSTTKey}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            request.timeout = 30;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                UpdateStatus("STT 실패", Color.red);
                yield break;
            }

            ProcessSTTResponse(request.downloadHandler.text);
        }
    }

    // ==============================
    // 응답 처리 및 파싱
    // ==============================
    private void ProcessSTTResponse(string json)
    {
        try
        {
            JObject obj = JObject.Parse(json);
            string text = obj["results"]?[0]?["alternatives"]?[0]?["transcript"]?.ToString();

            if (string.IsNullOrEmpty(text))
            {
                UpdateStatus("인식 실패", Color.red);
                return;
            }

            sttSuccess = true;
            UpdateStatus(text, Color.green);

            var (station, exit) = SubwayQueryParser.Parse(text);

            // 인식된 역 이름과 출구 번호를 ArrivalPanelUI에 넘겨서 저장  //b
            ArrivalPanelUI.Instance?.SetDestinationInfo(station, exit);

            if (string.IsNullOrEmpty(station))
            {
                DebugUI.Instance?.Log("STATION PARSE FAIL");
                return;
            }

            // 역명 파싱 성공시 Voice 패널을 숨겨 AR 화면 전환  //b
            ARPanelManager.Instance?.HideVoicePanel();

            // 편의시설 - 출구 번호 있을 때만
            if (exit > 0)
            {
                FacilityDataService.Instance?.Fetch(station, exit);
            }

            // 경로 AR - 역 이름만 있어도 동작
            if (geocodingTest != null)
            {
                geocodingTest.Geocode(station + "역");
            }
            else
            {
                DebugUI.Instance?.Log("GEOCODING TEST NULL");
            }
        }
        catch
        {
            UpdateStatus("파싱 오류", Color.red);
        }
    }

    // ==============================
    // UI 업데이트
    // ==============================
    private void UpdateStatus(string msg, Color color)
    {
        if (statusText == null) return;

        statusText.text = msg;
        statusText.color = color;
    }

    // ==============================
    // 메모리 정리 및 리소스 관리
    // ==============================
    private void CleanupAudioClip()
    {
        if (recordedClip != null)
        {
            Destroy(recordedClip);
            recordedClip = null;
        }
    }

    private void OnDestroy()
    {
        CleanupAudioClip();
    }

    private void OnDisable()
    {
        if (Microphone.IsRecording(null))
        {
            Microphone.End(null);
        }
    }
}