using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Applies the live IMU rotations to the avatar bones. Converts each sensor quaternion from the
/// sensor's coordinate frame into Unity's, and offsets it by a per-strap calibration captured
/// on recalibration ("c" key). An optional reference pose lets calibration snap the avatar into a
/// chosen pose (e.g. arm forward) instead of the default bind/T-pose.
/// </summary>

public class StreamSensorRotationController : MonoBehaviour
{
    [SerializeField] private UDPServer streamController;
    [SerializeField] private StreamSensorModel model;

    // Reference pose the avatar snaps to on recalibration.
    // No entry for an index -> Identity = bind/T-pose. Entry present -> that localRotation.
    [System.Serializable]
    public struct ReferencePose
    {
        [Tooltip("strapId of the target (must match model.targets[].index passen)")]
        public int index;  // strapId, must match model.targets[].index
        [Tooltip("Desired local rotation of the bone during calibration")]
        public Quaternion localRotation; // desired bone localRotation at calibration
    }

    [Header("--- Reference pose")]
    [Tooltip("Right-click > “Capture Reference Pose” to capture the pose currently set in the editor.")]
    [SerializeField] private List<ReferencePose> referencePoses = new List<ReferencePose>();

    private Quaternion GetReferencePose(int index)
    {
        foreach (var rp in referencePoses)
            if (rp.index == index) return rp.localRotation;
        return Quaternion.identity;
    }

    // Reads every sensor rotation and maps it from the IMU frame into Unity's frame.
    private IDictionary<int, Quaternion> Quaternions => streamController.SensorsMap.ToDictionary(
        kv => kv.Key,
        kv => {
            Quaternion raw = kv.Value.Quaternion;

            // Axis remap: sensor (x,y,z,w) -> Unity's left-handed frame.
            Quaternion converted = new Quaternion(-raw.y, raw.x, raw.z, raw.w);

            // Fixed offset for how the sensor sits on the strap (its mounting orientation).
            Quaternion mountingOffset = Quaternion.Euler(0f, -90f, 180f);

            return converted * mountingOffset;
        }
    );

    // Captures the calibration offset per strap so that, right now, each bone lands on its
    // reference pose (or the T-pose if none is set).
    public void Recalibrate()
    {
        foreach (var target in model.targets.Where(target => Quaternions.ContainsKey(target.index)))
        {
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
        // Each frame: bone = calibrationOffset * sensorRotation * inverse(deviceRotation).
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

    // Editor helper: pose the avatar forward in Edit mode, then right-click the component
    // > "Capture Reference Pose" to store the current bone rotations as the reference.
    [ContextMenu("Capture Reference Pose From Current Avatar")]
    private void CaptureReferencePose()
    {
        if (model == null || model.targets == null)
        {
            Debug.LogWarning("[Recalibrate] model/targets not set—cannot capture anything.");
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
        Debug.Log($"[Recalibrate] {referencePoses.Count} reference pose(s) captured");
    }

    [ContextMenu("Clear Reference Pose (Back to T pose)")]
    private void ClearReferencePose()
    {
        referencePoses.Clear();
        Debug.Log("[Recalibrate] Reference poses cleared—Null jumps back to the Bind/T pose.");
    }
}