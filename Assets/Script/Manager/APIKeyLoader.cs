using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

public class APIKeyLoader : MonoBehaviour
{
    public static APIKeyLoader Instance;

    public string GoogleSTT { get; private set; }
    public string Kakao { get; private set; }
    public string PublicData { get; private set; }
    public string SeoulData { get; private set; }
    public string GoogleMaps { get; private set; }

    void Awake()
    {
        Instance = this;

        TextAsset txt = Resources.Load<TextAsset>("APIKeys");

        if (txt == null)
        {
            Debug.LogError("[APIKeyLoader] APIKeys.json 없음 (Resources 폴더 확인)");
            return;
        }

        Dictionary<string, string> data = null;

        try
        {
            data = JsonConvert.DeserializeObject<Dictionary<string, string>>(txt.text);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[APIKeyLoader] JSON 파싱 실패: " + e.Message);
            return;
        }

        GoogleSTT = GetValue(data, "googleSTT");
        Kakao = GetValue(data, "kakao");
        PublicData = GetValue(data, "publicData");
        SeoulData = GetValue(data, "seoulData");
        GoogleMaps = GetValue(data, "googleMaps");

        Debug.Log("[APIKeyLoader] 키 로드 완료");
    }

    private string GetValue(Dictionary<string, string> data, string key)
    {
        if (data != null && data.ContainsKey(key))
            return data[key];

        Debug.LogWarning($"[APIKeyLoader] 키 없음: {key}");
        return "";
    }
}