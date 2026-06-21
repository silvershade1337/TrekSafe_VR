using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System.Collections.Generic;

// Alias to disambiguate — XR's InputDevice vs InputSystem's
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRNode = UnityEngine.XR.XRNode;

public class VRFlyController : MonoBehaviour
{
    [Header("References")]
    public Transform xrRig;            // XR Origin (XR Rig)
    public Transform cameraTransform;  // Main Camera inside Camera Offset

    [Header("Flight Settings")]
    public float horizontalSpeed = 10f;
    public float verticalSpeed = 5f;
    public float turnSpeed = 60f;
    public float sprintMultiplier = 3f;

    // -------------------------------------------------------
    // CONTROL SCHEME
    //
    // LEFT CONTROLLER
    //   Thumbstick X/Y  → strafe left/right, fly forward/back
    //   Grip            → sprint
    //
    // RIGHT CONTROLLER
    //   Thumbstick X    → turn left/right
    //   Thumbstick Y    → fly up/down
    //
    // EDITOR KEYBOARD (when no HMD)
    //   WASD            → forward/back/strafe
    //   Q / E           → fly down / up
    //   Arrow Left/Right→ turn
    //   Left Shift      → sprint
    //   Mouse (hold RMB)→ look around
    // -------------------------------------------------------

    private XRInputDevice leftController;
    private XRInputDevice rightController;

    // Editor mouse look state
    private float editorYaw = 0f;
    private float editorPitch = 0f;

    void Start()
    {
        AcquireControllers();

        // Seed editor yaw from rig's current rotation so it doesn't snap
        editorYaw = xrRig.eulerAngles.y;
    }

    void Update()
    {
        if (!leftController.isValid || !rightController.isValid)
            AcquireControllers();

#if UNITY_EDITOR
        HandleEditorInput();
#else
        HandleControllerInput();
#endif
    }

    // ─────────────────────────────────────────────────────────
    // CONTROLLER INPUT  (on headset)
    // ─────────────────────────────────────────────────────────
    void HandleControllerInput()
    {
        // Read axes
        leftController.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 leftStick);
        rightController.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 rightStick);

        // Read grip for sprint
        leftController.TryGetFeatureValue(XRCommonUsages.gripButton, out bool leftGrip);

        float speed = leftGrip ? horizontalSpeed * sprintMultiplier : horizontalSpeed;

        // Yaw-only directions (head pitch ignored for horizontal)
        Vector3 flatForward = FlatForward();
        Vector3 flatRight = FlatRight();

        // LEFT STICK → horizontal move
        // RIGHT STICK Y → vertical
        Vector3 moveDir = (flatForward * leftStick.y)
                        + (flatRight * leftStick.x)
                        + (Vector3.up * rightStick.y);

        xrRig.position += moveDir * speed * Time.deltaTime;

        // RIGHT STICK X → turn
        xrRig.Rotate(Vector3.up, rightStick.x * turnSpeed * Time.deltaTime, Space.World);
    }

    // ─────────────────────────────────────────────────────────
    // EDITOR / KEYBOARD INPUT  (no HMD)
    // ─────────────────────────────────────────────────────────
    void HandleEditorInput()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        if (keyboard == null) return;

        bool sprint = keyboard.leftShiftKey.isPressed;
        float speed = sprint ? horizontalSpeed * sprintMultiplier : horizontalSpeed;

        // WASD → move
        float fb = 0f, lr = 0f, ud = 0f;
        if (keyboard.wKey.isPressed) fb = 1f;
        if (keyboard.sKey.isPressed) fb = -1f;
        if (keyboard.aKey.isPressed) lr = -1f;
        if (keyboard.dKey.isPressed) lr = 1f;

        // Q / E → down / up
        if (keyboard.eKey.isPressed) ud = 1f;
        if (keyboard.qKey.isPressed) ud = -1f;

        Vector3 moveDir = (FlatForward() * fb)
                        + (FlatRight() * lr)
                        + (Vector3.up * ud);

        xrRig.position += moveDir * speed * Time.deltaTime;

        // Arrow keys → turn
        float turn = 0f;
        if (keyboard.leftArrowKey.isPressed) turn = -1f;
        if (keyboard.rightArrowKey.isPressed) turn = 1f;
        xrRig.Rotate(Vector3.up, turn * turnSpeed * Time.deltaTime, Space.World);

        // Hold RMB → mouse look
        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();

            editorYaw += mouseDelta.x * 0.2f;
            editorPitch -= mouseDelta.y * 0.2f;
            editorPitch = Mathf.Clamp(editorPitch, -80f, 80f);

            cameraTransform.localRotation = Quaternion.Euler(editorPitch, 0f, 0f);
            xrRig.rotation = Quaternion.Euler(0f, editorYaw, 0f);
        }
    }

    // ─────────────────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────────────────

    // Forward flattened to horizontal plane, based on camera yaw
    Vector3 FlatForward()
    {
        Vector3 f = cameraTransform.forward;
        f.y = 0;
        return f.normalized;
    }

    Vector3 FlatRight()
    {
        Vector3 r = cameraTransform.right;
        r.y = 0;
        return r.normalized;
    }

    void AcquireControllers()
    {
        var left = new List<XRInputDevice>();
        var right = new List<XRInputDevice>();

        InputDevices.GetDevicesAtXRNode(XRNode.LeftHand, left);
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, right);

        if (left.Count > 0) leftController = left[0];
        if (right.Count > 0) rightController = right[0];
    }
}