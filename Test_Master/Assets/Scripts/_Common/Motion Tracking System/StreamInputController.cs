using STREAM;
using UnityEngine;

/// <summary>
/// controller that applies controller input to the stream character, currently used to let the connected sensors vibrate or recalibrate.
/// </summary>
public class StreamInputController : MonoBehaviour
{
    [SerializeField] private UDPServer connection;
    [SerializeField] private StreamSensorRotationController rotationController;
    private bool _vibrationTriggerPressed;
    private bool _wasVibrationTriggerPressed;
    private bool _recalibrateTriggerPressed;
    private bool _wasRecalibrateTriggerPressed;
    
    private void Update()
    {
        // Space = vibrate all sensors once, C = recalibrate. Edge-detected so holding the key
        // fires only once per press (pressed && !wasPressed).
        _vibrationTriggerPressed = Input.GetKey(KeyCode.Space);
        _recalibrateTriggerPressed = Input.GetKey(KeyCode.C);

        if (_vibrationTriggerPressed && !_wasVibrationTriggerPressed)
        {
            foreach (var sensor in connection.SensorsMap.Values)
            {
                connection.SendVibrateOne(new VibrationRequest(sensor.Id, 1.0f, 1.0f));
            }
        }
        _wasVibrationTriggerPressed = _vibrationTriggerPressed;

        if (_recalibrateTriggerPressed && !_wasRecalibrateTriggerPressed)
        {
            rotationController.Recalibrate();
        }
        _wasRecalibrateTriggerPressed = _recalibrateTriggerPressed;
    }
}