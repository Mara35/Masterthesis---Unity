using System;
using System.Linq;
using STREAM;

/// <summary>
/// Static class to handle data that is sent from the VR headset to the stream sensors.
/// Provides methods to create the message contents in form of byte[] by providing parameters
/// </summary>
public static class StreamSendMessages
{
    public static byte[] CreateVibrateOneRequest(VibrationRequest vibrationRequest)
    {
        return new byte[] { 0xFA, 0xFB, 0x02, vibrationRequest.SensorId }
            .Concat(BitConverter.GetBytes(vibrationRequest.Intensity))
            .Concat(BitConverter.GetBytes(vibrationRequest.Seconds)).ToArray();
    }
}