// ─────────────────────────────────────────────────────────────────────────────
// DeviceMapManager.cs
//
// Watches SerialLogPanel.Devices. The first time a device ID shows up with
// data, it spawns a DeviceMarker prefab anchored at that Lat/Lon (devices that
// never send data never get a marker). After that, it just updates the
// existing marker's position + label every refresh.
//
// Also smoothly flies the main free-flight camera to a device's location
// when requested (e.g. wire this to your existing device buttons alongside
// DeviceDetailPanel — call FlyToDevice("T1") from a button's onClick, or let
// this script auto-hook the same buttons you already assigned there).
//
// Setup:
//   1. Assign serialLogPanel, markerPrefab (the DeviceMarker prefab), and
//      a parent Transform to spawn markers under (can be the CesiumGeoreference
//      object itself, or any child of it).
//   2. Assign flightCamera = your main free-flight camera.
//   3. Either call FlyToDevice("T1") from your own button handlers, or fill
//      in the optional "Device Buttons" array below to have this script wire
//      it up itself.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections;
using System.Collections.Generic;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

public class DeviceMapManager : MonoBehaviour
{
    [Serializable]
    public class DeviceButtonBinding
    {
        public Button button;
        public string deviceId;
    }

    [Header("References")]
    public SerialLogPanel serialLogPanel;
    public DeviceMarker markerPrefab;
    public Transform markerParent;

    [Header("Camera Fly-To")]
    [Tooltip("Assign your XR Origin / XR Rig root here — it must have a CesiumGlobeAnchor " +
             "component on it (added when you set up CesiumOriginShift). We drive its " +
             "Lat/Lon/Height through that anchor, NOT raw Transform — setting raw Transform " +
             "position directly fights the anchor and throws the rig to nonsensical positions.")]
    public CesiumGlobeAnchor flightRigAnchor;
    [Tooltip("How long the rig takes to fly to a device, in seconds")]
    public float flightDuration = 2f;
    [Tooltip("Height in meters ABOVE the device's own altitude to hover")]
    public float cameraHeightOffset = 50f;
    [Tooltip("How far back (meters, roughly) from the device the rig settles — " +
             "approximated as a small latitude offset since we're working in degrees")]
    public float cameraBackDistanceMeters = 30f;

    [Header("Optional: Wire Buttons Directly")]
    [Tooltip("If filled in, clicking these buttons calls FlyToDevice automatically")]
    public DeviceButtonBinding[] deviceButtons;

    [Header("Refresh")]
    public float refreshInterval = 0.5f;

    private readonly Dictionary<string, DeviceMarker> _markers = new Dictionary<string, DeviceMarker>();
    private float _timer;
    private Coroutine _flightCoroutine;

    private void Start()
    {
        foreach (var binding in deviceButtons)
        {
            if (binding.button == null || string.IsNullOrEmpty(binding.deviceId)) continue;
            string idCopy = binding.deviceId;
            binding.button.onClick.AddListener(() => FlyToDevice(idCopy));
        }
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < refreshInterval) return;
        _timer = 0f;

        if (serialLogPanel == null) return;

        foreach (var kvp in serialLogPanel.Devices)
        {
            string id = kvp.Key;
            DeviceData latest = kvp.Value.Latest;
            if (latest == null) continue;

            if (!_markers.TryGetValue(id, out DeviceMarker marker))
            {
                marker = SpawnMarker(id, latest);
                _markers[id] = marker;
            }

            marker.SetPosition(latest.Lat, latest.Lon, latest.Alt);
            marker.UpdateLabel(latest);
        }
    }

    private DeviceMarker SpawnMarker(string id, DeviceData initialData)
    {
        DeviceMarker marker = Instantiate(markerPrefab, markerParent);
        marker.name = $"DeviceMarker_{id}";
        marker.SetPosition(initialData.Lat, initialData.Lon, initialData.Alt);
        marker.UpdateLabel(initialData);
        return marker;
    }

    /// <summary>Smoothly flies the rig to hover near the given device's current globe position.</summary>
    public void FlyToDevice(string id)
    {
        if (!_markers.TryGetValue(id, out DeviceMarker marker))
        {
            Debug.LogWarning($"[DeviceMapManager] No marker yet for device '{id}' — can't fly to it.");
            return;
        }

        if (flightRigAnchor == null)
        {
            Debug.LogWarning("[DeviceMapManager] flightRigAnchor is not assigned.");
            return;
        }

        DeviceData latest = serialLogPanel.GetDevice(id);
        if (latest == null) return;

        if (_flightCoroutine != null)
            StopCoroutine(_flightCoroutine);

        _flightCoroutine = StartCoroutine(FlyRoutine(latest));
    }

    // Rough meters-per-degree conversion for a small latitude offset (good enough
    // for "settle back a bit" — doesn't need to be geodesically perfect).
    private const double MetersPerDegreeLatitude = 111320.0;

    private IEnumerator FlyRoutine(DeviceData device)
    {
        double3 startLLH = flightRigAnchor.longitudeLatitudeHeight;

        double backDegreesLat = cameraBackDistanceMeters / MetersPerDegreeLatitude;

        double3 endLLH = new double3(
            device.Lon,                                  // longitude unchanged — settle directly "south" of the device
            device.Lat - backDegreesLat,                  // shift back slightly in latitude
            device.Alt + cameraHeightOffset);              // hover above the device's altitude

        float elapsed = 0f;
        while (elapsed < flightDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / flightDuration);

            flightRigAnchor.longitudeLatitudeHeight = new double3(
                math.lerp(startLLH.x, endLLH.x, t),
                math.lerp(startLLH.y, endLLH.y, t),
                math.lerp(startLLH.z, endLLH.z, t));

            yield return null;
        }

        flightRigAnchor.longitudeLatitudeHeight = endLLH;
    }
}