using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    [Header("추적 대상 (AR Main Camera)")]
    public Transform player;

    [Header("미니맵 높이 설정")]
    public float cameraHeight = 20f;

    void LateUpdate()
    {
        if (player == null) return;

        // 내 위치의 X, Z 평면 좌표만 추적하고, 높이(Y)는 지정한 값으로 고정
        Vector3 newPosition = player.position;
        newPosition.y = player.position.y + cameraHeight;
        transform.position = newPosition;
    }
}