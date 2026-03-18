using UnityEngine;

public class BallTestThrow : MonoBehaviour
{
    public float throwForce = 500f;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            GetComponent<Rigidbody>().AddForce(Vector3.forward * throwForce);
        }
    }
}
