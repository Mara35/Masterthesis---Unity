using UnityEngine;

/// <summary>
/// Plays a recorded full-body motion onto the avatar. Maps each recorded sensor id to a bone
/// (via the Inspector <see cref="SensorBinding"/>s) and, every frame, applies the current
/// <see cref="FullBodyFrame"/> rotations. Each bone's starting rotation is cached once so the
/// recorded rotation is applied relative to it (baseRotation * recorded).
/// </summary>

public class FullBodyPlaybackController : MonoBehaviour
{
    [System.Serializable]
    public class SensorBinding
    {
        public int sensorId;
        public Transform targetBone;
        public bool useBaseRotation = true;
        public Quaternion baseRotation;
    }

    [SerializeField] private FullBodyPlaybackSource playbackSource;

    [Header("Torso")]
    [SerializeField] private SensorBinding hips;
    [SerializeField] private SensorBinding chest;

    [Header("Right Arm")]
    [SerializeField] private SensorBinding rightUpperArm;
    [SerializeField] private SensorBinding rightForeArm;

    [Header("Left Arm")]
    [SerializeField] private SensorBinding leftUpperArm;
    [SerializeField] private SensorBinding leftForeArm;

    [Header("Right Leg")]
    [SerializeField] private SensorBinding rightUpperLeg;
    [SerializeField] private SensorBinding rightLowerLeg;

    [Header("Left Leg")]
    [SerializeField] private SensorBinding leftUpperLeg;
    [SerializeField] private SensorBinding leftLowerLeg;

    private void Start()
    {
        CacheBaseRotations();
    }

    [ContextMenu("Cache Base Rotations")]
    public void CacheBaseRotations()
    {
        CacheBaseRotation(hips);
        CacheBaseRotation(chest);

        CacheBaseRotation(rightUpperArm);
        CacheBaseRotation(rightForeArm);

        CacheBaseRotation(leftUpperArm);
        CacheBaseRotation(leftForeArm);

        CacheBaseRotation(rightUpperLeg);
        CacheBaseRotation(rightLowerLeg);

        CacheBaseRotation(leftUpperLeg);
        CacheBaseRotation(leftLowerLeg);
    }

    private void Update()
    {
        if (playbackSource == null || !playbackSource.HasData)
            return;

        FullBodyFrame frame = playbackSource.CurrentFrame;

        ApplyBinding(frame, hips);
        ApplyBinding(frame, chest);

        ApplyBinding(frame, rightUpperArm);
        ApplyBinding(frame, rightForeArm);

        ApplyBinding(frame, leftUpperArm);
        ApplyBinding(frame, leftForeArm);

        ApplyBinding(frame, rightUpperLeg);
        ApplyBinding(frame, rightLowerLeg);

        ApplyBinding(frame, leftUpperLeg);
        ApplyBinding(frame, leftLowerLeg);
    }

    private void ApplyBinding(FullBodyFrame frame, SensorBinding binding)
    {
        if (binding == null || binding.targetBone == null)
            return;

        if (!frame.Rotations.TryGetValue(binding.sensorId, out Quaternion q))
            return; // no recorded data for this sensor id in this frame

        // Apply relative to the cached rest rotation, unless the binding wants the raw value.
        if (binding.useBaseRotation)
            binding.targetBone.localRotation = binding.baseRotation * q;
        else
            binding.targetBone.localRotation = q;
    }

    private void CacheBaseRotation(SensorBinding binding)
    {
        if (binding == null || binding.targetBone == null)
            return;

        binding.baseRotation = binding.targetBone.localRotation;
    }
}