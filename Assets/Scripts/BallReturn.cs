using UnityEngine;

public class BallReturn : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            Destroy(other.gameObject);

            BallSpawner spawner = FindObjectOfType<BallSpawner>();
            if (spawner != null)
                spawner.SpawnBall();
        }
    }
}
