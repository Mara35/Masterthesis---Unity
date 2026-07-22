using UnityEngine;
using STREAM;

/// <summary>
/// Thin accessor over <see cref="UDPServer"/>. Lets consumers query the latest
/// <see cref="StreamSensor"/> or <see cref="GloveSensorData"/> by id without touching the
/// server's maps directly.
/// </summary>

public class UDPCommunicationGlove : MonoBehaviour
{
    [SerializeField] private UDPServer udpServer;

    private void Awake()
    {
        if (udpServer == null)
            udpServer = GetComponent<UDPServer>();
    }

    public bool TryGetSensor(int sensorId, out StreamSensor sensor)
    {
        sensor = null;

        if (udpServer == null)
            return false;

        return udpServer.SensorsMap.TryGetValue(sensorId, out sensor);
    }

    public bool TryGetGloveData(int gloveId, out GloveSensorData gloveData)
    {
        gloveData = null;

        if (udpServer == null)
            return false;

        return udpServer.GloveMap.TryGetValue(gloveId, out gloveData);
    }
}