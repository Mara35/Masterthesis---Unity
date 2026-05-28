using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class StreamSensorRotationController : MonoBehaviour
{
    [SerializeField] private UDPServer streamController;
    [SerializeField] private StreamSensorModel model;

    // Hier passen wir die Raumachsen exakt an dein Setup an
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
            target.calibrationRot =
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
}