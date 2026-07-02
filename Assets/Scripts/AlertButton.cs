// ─────────────────────────────────────────────────────────────────────────────
// AlertButton.cs
//
// A button that:
//   - Always shows "Alert T1" (or whatever device is currently open in
//     DeviceDetailPanel) as its label
//   - When clicked, sends "ID=T1,ALERT=1" back over serial via SerialLogPanel
//
// Setup:
//   1. Create a Button in your Canvas, attach this script to it.
//   2. Assign serialLogPanel, deviceDetailPanel, and buttonLabel (the
//      TextMeshProUGUI child of the Button) in the Inspector.
//   3. Wire the Button's OnClick → this script's OnAlertClicked(), OR let
//      this script auto-hook it (it does in Start()).
// ─────────────────────────────────────────────────────────────────────────────
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AlertButton : MonoBehaviour
{
    [Header("References")]
    public SerialLogPanel serialLogPanel;
    public DeviceDetailPanel deviceDetailPanel;

    [Tooltip("The TextMeshProUGUI on this button that shows 'Alert T1' etc.")]
    public TextMeshProUGUI buttonLabel;

    [Tooltip("Button component to hook OnClick to. Auto-found on this GameObject if left empty.")]
    public Button button;

    private void Start()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(OnAlertClicked);

        UpdateLabel();
    }

    private void Update()
    {
        // Keep label in sync as the selected device changes
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (buttonLabel == null || deviceDetailPanel == null) return;

        string id = deviceDetailPanel.SelectedId;
        buttonLabel.text = string.IsNullOrEmpty(id) ? "Alert" : $"Alert {id}";
    }

    public void OnAlertClicked()
    {
        if (deviceDetailPanel == null || serialLogPanel == null) return;

        string id = deviceDetailPanel.SelectedId;
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[AlertButton] No device selected — can't send alert.");
            return;
        }

        string message = $"ID={id},ALERT=1";
        serialLogPanel.SendLine(message);
    }
}