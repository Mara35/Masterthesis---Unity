using UnityEngine;
using STREAM;

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