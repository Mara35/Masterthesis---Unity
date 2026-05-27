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

        // Koordinatensystem-Konvertierung
        Quaternion converted = new Quaternion(-raw.x, -raw.y, raw.z, raw.w);

        // Montagekorrektur
        Quaternion mountingOffset = Quaternion.Euler(180f, 0f, 0f);

        Quaternion result = converted * mountingOffset;

        // Oberarm-Sensor: Y-Achse gespiegelt
        if (kv.Key == 3) 
        {
            result = new Quaternion(result.x, -result.y, result.z, -result.w);
        }

        return result;
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