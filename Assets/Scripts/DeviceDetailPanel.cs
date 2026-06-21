// ─────────────────────────────────────────────────────────────────────────────
// DeviceDetailPanel.cs
//
// Separate panel (alongside the raw log) that shows ONE device's full data
// as rich-text. Three independent buttons (assigned in Inspector, each tied
// to a device ID string) switch which device's data is displayed.
//
// Setup:
//   1. Create 3 Buttons in your Canvas (one per device).
//   2. Create a TextMeshProUGUI for the detail display (Rich Text ON by default).
//   3. Add this script to any GameObject, assign serialLogPanel + detailText.
//   4. In the "Device Buttons" list, add 3 entries: each with its Button
//      reference and the device ID string it represents (e.g. "T1", "T2", "T3").
//      These IDs must match whatever ID= the ESP actually sends.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DeviceDetailPanel : MonoBehaviour
{
    [Serializable]
    public class DeviceButtonBinding
    {
        public Button button;
        [Tooltip("Device ID this button represents, e.g. T1 — must match the ID= field from the ESP")]
        public string deviceId;
    }

    [Header("References")]
    public SerialLogPanel serialLogPanel;
    public TextMeshProUGUI detailText;

    [Header("Device Buttons")]
    [Tooltip("One entry per button — assign the Button and the device ID it represents")]
    public DeviceButtonBinding[] deviceButtons;

    [Header("Refresh")]
    [Tooltip("How often (seconds) to refresh the currently displayed device's data")]
    public float refreshInterval = 0.5f;

    private string _selectedId;
    private float _timer;

    private void Start()
    {
        foreach (var binding in deviceButtons)
        {
            if (binding.button == null || string.IsNullOrEmpty(binding.deviceId)) continue;

            string idCopy = binding.deviceId; // capture for closure
            Button buttonCopy = binding.button;
            binding.button.onClick.AddListener(() => SelectDevice(idCopy, buttonCopy));
        }

        // Default to the first configured button's device, if any
        if (deviceButtons.Length > 0 && !string.IsNullOrEmpty(deviceButtons[0].deviceId))
            SelectDevice(deviceButtons[0].deviceId, deviceButtons[0].button);
        else
            RefreshDisplay();
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < refreshInterval) return;
        _timer = 0f;

        RefreshDisplay();
    }

    /// <summary>
    /// Switches the displayed device AND forces Unity's EventSystem to mark
    /// this button as "Selected" — which is what makes the Button component's
    /// Sprite Swap transition show its Selected Sprite. Selecting a new button
    /// automatically clears the Selected state on whichever button had it
    /// before, so only one button ever appears "active" at a time.
    /// </summary>
    public void SelectDevice(string id, Button sourceButton = null)
    {
        _selectedId = id;
        RefreshDisplay();

        if (sourceButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(sourceButton.gameObject);
    }

    private void RefreshDisplay()
    {
        if (detailText == null) return;

        if (string.IsNullOrEmpty(_selectedId) || serialLogPanel == null)
        {
            detailText.text = "<color=#888888>No device selected</color>";
            return;
        }

        DeviceData d = serialLogPanel.GetDevice(_selectedId);
        if (d == null)
        {
            detailText.text = $"<color=#888888>No data for {_selectedId} yet...</color>";
            return;
        }

        detailText.text = FormatDevice(d);
    }

    private static string FormatDevice(DeviceData d)
    {
        string sosColor = d.Sos ? "#E57373" : "#66BB6A";
        string fallColor = d.Fall ? "#E57373" : "#66BB6A";
        string sosText = d.Sos ? "TRIGGERED" : "ok";
        string fallText = d.Fall ? "DETECTED" : "none";

        return
            $"<b><size=140%><color=#4FC3F7>{d.Id}</color></size></b>  " +
            $"<color=#888888>(updated {d.Timestamp:HH:mm:ss})</color>\n\n" +
            $"<b>Location</b>\n" +
            $"  Lat: <color=#FFD54F>{d.Lat:F6}</color>\n" +
            $"  Lon: <color=#FFD54F>{d.Lon:F6}</color>\n\n" +
            $"<b>Environment</b>\n" +
            $"  Temp:  <color=#81C784>{d.Temp:F1} \u00B0C</color>\n" +
            $"  Press: <color=#81C784>{d.Press:F0} Pa</color>\n" +
            $"  Alt:   <color=#81C784>{d.Alt:F1} m</color>\n\n" +
            $"<b>Status</b>\n" +
            $"  SOS:  <color={sosColor}>{sosText}</color>\n" +
            $"  FALL: <color={fallColor}>{fallText}</color>";
    }
}