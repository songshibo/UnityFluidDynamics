using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    Transform center;
    Quaternion rot;
    // Start is called before the first frame update
    void Start()
    {
        center = transform.parent.transform;
        rot = center.rotation;
    }

    // Update is called once per frame
    void Update()
    {
        float angle = Input.GetAxis("Mouse ScrollWheel") * 3.0f;
        rot *= Quaternion.AngleAxis(angle, Vector3.up);
        center.rotation = Quaternion.Slerp(center.rotation, rot, Time.deltaTime * 3.0f);

        transform.LookAt(new Vector3(0, 0, 0), transform.up);
    }
}
