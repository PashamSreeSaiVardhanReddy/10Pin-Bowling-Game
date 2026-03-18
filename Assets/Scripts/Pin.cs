using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Pin : MonoBehaviour
{
    public enum UprightAxis { Up, Forward, Right, Auto }

    [Header("Detection")]
    public bool isFallen = false;
    [Tooltip("Angle (degrees) from world-up at which the pin is considered fallen.")]
    public float fallAngle = 35f;

    [Tooltip("Select which local axis of the model represents the pin's 'upright' direction.\n" +
             "If your prefab was rotated -90° on X, choose Forward.")]
    public UprightAxis uprightAxis = UprightAxis.Forward;

    // When a pin is still moving it can transiently exceed the angle — require it to be mostly settled.
    [Header("Settling")]
    public float settleVelocityThreshold = 0.1f;       // linear speed (m/s)
    public float settleAngularThreshold = 1f;          // angular speed (rad/s)

    [Header("Quick Confirm")]
    [Tooltip("When a collision indicates a possible fall, wait this short time and re-check to confirm. " +
             "Short values = faster detection, but may risk false positives.")]
    public float quickConfirmDelay = 0.12f;

    // Debug logging toggle to avoid string allocations in release runs
    [Header("Debug")]
    public bool enableDebugLogging = false;

    Rigidbody rb;
    // cache which local axis is treated as "up" for the model (unit vector in local space)
    Vector3 expectedUpLocal = Vector3.forward;
    // cached cosine of fallAngle to compare using dot products (avoids Angle/acos)
    float cosFallThreshold;
    // squared thresholds for velocity checks
    float settleVelSqrThreshold;
    float settleAngVelSqrThreshold;

    Coroutine confirmCoroutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        isFallen = false;
        ComputeExpectedUpLocal();
        UpdateThresholds();
    }

    void OnEnable()
    {
        // Ensure reused/pooled pins start upright/not fallen
        isFallen = false;
        ComputeExpectedUpLocal();
        UpdateThresholds();
    }

    void OnValidate()
    {
        ComputeExpectedUpLocal();
        UpdateThresholds();
    }

    void ComputeExpectedUpLocal()
    {
        if (uprightAxis == UprightAxis.Auto)
        {
            // Auto-detect which local axis is closest to world-up at setup time.
            float aUp = Vector3.Angle(transform.up, Vector3.up);
            float aForward = Vector3.Angle(transform.forward, Vector3.up);
            float aRight = Vector3.Angle(transform.right, Vector3.up);

            if (aForward <= aUp && aForward <= aRight) expectedUpLocal = Vector3.forward;
            else if (aRight <= aUp && aRight <= aForward) expectedUpLocal = Vector3.right;
            else expectedUpLocal = Vector3.up;
        }
        else if (uprightAxis == UprightAxis.Forward)
            expectedUpLocal = Vector3.forward;
        else if (uprightAxis == UprightAxis.Right)
            expectedUpLocal = Vector3.right;
        else
            expectedUpLocal = Vector3.up;
    }

    void UpdateThresholds()
    {
        cosFallThreshold = Mathf.Cos(fallAngle * Mathf.Deg2Rad);
        settleVelSqrThreshold = settleVelocityThreshold * settleVelocityThreshold;
        settleAngVelSqrThreshold = settleAngularThreshold * settleAngularThreshold;
    }

    // Fast path: when the pin is hit (collision), check angle immediately and start a short confirm
    // coroutine so the scoring manager can be notified sooner than waiting for rb.IsSleeping().
    void OnCollisionEnter(Collision collision)
    {
        if (isFallen) return;

        // Prefer to react only on meaningful collisions to avoid noise.
        // Use relativeVelocity magnitude as an inexpensive proxy for impact strength.
        float relVelSqr = collision.relativeVelocity.sqrMagnitude;

        // If collision was non-trivial or it was hit by the ball, try quick detection.
        bool isBall = collision.collider.CompareTag("Ball");
        if (isBall || relVelSqr > 0.01f)
        {
            TryStartQuickConfirm();
        }
    }

    void TryStartQuickConfirm()
    {
        if (isFallen) return;

        // If angle already exceeded, start/refresh confirm coroutine.
        if (IsAngleExceeded())
        {
            if (confirmCoroutine != null)
                StopCoroutine(confirmCoroutine);
            confirmCoroutine = StartCoroutine(ConfirmFallCoroutine());
        }
    }

    IEnumerator ConfirmFallCoroutine()
    {
        // Short wait to allow transient physics to settle a little.
        yield return new WaitForSeconds(quickConfirmDelay);

        // If pin already flagged by another path, exit.
        if (isFallen)
        {
            confirmCoroutine = null;
            yield break;
        }

        // Re-evaluate: angle and settling conditions.
        bool angleExceeded = IsAngleExceeded();
        bool settled = true;

        if (rb != null)
        {
            settled = rb.IsSleeping() ||
                      (rb.velocity.sqrMagnitude <= settleVelSqrThreshold &&
                       rb.angularVelocity.sqrMagnitude <= settleAngVelSqrThreshold);
        }

        if (angleExceeded && settled)
        {
            MarkFallen();
        }

        confirmCoroutine = null;
    }

    bool IsAngleExceeded()
    {
        // Compute model-up in world space using rotation multiplication (cheaper than TransformDirection)
        Vector3 expectedUpWorld = transform.rotation * expectedUpLocal;

        // expectedUpWorld is a rotated unit vector; dot product with world up is cos(angle).
        float dot = Vector3.Dot(expectedUpWorld, Vector3.up);
        return dot < cosFallThreshold;
    }

    void FixedUpdate()
    {
        if (isFallen) return;

        // Fast settle check first: if still moving (above thresholds) don't run angle check here.
        if (rb != null)
        {
            if (!rb.IsSleeping())
            {
                float velSqr = rb.velocity.sqrMagnitude;
                float angVelSqr = rb.angularVelocity.sqrMagnitude;
                if (velSqr > settleVelSqrThreshold ||
                    angVelSqr > settleAngVelSqrThreshold)
                {
                    // Still moving — don't run angle check this FixedUpdate.
                    return;
                }
            }
            // else sleeping -> proceed to angle detection
        }

        // If angle exceeded now, mark fallen (fallback if collision-driven quick confirm didn't happen).
        if (IsAngleExceeded())
        {
            MarkFallen();
        }
    }

    void MarkFallen()
    {
        if (isFallen) return;
        isFallen = true;

        var scoring = ScoringManager.Instance;
        if (scoring != null)
        {
            scoring.RegisterPinDown();
        }
        else if (enableDebugLogging)
        {
            Debug.LogWarning("ScoringManager.Instance is null when registering pin down: " + name);
        }

        if (enableDebugLogging)
        {
            Vector3 expectedUpWorld = transform.rotation * expectedUpLocal;
            float dot = Vector3.Dot(expectedUpWorld, Vector3.up);
            Debug.Log($"Pin fallen: {name} (dot: {dot:F3}, thresholdCos: {cosFallThreshold:F3})");
        }

        // stop any pending confirm coroutine
        if (confirmCoroutine != null)
        {
            StopCoroutine(confirmCoroutine);
            confirmCoroutine = null;
        }
    }
}
