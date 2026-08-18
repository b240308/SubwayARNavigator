using UnityEngine;

public class AttachMarker : MonoBehaviour
{
    [Header("미니맵에 표시할 빨간 점/화살표 프리팹")]
    public GameObject markerPrefab;

    [Header("카메라 기준 표식 위치 조정")]
    public Vector3 offset = new Vector3(0, -0.5f, 0);

    void Start()
    {
        // 씬에서 MainCamera 태그를 가진 카메라 탐색
        Camera mainCam = Camera.main;

        if (mainCam != null && markerPrefab != null)
        {
            // 메인 카메라의 자식(Child)으로 표식 생성
            GameObject marker = Instantiate(markerPrefab, mainCam.transform);

            // 로컬 위치 및 회전값 설정 (바닥에 납작하게 펼침)
            marker.transform.localPosition = offset;
            marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // 기울임 방지 스크립트 자동 추가
            marker.AddComponent<KeepMarkerFlat>();
        }
        else
        {
            Debug.LogWarning("[AttachMarker] MainCamera 또는 MarkerPrefab이 연결되지 않았습니다.");
        }
    }
}