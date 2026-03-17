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


        public VibrationRequest(byte sensorId, float intensity, float seconds = -1.0f)
        {
            SensorId = sensorId;
            Intensity = intensity;
            Seconds = seconds;
        }
    }
}
