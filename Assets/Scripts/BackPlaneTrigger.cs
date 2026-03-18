using UnityEngine;

public class BackPlaneTrigger : MonoBehaviour
{
    public SettleDetector settleDetector;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            Debug.Log("Ball crossed back plane");
            settleDetector.StartSettle();
        }
    }
}
