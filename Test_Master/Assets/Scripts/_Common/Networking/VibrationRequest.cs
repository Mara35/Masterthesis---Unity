namespace STREAM
{
    /// <summary>
    /// wrapper class for a vibration request, sent to the stream sensors
    /// </summary>
    public struct VibrationRequest
    {
        public byte SensorId { get; }
        public float Intensity { get; }
        public float Seconds { get; }

        // seconds = -1 means "no timeout": vibrate until a new request stops it.
        public VibrationRequest(byte sensorId, float intensity, float seconds = -1.0f)
        {
            SensorId = sensorId;
            Intensity = intensity;
            Seconds = seconds;
        }
    }
}
