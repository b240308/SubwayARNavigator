using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// STT 텍스트에서 역명과 출구 번호를 추출
/// 예:
/// "신도림 2번 출구"
/// → station="신도림", exit=2
/// </summary>
public static class SubwayQueryParser
{
    /// <summary>
    /// 역명 + 출구번호 파싱
    /// </summary>
    public static (string station, int exit) Parse(string input)
    {
        Debug.Log($"[Parser 입력값] '{input}'");

        if (string.IsNullOrEmpty(input))
        {
            Debug.LogError("[Parser] 입력값 NULL");
            return ("", -1);
        }

        input = input.Trim();

        // 출구 번호 있는 경우: "상인역 2번 출구"
        Match matchFull = Regex.Match(input, @"(.+?)\s*(\d+)\s*번?\s*출구");
        if (matchFull.Success)
        {
            string station = matchFull.Groups[1].Value.Trim().Replace("역", "").Trim();
            int.TryParse(matchFull.Groups[2].Value, out int exit);
            Debug.Log($"[Parser 성공] station={station}, exit={exit}");
            return (station, exit);
        }

        // 역 이름만 있는 경우: "상인역"
        Match matchStation = Regex.Match(input, @"(.+?)역");
        if (matchStation.Success)
        {
            string station = matchStation.Groups[1].Value.Trim();
            Debug.Log($"[Parser 성공] station={station}, exit=0");
            return (station, 0);
        }

        Debug.LogError("[Parser] 정규식 매칭 실패");
        return ("", -1);
    }

    /// <summary>
    /// 결과 포맷
    /// </summary>
    public static string FormatResult(string station, int exit)
    {
        return $"{station}역 {exit}번 출구";
    }
}
