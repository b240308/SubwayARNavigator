using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Google.XR.ARCoreExtensions;
using System;

public class ARStateManager : MonoBehaviour
{
    public AREarthManager earthManager;

    public static event Action OnARReady;

    private bool isReady = false;

    void Update()
    {
        if (isReady)
            return;

        if (earthManager == null)
            return;

        if (ARSession.state != ARSessionState.SessionTracking)
            return;

        if (earthManager.EarthTrackingState != TrackingState.Tracking)
            return;

        var pose = earthManager.CameraGeospatialPose;

        if (pose.HorizontalAccuracy <= 0 || pose.OrientationYawAccuracy <= 0)
            return;

        isReady = true;

        DebugUI.Instance?.Log("AR READY → UNLOCKED");

        OnARReady?.Invoke();
    }
}