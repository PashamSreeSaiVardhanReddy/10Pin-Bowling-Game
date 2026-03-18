using UnityEngine;
using System.Collections.Generic;

public class PinSetManager : MonoBehaviour
{
    [Header("Pin Setup")]
    public GameObject pinPrefab;

    [Header("Runtime Pins")]
    public List<GameObject> spawnedPins = new List<GameObject>();

    [Header("Spawn Settings")]
    public float spacing = 0.3048f; // 12 inches

    [ContextMenu("Generate Pin Positions")]
    public void GeneratePinPositions()
    {
        // Clear old markers
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        int index = 0;

        // 4 rows: 1-2-3-4 pins
        for (int row = 0; row < 4; row++)
        {
            float z = row * spacing;
            float startX = -row * spacing * 0.5f;

            for (int col = 0; col <= row; col++)
            {
                GameObject marker = new GameObject("PinPoint_" + index++);
                marker.transform.parent = transform;
                marker.transform.localPosition = new Vector3(
                    startX + col * spacing,
                    0f,
                    z
                );
            }
        }

        Debug.Log("Pin spawn positions generated.");
    }

    void Start()
    {
        SpawnPins();
    }

    public void SpawnPins()
    {
        ClearPins();

        foreach (Transform point in transform)
        {
            GameObject pin = Instantiate(
                pinPrefab,
                point.position,
                point.rotation
            );
            spawnedPins.Add(pin);
        }

        Debug.Log("Pins spawned: " + spawnedPins.Count);
    }

    public void ClearPins()
    {
        foreach (GameObject pin in spawnedPins)
        {
            if (pin != null)
                Destroy(pin);
        }

        spawnedPins.Clear();
    }
}
