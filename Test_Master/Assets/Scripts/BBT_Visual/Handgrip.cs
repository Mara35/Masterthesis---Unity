using UnityEngine;

/// <summary>
/// Controls the XBot's fingers, wrist, and elbow via script.
/// - Fingers: Grip()/Release() to grasp/release
/// - Wrist: SetWristBend() to bend when picking up/putting down
/// - Elbow: bends automatically based on the X-position of the hand (right = extended, left = bent)
/// </summary>
public class HandGrip : MonoBehaviour
{
    // -----------------------------------------------------------------------
    [Header("Finger bones (3 joints per finger)")]
    [SerializeField] private Transform[] indexBones;
    [SerializeField] private Transform[] middleBones;
    [SerializeField] private Transform[] ringBones;
    [SerializeField] private Transform[] pinkyBones;
    [SerializeField] private Transform[] thumbBones;

    [Header("Grip angle (degrees)")]
    [SerializeField] private float fingerGripAngle = 70f;
    [SerializeField] private float thumbGripAngle = 45f;

    [Header("Rotation axis (local)")]
    [SerializeField] private Vector3 fingerAxis = new Vector3(1, 0, 0);
    [SerializeField] private Vector3 thumbAxis = new Vector3(0, 1, 0);

    [Header("Blend speed: Finger")]
    [SerializeField] private float blendSpeed = 8f;

    // -----------------------------------------------------------------------
    [Header("Wrist bend")]
    [Tooltip("mixamorig:RightHand assign")]
    [SerializeField] private Transform wristBone;
    [SerializeField] private float wristBendAngle = 60f;
    [SerializeField] private Vector3 wristBendAxis = new Vector3(1, 0, 0);
    [SerializeField] private float wristBlendSpeed = 5f;

    private Quaternion wristOpenRot;
    private float currentWristBlend = 0f;
    private float targetWristBlend = 0f;

    // -----------------------------------------------------------------------
    [Header("Elbow bend")]
    [Tooltip("mixamorig:RightForeArm assign")]
    [SerializeField] private Transform foreArmBone;

    [Tooltip("Maximum bending angle when the hand is all the way to the left (degrees)")]
    [SerializeField] private float elbowMaxBendAngle = 40f;

    [Tooltip("Axis of elbow flexion (local). Z=1 is usually correct for Mixamo")]
    [SerializeField] private Vector3 elbowBendAxis = new Vector3(0, 0, 1);

    [Tooltip("Blend speed of elbow flexion")]
    [SerializeField] private float elbowBlendSpeed = 4f;

    [Tooltip("Reference to HandMoverGhost – its X-position controls the bending")]
    [SerializeField] private Transform handMoverGhost;

    [Tooltip("X-position of the partition (center). To the right of it = extended, to the left = flexed.")]
    [SerializeField] private float partitionX = 0f;

    [Tooltip("The X-coordinate of the left side where the bending is greatest.")]
    [SerializeField] private float leftMaxX = -0.3f;

    private float currentElbowBlend = 0f;
    private bool targetElbowEnabled = true;

    // -----------------------------------------------------------------------
    private float currentBlend = 0f;
    private float targetBlend = 0f;

    private Quaternion[] indexOpen, middleOpen, ringOpen, pinkyOpen, thumbOpen;

    // -----------------------------------------------------------------------
    private void Awake()
    {
        indexOpen = StoreOpen(indexBones);
        middleOpen = StoreOpen(middleBones);
        ringOpen = StoreOpen(ringBones);
        pinkyOpen = StoreOpen(pinkyBones);
        thumbOpen = StoreOpen(thumbBones);

        if (wristBone != null)
            wristOpenRot = wristBone.localRotation;
    }

    private void LateUpdate()
    {
        // --- Finger ---
        currentBlend = Mathf.Lerp(currentBlend, targetBlend, Time.deltaTime * blendSpeed);
        ApplyFinger(indexBones, indexOpen, fingerAxis, fingerGripAngle);
        ApplyFinger(middleBones, middleOpen, fingerAxis, fingerGripAngle);
        ApplyFinger(ringBones, ringOpen, fingerAxis, fingerGripAngle);
        ApplyFinger(pinkyBones, pinkyOpen, fingerAxis, fingerGripAngle);
        ApplyFinger(thumbBones, thumbOpen, thumbAxis, thumbGripAngle);

        // --- wrist ---
        if (wristBone != null)
        {
            currentWristBlend = Mathf.Lerp(currentWristBlend, targetWristBlend,
                                            Time.deltaTime * wristBlendSpeed);
            Quaternion bentRot = wristOpenRot * Quaternion.AngleAxis(wristBendAngle, wristBendAxis);
            wristBone.localRotation = Quaternion.Lerp(wristOpenRot, bentRot, currentWristBlend);
        }

        // --- elbow ---
        if (foreArmBone != null && handMoverGhost != null)
        {
            float handX = handMoverGhost.position.x;

            // Right side of partition = 0, left side increasing up to 1
            // Active only if targetElbowEnabled (not during ArcReturn)
            float targetElbow = 0f;
            if (targetElbowEnabled && handX < partitionX)
            {
                float range = Mathf.Abs(leftMaxX - partitionX);
                targetElbow = Mathf.Clamp01(Mathf.Abs(handX - partitionX) / range);
            }

            currentElbowBlend = Mathf.Lerp(currentElbowBlend, targetElbow,
                                             Time.deltaTime * elbowBlendSpeed);

            // Override rotation after IK
            Quaternion currentRot = foreArmBone.localRotation;
            Quaternion bentRot = currentRot * Quaternion.AngleAxis(
                                        elbowMaxBendAngle * currentElbowBlend, elbowBendAxis);
            foreArmBone.localRotation = bentRot;
        }
    }

    // -----------------------------------------------------------------------
    public void Grip() => targetBlend = 1f;
    public void Release() => targetBlend = 0f;
    public void SetWristBend(bool active) => targetWristBlend = active ? 1f : 0f;
    public void SetElbowBend(bool active) => targetElbowEnabled = active;

    // -----------------------------------------------------------------------
    private Quaternion[] StoreOpen(Transform[] bones)
    {
        if (bones == null) return new Quaternion[0];
        var result = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            result[i] = bones[i] != null ? bones[i].localRotation : Quaternion.identity;
        return result;
    }

    private void ApplyFinger(Transform[] bones, Quaternion[] openRots, Vector3 axis, float angle)
    {
        if (bones == null) return;
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null) continue;
            Quaternion gripRot = openRots[i] * Quaternion.AngleAxis(angle, axis);
            bones[i].localRotation = Quaternion.Lerp(openRots[i], gripRot, currentBlend);
        }
    }
}