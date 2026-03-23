using UnityEngine;

public class ArmPlaybackController : MonoBehaviour
{
    [SerializeField] private ArmPlaybackSource armSource;
    [SerializeField] private Transform targetBone;
    [SerializeField] private bool useBaseRotation = true;

    private Quaternion _baseRotation;

    private void Start()
    {
        if (targetBone != null)
            _baseRotation = targetBone.localRotation;
    }

    private void Update()
    {
        if (armSource == null || !armSource.HasData || targetBone == null)
            return;

        Quaternion q = armSource.CurrentFrame.Rotation;

        if (useBaseRotation)
            targetBone.localRotation = _baseRotation * q;
        else
            targetBone.localRotation = q;
    }
}