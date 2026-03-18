using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseMovementOfCamera : MonoBehaviour
{
    [Header("Activation")]
    [Tooltip("When true the mouse will control the camera. Call StartGame() to enable.")]
    public bool isGameStarted = false;

    [Header("Mouse Settings")]
    public float sensitivity = 2.0f;
    public float smoothing = 5.0f;
    [Tooltip("Maximum up/down looking angle (degrees)")]
    public float maxVerticalAngle = 80f;
    public bool lockCursorOnStart = true;

    float yaw;   // left/right
    float pitch; // up/down
    Vector2 currentVelocity;

    void Start()
    {
        Vector3 e = transform.localEulerAngles;
        yaw = e.y;
        pitch = e.x;
    }

    void Update()
    {
        if (!isGameStarted) return;

        // Read raw mouse delta
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        // Scale by sensitivity
        Vector2 targetDelta = new Vector2(mouseX, mouseY) * sensitivity;

        // Smooth the input
        currentVelocity = Vector2.Lerp(currentVelocity, targetDelta, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        Vector2 smoothed = currentVelocity * Time.deltaTime * 100f; // scale for consistent feel

        yaw += smoothed.x;
        pitch -= smoothed.y;

        pitch = Mathf.Clamp(pitch, -maxVerticalAngle, maxVerticalAngle);

        transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    // Call this from your Start Game button (e.g., in the UI OnClick) or other game-start logic
    public void StartGame()
    {
        isGameStarted = true;
        if (lockCursorOnStart)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // Optional: call to stop camera control (e.g., on pause / end)
    public void StopGame()
    {
        isGameStarted = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Optional externally-accessible setter if you prefer to manage state elsewhere
    public void SetGameStarted(bool started)
    {
        if (started) StartGame();
        else StopGame();
    }
}
