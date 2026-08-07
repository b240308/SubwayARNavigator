using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Google.XR.ARCoreExtensions;
using UnityEngine.Android;
using System.Collections;


public class ARSystemDebugger : MonoBehaviour
{
    public AREarthManager earthManager;

    private bool gpsReady = false;

    private ARSessionState lastSessionState;
    private TrackingState lastEarthState;
    private bool poseReadyLast = false;

    IEnumerator Start()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            Permission.RequestUserPermission(Permission.FineLocation);

        Input.location.Start();

        int wait = 20;

        while (Input.location.status == LocationServiceStatus.Initializing && wait-- > 0)
            yield return new WaitForSeconds(1);

        gpsReady = Input.location.status == LocationServiceStatus.Running;

        DebugUI.Instance?.Log(gpsReady ? "GPS READY" : "GPS FAIL");
    }

    void Update()
    {
        if (earthManager == null)
            return;

        // 1. SESSION 상태 변화만 출력
        if (ARSession.state != lastSessionState)
        {
            DebugUI.Instance?.Log("SESSION → " + ARSession.state);
            lastSessionState = ARSession.state;
        }

        // 2. EARTH 상태 변화만 출력
        var earthState = earthManager.EarthTrackingState;

        if (earthState != lastEarthState)
        {
            DebugUI.Instance?.Log("EARTH → " + earthState);
            lastEarthState = earthState;
        }

        // 3. POSE 준비 상태 변화만 출력
        var pose = earthManager.CameraGeospatialPose;

        bool poseReady =
            pose.HorizontalAccuracy > 0 &&
            pose.OrientationYawAccuracy > 0;

        if (poseReady != poseReadyLast)
        {
            DebugUI.Instance?.Log("POSE READY → " + poseReady);
            poseReadyLast = poseReady;
        }
    }
}