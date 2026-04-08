using System.Net;

[System.Serializable]
public class GloveSensorData
{
    public byte Id;
    public IPAddress IpAddress;

    // Thumb
    public float Thumb_MCP;
    public float Thumb_PIP;

    // Index
    public float Index_MCP;
    public float Index_PIP;

    // Middle
    public float Middle_MCP;
    public float Middle_PIP;

    // Ring
    public float Ring_MCP;
    public float Ring_PIP;

    // Pinky
    public float Pinky_MCP;
    public float Pinky_PIP;
}