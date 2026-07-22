using UnityEngine;

/// <summary>
/// Pins the avatar's head bone to the VR camera every LateUpdate, so the avatar's head follows the
/// headset. LateUpdate is used so it runs after animation/IK has posed the skeleton for the frame.
/// </summary>

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
