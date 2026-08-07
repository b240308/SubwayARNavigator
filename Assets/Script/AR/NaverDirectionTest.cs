using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class NaverDirectionTest : MonoBehaviour
{
    public string tmapApiKey; // 인스펙터에서 T Map API 키 할당
    public RouteAnchorSpawner routeSpawner;

    public void Request(double goalLat, double goalLon)
    {
        StartCoroutine(GPSThenRequest(goalLat, goalLon));
    }

    private IEnumerator GPSThenRequest(double goalLat, double goalLon)
    {
        yield return StartCoroutine(GPS());
        yield return StartCoroutine(RequestRoute(goalLat, goalLon));
    }

    private IEnumerator GPS()
    {
        Input.location.Start();
        int wait = 20;

        while (Input.location.status == LocationServiceStatus.Initializing && wait-- > 0)
        {
            yield return new WaitForSeconds(1);
        }

        if (Input.location.status != LocationServiceStatus.Running)
            DebugUI.Instance?.Log("GPS FAIL");
        else
            DebugUI.Instance?.Log("GPS READY");
    }

    private IEnumerator RequestRoute(double goalLat, double goalLon)
    {
        if (Input.location.status != LocationServiceStatus.Running)
        {
            DebugUI.Instance?.Log("GPS NOT READY");
            yield break;
        }

        double lat = Input.location.lastData.latitude;
        double lon = Input.location.lastData.longitude;

        if (lat == 0 || lon == 0)
        {
            DebugUI.Instance?.Log("INVALID GPS (0,0)");
            yield break;
        }

        string url = "https://apis.openapi.sk.com/tmap/routes/pedestrian?version=1";

        // T Map 도보는 POST 요청
        string body = $@"{{
            ""startX"": ""{lon}"",
            ""startY"": ""{lat}"",
            ""endX"": ""{goalLon}"",
            ""endY"": ""{goalLat}"",
            ""reqCoordType"": ""WGS84GEO"",
            ""resCoordType"": ""WGS84GEO"",
            ""startName"": ""출발지"",
            ""endName"": ""목적지""
        }}";

        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(body);

        DebugUI.Instance?.Log("TMAP REQUEST");

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("appKey", tmapApiKey);

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                DebugUI.Instance?.Log("API FAIL: " + req.error);
                DebugUI.Instance?.Log("CODE: " + req.responseCode);
                yield break;
            }

            DebugUI.Instance?.Log("ROUTE OK");
            ParseRoute(req.downloadHandler.text);
        }
    }

    private void ParseRoute(string json)
    {
        DebugUI.Instance?.Log("PARSE START");

        JObject obj = JObject.Parse(json);
        var features = obj["features"];

        if (features == null || !features.HasValues)
        {
            DebugUI.Instance?.Log("PATH PARSE FAIL");
            return;
        }

        List<Vector2> routePoints = new List<Vector2>();

        foreach (var feature in features)
        {
            var geometry = feature["geometry"];
            if (geometry == null) continue;

            string type = geometry["type"]?.ToString();

            // LineString 타입만 경로 좌표
            if (type == "LineString")
            {
                var coords = geometry["coordinates"];
                if (coords == null) continue;

                foreach (var coord in coords)
                {
                    double rLon = (double)coord[0];
                    double rLat = (double)coord[1];
                    routePoints.Add(new Vector2((float)rLat, (float)rLon));
                }
            }
        }

        DebugUI.Instance?.Log("PATH COUNT: " + routePoints.Count);

        if (routeSpawner == null)
        {
            DebugUI.Instance?.Log("ROUTE SPAWNER NULL");
            return;
        }

        routeSpawner.SpawnRoute(routePoints);
    }
}