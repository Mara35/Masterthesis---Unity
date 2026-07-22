using UnityEngine;

public class HeadFollowCamera : MonoBehaviour
{
    public Transform vrCamera;
    public Transform headBone;

    void LateUpdate()
    {
        headBone.position = vrCamera.position;
        headBone.rotation = vrCamera.rotation;
    }
}
