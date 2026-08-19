using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class NaverGeocodingTest : MonoBehaviour
{
    public string kakaoRestApiKey; // 인스펙터에서 할당
    public NaverDirectionTest directionTest;

    public void Geocode(string query)
    {
        StartCoroutine(GeocodeCoroutine(query));
    }

    private IEnumerator GeocodeCoroutine(string query)
    {
        DebugUI.Instance?.Log("GEOCODE: " + query);

        string url = $"https://dapi.kakao.com/v2/local/search/keyword.json?query={UnityWebRequest.EscapeURL(query)}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Authorization", "KakaoAK " + kakaoRestApiKey);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                DebugUI.Instance?.Log("GEOCODE FAIL: " + req.error);
                yield break;
            }

            DebugUI.Instance?.Log("GEOCODE RAW: " + req.downloadHandler.text);

            JObject obj = JObject.Parse(req.downloadHandler.text);
            var documents = obj["documents"];

            if (documents == null || !documents.HasValues)
            {
                DebugUI.Instance?.Log("GEOCODE NO RESULT");
                yield break;
            }

            double lat = double.Parse(documents[0]["y"].ToString());
            double lon = double.Parse(documents[0]["x"].ToString());

            DebugUI.Instance?.Log($"GEOCODE OK: {lat}, {lon}");

            if (directionTest == null)
            {
                DebugUI.Instance?.Log("DIRECTION TEST NULL");
                yield break;
            }

            directionTest.Request(lat, lon);
        }
    }
}