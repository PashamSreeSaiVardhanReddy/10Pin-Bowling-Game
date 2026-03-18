using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SettleDetector : MonoBehaviour
{
    [Header("Pin Set (optional)")]
    [Tooltip("Optional direct reference to the PinSetManager. If left null, the script will use ScoringManager.Instance.pinSet.")]
    public PinSetManager pinSet;

    [Header("Timing")]
    [Tooltip("Initial wait before starting to check for motion (seconds).")]
    public float initialWait = 0.5f;

    [Tooltip("Minimum time all pins must remain below thresholds to be considered settled (seconds).")]
    public float stableDuration = 0.7f;

    [Tooltip("Interval between motion checks (seconds).")]
    public float checkInterval = 0.15f;

    [Header("Motion thresholds")]
    [Tooltip("Linear speed (m/s) threshold under which a pin is considered not moving.")]
    public float velocityThreshold = 0.05f;

    [Tooltip("Angular speed (rad/s) threshold under which a pin is considered not moving.")]
    public float angularThreshold = 0.5f;

    bool running = false;

    public void StartSettle()
    {
        if (!running)
            StartCoroutine(SettleRoutine());
    }

    public void CancelSettle()
    {
        // Optional helper to stop a running settle coroutine (if needed by caller)
        StopAllCoroutines();
        running = false;
    }

    IEnumerator SettleRoutine()
    {
        running = true;

        if (initialWait > 0f)
            yield return new WaitForSeconds(initialWait);

        float stableTimer = 0f;

        // Resolve PinSetManager: prefer assigned field, fallback to ScoringManager
        PinSetManager effectivePinSet = pinSet;
        if (effectivePinSet == null && ScoringManager.Instance != null)
            effectivePinSet = ScoringManager.Instance.pinSet;

        if (effectivePinSet == null)
        {
            Debug.LogWarning("SettleDetector: PinSetManager not assigned and not found on ScoringManager. Falling back to fixed wait.");
            yield return new WaitForSeconds(stableDuration);
            if (ScoringManager.Instance != null)
                ScoringManager.Instance.EndRoll();
            else
                Debug.LogWarning("SettleDetector: ScoringManager.Instance is null.");
            running = false;
            yield break;
        }

        List<GameObject> pins = effectivePinSet.spawnedPins;

        // If there are no pins treat as settled immediately (avoids hang)
        if (pins == null || pins.Count == 0)
        {
            Debug.Log("SettleDetector: no pins to check — treating as settled.");
            if (ScoringManager.Instance != null)
                ScoringManager.Instance.EndRoll();
            running = false;
            yield break;
        }

        while (true)
        {
            bool allBelowThreshold = true;

            foreach (GameObject g in pins)
            {
                if (g == null) continue;

                Rigidbody rb = g.GetComponent<Rigidbody>();
                if (rb == null) continue;

                if (rb.IsSleeping()) continue;

                if (rb.velocity.sqrMagnitude > (velocityThreshold * velocityThreshold) ||
                    rb.angularVelocity.sqrMagnitude > (angularThreshold * angularThreshold))
                {
                    allBelowThreshold = false;
                    break;
                }
            }

            if (allBelowThreshold)
            {
                stableTimer += checkInterval;
                if (stableTimer >= stableDuration)
                    break;
            }
            else
            {
                stableTimer = 0f;
            }

            yield return new WaitForSeconds(checkInterval);
        }

        Debug.Log("Pins settled");
        if (ScoringManager.Instance != null)
            ScoringManager.Instance.EndRoll();
        else
            Debug.LogWarning("SettleDetector: ScoringManager.Instance is null when attempting to EndRoll.");

        running = false;
    }
}
