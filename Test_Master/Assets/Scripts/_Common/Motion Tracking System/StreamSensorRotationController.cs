using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StreamSensorRotationController : MonoBehaviour
{
    [SerializeField] private UDPServer streamController;
    [SerializeField] private StreamSensorModel model;

    // -----------------------------------------------------------------
    // Referenzpose: Pose, in die der Avatar beim Nullen ("c") springt.
    // Leer / kein Eintrag fuer einen Index  ->  Identity = Bind-/T-Pose (wie bisher).
    // Eintrag vorhanden                     ->  diese localRotation (z.B. "Arm vorne").
    // -----------------------------------------------------------------
    [System.Serializable]
    public struct ReferencePose
    {
        [Tooltip("strapId des Targets (muss zu model.targets[].index passen)")]
        public int index;
        [Tooltip("Gewuenschte localRotation des Bones bei Kalibrierung")]
        public Quaternion localRotation;
    }

    [Header("--- Referenzpose (z.B. Arm horizontal vorne) ---")]
    [Tooltip("Per Rechtsklick > 'Capture Reference Pose' aus der aktuell im Editor gesetzten Pose fuellen.")]
    [SerializeField] private List<ReferencePose> referencePoses = new List<ReferencePose>();

    private Quaternion GetReferencePose(int index)
    {
        foreach (var rp in referencePoses)
            if (rp.index == index) return rp.localRotation;
        return Quaternion.identity; // Fallback: Verhalten exakt wie vorher (Bind-/T-Pose)
    }

    private IDictionary<int, Quaternion> Quaternions => streamController.SensorsMap.ToDictionary(
        kv => kv.Key,
        kv => {
            Quaternion raw = kv.Value.Quaternion;

            Quaternion converted = new Quaternion(-raw.y, raw.x, raw.z, raw.w);
            Quaternion mountingOffset = Quaternion.Euler(0f, -90f, 180f);

            return converted * mountingOffset;
        }
    );

    public void Recalibrate()
    {
        foreach (var target in model.targets.Where(target => Quaternions.ContainsKey(target.index)))
        {
            // EINZIGE inhaltliche Aenderung: gewuenschte Referenzpose vorne dranmultiplizieren.
            // refPose = Identity  ->  identisch zum urspruenglichen Verhalten (T-Pose).
            target.calibrationRot =
                GetReferencePose(target.index) *
                Quaternion.Inverse(
                    Quaternions[target.index] *
                    Quaternion.Inverse(target.deviceRotation.localRotation)
                );
        }
    }

    private void Update()
    {
        foreach (var target in model.targets.Where(target => Quaternions.ContainsKey(target.index)))
        {
            target.targetTransform.SetLocalPositionAndRotation(
                target.targetTransform.localPosition,
                target.calibrationRot *
                Quaternions[target.index] *
                Quaternion.Inverse(target.deviceRotation.localRotation)
            );
        }
    }

    // -----------------------------------------------------------------
    // Editor-Helfer: aktuelle Bone-Posen als Referenz einfangen.
    // Avatar im EDIT-Mode (nicht Play) in die Vorwaerts-Pose bringen,
    // dann Rechtsklick auf die Komponente > "Capture Reference Pose".
    // -----------------------------------------------------------------
    [ContextMenu("Capture Reference Pose From Current Avatar")]
    private void CaptureReferencePose()
    {
        if (model == null || model.targets == null)
        {
            Debug.LogWarning("[Recalibrate] model/targets nicht gesetzt - kann nichts einfangen.");
            return;
        }

        referencePoses.Clear();
        foreach (var target in model.targets)
        {
            if (target.targetTransform == null) continue;
            referencePoses.Add(new ReferencePose
            {
                index = target.index,
                localRotation = target.targetTransform.localRotation
            });
        }
        Debug.Log($"[Recalibrate] {referencePoses.Count} Referenzpose(n) eingefangen.");
    }

    [ContextMenu("Clear Reference Pose (zurueck zu T-Pose)")]
    private void ClearReferencePose()
    {
        referencePoses.Clear();
        Debug.Log("[Recalibrate] Referenzposen geleert - Nullen springt wieder in Bind-/T-Pose.");
    }
}