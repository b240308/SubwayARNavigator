using UnityEngine;

public class KeepMarkerFlat : MonoBehaviour
{
    void LateUpdate()
    {
        if (transform.parent == null) return;

        // 카메라의 좌우 회전(Y축)만 가져오고, 위아래 기울임(X, Z축)은 90도로 고정
        float parentYRotation = transform.parent.eulerAngles.y;
        transform.rotation = Quaternion.Euler(90f, parentYRotation, 0f);
    }
}