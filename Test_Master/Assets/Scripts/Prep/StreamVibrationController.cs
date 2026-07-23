using STREAM;
using UnityEngine;

/// <summary>
/// DEBUG (SampleScene). Triggers a sensor's vibration motor when a stream collision is reported
/// (OnStreamCollisionEnter/Exit). A wiring test, not part of the training scenes.
/// </summary>
public class StreamVibrationController : MonoBehaviour, IStreamCollisionListener
{
    [SerializeField] private UDPServer streamController;
    [SerializeField] private StreamSensorModel model;
    private const int IndexOffset = 1000;


    private void OnDisable()
    {
        foreach (var sensorModel in model.targets)
        {
            sensorModel.collisionReporter.RemoveCollisionListener(this);
        }
    }

    private void Update()
    {
        foreach (var sensorModel in model.targets)
        {
            sensorModel.collisionReporter.AddCollisionListener(this, sensorModel.index + IndexOffset);
        }
    }

    public void OnStreamCollisionEnter(int sensorId, Collider c)
    {
        if(c.gameObject.CompareTag("Player")) return;
        streamController.SendVibrateOne(new VibrationRequest((byte)(sensorId - IndexOffset), 1.0f, 1.0f));
    }

    public void OnStreamCollisionExit(int sensorId, Collider c)
    {
    }
}