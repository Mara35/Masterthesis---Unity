using STREAM;
using UnityEngine;

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