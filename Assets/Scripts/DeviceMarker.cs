// ─────────────────────────────────────────────────────────────────────────────
// DeviceMarker.cs
//
// Attached to the marker PREFAB. Anchors the marker to real-world Lat/Lon/Height
// using CesiumGlobeAnchor, and displays a live label (temp/SOS/fall) above it
// using a World Space Canvas + TextMeshProUGUI child — NOT head-locked, this
// floats at the marker's position in the world (label always faces camera via
// a simple billboard).
//
// Setup:
//   1. Create a prefab: a small Sphere/Pin mesh, with:
//        - CesiumGlobeAnchor component (from Cesium for Unity)
//        - This script
//        - A child World Space Canvas with a TextMeshProUGUI for the label
//          (Rich Text ON), positioned above the mesh (e.g. local Y = 2)
//   2. Assign labelText in the Inspector (or it auto-finds via GetComponentInChildren)
// ─────────────────────────────────────────────────────────────────────────────
using CesiumForUnity;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(CesiumGlobeAnchor))]
public class DeviceMarker : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Label shown above the marker. Auto-found in children if left empty.")]
    public TextMeshProUGUI labelText;

    [Tooltip("If assigned, this Transform is what billboards to face the camera (defaults to labelText's Canvas).")]
    public Transform billboardTarget;

    [Tooltip("Optional GameObject on the 3D pointer canvas that blinks when SOS is active")]
    public GameObject sosWarningElement;

    private CesiumGlobeAnchor _anchor;
    private Camera _mainCamera;
    private bool _isSosActive;

    public string DeviceId { get; private set; }

    private void Awake()
    {
        _anchor = GetComponent<CesiumGlobeAnchor>();

        if (labelText == null)
            labelText = GetComponentInChildren<TextMeshProUGUI>();

        if (billboardTarget == null && labelText != null)
            billboardTarget = labelText.transform.root == transform ? labelText.transform : labelText.transform.parent;

        if (sosWarningElement != null)
            sosWarningElement.SetActive(false);
    }

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    private void Update()
    {
        if (sosWarningElement != null)
        {
            if (_isSosActive)
            {
                // Blink state: cycles active/inactive (3 blinks per second)
                bool blinkState = (Mathf.FloorToInt(Time.time * 3f) % 2) == 0;
                sosWarningElement.SetActive(blinkState);
            }
            else
            {
                sosWarningElement.SetActive(false);
            }
        }
    }

    private void LateUpdate()
    {
        // Simple billboard: label always faces the camera
        if (billboardTarget != null && _mainCamera != null)
        {
            billboardTarget.rotation = Quaternion.LookRotation(
                billboardTarget.position - _mainCamera.transform.position);
        }
    }

    /// <summary>Moves this marker to the given Lat/Lon/Height (degrees, degrees, meters).</summary>
    public void SetPosition(double latitude, double longitude, double height)
    {
        _anchor.longitudeLatitudeHeight = new Unity.Mathematics.double3(longitude, latitude, height);
    }

    /// <summary>Updates the live label text from a DeviceData snapshot.</summary>
    public void UpdateLabel(DeviceData d)
    {
        DeviceId = d.Id;
        _isSosActive = d.Sos;

        if (labelText == null) return;

        string sosColor  = d.Sos  ? "#E57373" : "#66BB6A";
        string fallColor = d.Fall ? "#E57373" : "#66BB6A";

        labelText.text =
            $"<b>{d.Id}</b>\n" +
            $"<color=#81C784>{d.Temp:F1}°C</color>  " +
            $"<color={sosColor}>SOS:{(d.Sos ? 1 : 0)}</color>  " +
            $"<color={fallColor}>FALL:{(d.Fall ? 1 : 0)}</color>";
    }
}
