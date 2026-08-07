using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Xml;

public class FacilityDataService : MonoBehaviour
{
    public static FacilityDataService Instance;

    [Header("공공데이터 API Key")]
    [SerializeField]
    private string serviceKey;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void Fetch(string station, int exitNumber)
    {
        ARPanelManager.Instance?.SetLoading();
        StartCoroutine(FetchFacilityData(station, exitNumber));
    }

    private IEnumerator FetchFacilityData(string station, int exitNumber)
    {
        Debug.Log($"[FacilityDataService] 요청: {station} {exitNumber}");

        string encodedStation = UnityWebRequest.EscapeURL(station);

        string url =
            $"https://apis.data.go.kr/B553766/facility/getFcElvtr" +
            $"?serviceKey={serviceKey}" +
            $"&pageNo=1" +
            $"&numOfRows=100" +
            $"&stnNm={encodedStation}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 20;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                ARPanelManager.Instance?.StopLoading();
                ARPanelManager.Instance?.ShowError("API 실패");
                Debug.LogError($"API 실패: {request.error}");
                yield break;
            }

            string response = request.downloadHandler.text;

            ParseFacilityData(response, station, exitNumber);
        }
    }

    private void ParseFacilityData(string response, string station, int exitNumber)
    {
        try
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(response);

            XmlNodeList facilityNodes = xmlDoc.GetElementsByTagName("fcltNm");

            List<FacilityInfo> facilityList = new List<FacilityInfo>();

            for (int i = 0; i < facilityNodes.Count; i++)
            {
                string facilityName = facilityNodes[i].InnerText;

                if (!facilityName.Contains(station)) continue;
                if (!facilityName.Contains($"{exitNumber}번")) continue;

                FacilityInfo info = new FacilityInfo
                {
                    stationName = station,
                    exitNumber = exitNumber,
                    facilityName = facilityName
                };

                if (facilityName.Contains("엘리베이터"))
                    info.facilityType = "ELV";
                else if (facilityName.Contains("에스컬레이터"))
                    info.facilityType = "ESC";
                else if (facilityName.Contains("화장실"))
                    info.facilityType = "TOI";
                else
                    info.facilityType = "UNKNOWN";

                facilityList.Add(info);
            }

            ARPanelManager.Instance?.UpdatePanel(facilityList);
        }
        catch (System.Exception e)
        {
            ARPanelManager.Instance?.StopLoading();
            Debug.LogError($"XML 파싱 실패: {e.Message}");
        }
    }
}