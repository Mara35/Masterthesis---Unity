using UnityEngine;

/// <summary>
/// Steuert Finger, Handgelenk und Ellbogen des XBot per Script.
/// - Finger: Grip()/Release() zum Greifen/Loslassen
/// - Handgelenk: SetWristBend() zum Knicken beim Auf-/Ablegen
/// - Ellbogen: beugt sich automatisch basierend auf der X-Position
///             der Hand (rechts = gestreckt, links = gebeugt)
/// </summary>
public class HandGrip : MonoBehaviour
{
    // -----------------------------------------------------------------------
    [Header("Fingerknochen (je 3 Glieder pro Finger)")]
    [SerializeField] private Transform[] indexBones;
    [SerializeField] private Transform[] middleBones;
    [SerializeField] private Transform[] ringBones;
    [SerializeField] private Transform[] pinkyBones;
    [SerializeField] private Transform[] thumbBones;

    [Header("Greifwinkel (Grad)")]
    [SerializeField] private float fingerGripAngle = 70f;
    [SerializeField] private float thumbGripAngle = 45f;

    [Header("Rotation-Achse (lokal)")]
    [SerializeField] private Vector3 fingerAxis = new Vector3(1, 0, 0);
    [SerializeField] private Vector3 thumbAxis = new Vector3(0, 1, 0);

    [Header("Blend-Geschwindigkeit Finger")]
    [SerializeField] private float blendSpeed = 8f;

    // -----------------------------------------------------------------------
    [Header("Handgelenk-Knick")]
    [Tooltip("mixamorig:RightHand zuweisen.")]
    [SerializeField] private Transform wristBone;
    [SerializeField] private float wristBendAngle = 60f;
    [SerializeField] private Vector3 wristBendAxis = new Vector3(1, 0, 0);
    [SerializeField] private float wristBlendSpeed = 5f;

    private Quaternion wristOpenRot;
    private float currentWristBlend = 0f;
    private float targetWristBlend = 0f;

    // -----------------------------------------------------------------------
    [Header("Ellbogen-Beugung")]
    [Tooltip("mixamorig:RightForeArm zuweisen.")]
    [SerializeField] private Transform foreArmBone;

    [Tooltip("Maximaler Beugewinkel wenn Hand ganz links ist (Grad).")]
    [SerializeField] private float elbowMaxBendAngle = 40f;

    [Tooltip("Achse der Ellbogen-Beugung (lokal). Z=1 meist korrekt für Mixamo.")]
    [SerializeField] private Vector3 elbowBendAxis = new Vector3(0, 0, 1);

    [Tooltip("Blend-Geschwindigkeit der Ellbogen-Beugung.")]
    [SerializeField] private float elbowBlendSpeed = 4f;

    [Tooltip("Referenz auf HandMoverGhost – dessen X-Position steuert die Beugung.")]
    [SerializeField] private Transform handMoverGhost;

    [Tooltip("X-Position der Trennwand (Mitte). Rechts davon = gestreckt, links = gebeugt.")]
    [SerializeField] private float partitionX = 0f;

    [Tooltip("X-Position der linken Seite wo Beugung maximal ist.")]
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

        // --- Handgelenk ---
        if (wristBone != null)
        {
            currentWristBlend = Mathf.Lerp(currentWristBlend, targetWristBlend,
                                            Time.deltaTime * wristBlendSpeed);
            Quaternion bentRot = wristOpenRot * Quaternion.AngleAxis(wristBendAngle, wristBendAxis);
            wristBone.localRotation = Quaternion.Lerp(wristOpenRot, bentRot, currentWristBlend);
        }

        // --- Ellbogen ---
        if (foreArmBone != null && handMoverGhost != null)
        {
            float handX = handMoverGhost.position.x;

            // Rechts von Partition = 0, links zunehmend bis 1
            // Nur aktiv wenn targetElbowEnabled (nicht während ArcReturn)
            float targetElbow = 0f;
            if (targetElbowEnabled && handX < partitionX)
            {
                float range = Mathf.Abs(leftMaxX - partitionX);
                targetElbow = Mathf.Clamp01(Mathf.Abs(handX - partitionX) / range);
            }

            currentElbowBlend = Mathf.Lerp(currentElbowBlend, targetElbow,
                                             Time.deltaTime * elbowBlendSpeed);

            // Rotation NACH IK überschreiben
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