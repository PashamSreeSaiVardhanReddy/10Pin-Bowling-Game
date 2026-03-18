using UnityEngine;

public class BallSpawner : MonoBehaviour
{
    public GameObject ballPrefab;
    public Transform spawnPoint;

    private GameObject currentBall;

    void Start()
    {
        SpawnBall();
    }

    public void SpawnBall()
    {
        if (currentBall != null)
            Destroy(currentBall);

        currentBall = Instantiate(
            ballPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );
    }
}
