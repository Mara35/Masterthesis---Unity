using UnityEngine;

/// <summary>
/// Dreht die Fingerknochen des XBot per Script.
/// Kein Animator, kein Animationsclip nötig.
/// Aufruf: Grip() zum Greifen, Release() zum Loslassen.
///
/// NEU: Handgelenk-Knick – dreht mixamorig:RightHand nach unten damit
///      der Unterarm oben bleibt und nicht durch die Wand geht.
///      Der Knick ist immer aktiv (unabhängig vom Greifen).
/// </summary>
public class HandGrip : MonoBehaviour
{
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

    [Header("Blend-Geschwindigkeit")]
    [SerializeField] private float blendSpeed = 8f;

    [Header("Handgelenk-Knick (gegen Wanddurchdringung)")]
    [Tooltip("mixamorig:RightHand zuweisen.")]
    [SerializeField] private Transform wristBone;

    [Tooltip("Winkel nach unten knicken (Grad). Positiv = Hand nach unten.")]
    [SerializeField] private float wristBendAngle = 60f;

    [Tooltip("Achse des Handgelenk-Knicks (lokal). X=1 ist meist korrekt für Mixamo.")]
    [SerializeField] private Vector3 wristBendAxis = new Vector3(1, 0, 0);

    [Tooltip("Blend-Geschwindigkeit des Handgelenk-Knicks.")]
    [SerializeField] private float wristBlendSpeed = 5f;

    private Quaternion wristOpenRot;
    private float currentWristBlend = 0f;
    private float targetWristBlend = 0f;   // standardmäßig immer aktiv

    private float currentBlend = 0f;
    private float targetBlend = 0f;

    private Quaternion[] indexOpen, middleOpen, ringOpen, pinkyOpen, thumbOpen;

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

    // LateUpdate damit der Knick NACH dem IK-Constraint angewendet wird
    private void LateUpdate()
    {
        currentBlend = Mathf.Lerp(currentBlend, targetBlend, Time.deltaTime * blendSpeed);

        ApplyFinger(indexBones, indexOpen, fingerAxis, fingerGripAngle);
        ApplyFinger(middleBones, middleOpen, fingerAxis, fingerGripAngle);
        ApplyFinger(ringBones, ringOpen, fingerAxis, fingerGripAngle);
        ApplyFinger(pinkyBones, pinkyOpen, fingerAxis, fingerGripAngle);
        ApplyFinger(thumbBones, thumbOpen, thumbAxis, thumbGripAngle);

        if (wristBone != null)
        {
            currentWristBlend = Mathf.Lerp(currentWristBlend, targetWristBlend,
                                            Time.deltaTime * wristBlendSpeed);
            Quaternion bentRot = wristOpenRot * Quaternion.AngleAxis(wristBendAngle, wristBendAxis);
            wristBone.localRotation = Quaternion.Lerp(wristOpenRot, bentRot, currentWristBlend);
        }
    }

    public void Grip() => targetBlend = 1f;
    public void Release() => targetBlend = 0f;

    public void SetWristBend(bool active) => targetWristBlend = active ? 1f : 0f;

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