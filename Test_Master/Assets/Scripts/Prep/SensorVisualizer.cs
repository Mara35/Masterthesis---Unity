using STREAM;
using UnityEngine;

/// <summary>
/// DEBUG (SampleScene). Shows/hides the raw sensor objects on the avatar depending on whether each
/// sensor is currently connected, by reading UDPServer's SensorsMap each frame. A visualization
/// aid, not part of the training scenes.
/// </summary>

public class SensorVisualizer : MonoBehaviour
{
    [SerializeField] private UDPServer streamController;
    [SerializeField] private StreamSensorModel model;

    private void Update()
    {
        foreach (var sensorModel in model.targets)
        {
            sensorModel.deviceRotation.gameObject.SetActive(
                streamController.SensorsMap.ContainsKey(sensorModel.index) && streamController.SensorsMap[sensorModel.index].Connected
            );
        }
    }

}