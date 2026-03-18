using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
public class BallThrow : MonoBehaviour
{
    public float velocityScale = 1.0f;        // adjust for feel
    public float angularScale = 1.0f;
    public int sampleCount = 6;               // smoothing window
    public float maxThrowSpeed = 12f;         // clamp to avoid explosions

    Rigidbody rb;
    XRGrabInteractable grab;
    Queue<Vector3> posBuffer = new Queue<Vector3>();
    Queue<Quaternion> rotBuffer = new Queue<Quaternion>();
    Transform interactorAttach;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    void OnDestroy()
    {
        grab.selectEntered.RemoveListener(OnGrab);
        grab.selectExited.RemoveListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        rb.isKinematic = true;
        posBuffer.Clear();
        rotBuffer.Clear();
        interactorAttach = args.interactorObject.transform;
        // seed buffer
        for (int i = 0; i < sampleCount; i++) posBuffer.Enqueue(interactorAttach.position);
        for (int i = 0; i < sampleCount; i++) rotBuffer.Enqueue(interactorAttach.rotation);
        StartCoroutine(SampleRoutine());
    }

    System.Collections.IEnumerator SampleRoutine()
    {
        while (grab.isSelected)
        {
            if (interactorAttach != null)
            {
                posBuffer.Enqueue(interactorAttach.position);
                rotBuffer.Enqueue(interactorAttach.rotation);
                if (posBuffer.Count > sampleCount) posBuffer.Dequeue();
                if (rotBuffer.Count > sampleCount) rotBuffer.Dequeue();
            }
            yield return new WaitForSeconds(0.01f);
        }
    }

    void OnRelease(SelectExitEventArgs args)
    {
        StopAllCoroutines();
        rb.isKinematic = false;

        // compute velocity by difference between oldest and newest sample
        Vector3[] poses = posBuffer.ToArray();
        if (poses.Length >= 2)
        {
            Vector3 oldest = poses[0];
            Vector3 newest = poses[poses.Length - 1];
            float dt = (poses.Length - 1) * 0.01f;
            Vector3 vel = (newest - oldest) / Mathf.Max(dt, 0.0001f);
            Vector3 finalVel = Vector3.ClampMagnitude(vel * velocityScale, maxThrowSpeed);
            rb.velocity = finalVel;
        }

        // angular velocity (approx)
        Quaternion[] rots = rotBuffer.ToArray();
        if (rots.Length >= 2)
        {
            Quaternion qOld = rots[0];
            Quaternion qNew = rots[rots.Length - 1];
            Quaternion dq = qNew * Quaternion.Inverse(qOld);
            float angle;
            Vector3 axis;
            dq.ToAngleAxis(out angle, out axis);
            float angVel = (angle * Mathf.Deg2Rad) / Mathf.Max((rots.Length - 1) * 0.01f, 0.0001f);
            rb.angularVelocity = axis.normalized * angVel * angularScale;
            rb.maxAngularVelocity = Mathf.Max(rb.maxAngularVelocity, rb.angularVelocity.magnitude);
        }
    }
}
