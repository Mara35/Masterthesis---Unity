using System;
using System.Net;
using UnityEngine;

/// <summary>
/// Represents the runtime state of a stream sensor: its <see cref="Id"/>, current rotation
/// <see cref="Quaternion"/>, and whether it is <see cref="Connected"/>.
/// </summary>
public class StreamSensor
{
    public byte Id { get; set; } = 255;
    private Quaternion _quaternion = new (0, 0, 0, 0);

    public Quaternion Quaternion
    {
        get => _quaternion;
        set
        {
            // An all-zero quaternion is the sensor's "no data" marker -> treat as disconnected.
            if (value is { x: 0, y: 0, z: 0, w: 0 })
            {
                Connected = false;
            }
            else
            {
                Connected = true;
                LastMessageTime = DateTime.Now; // feeds the timeout in the Connected getter
            }
            _quaternion = value;
        }
    }

    private bool _connected = false;
    public bool Connected { get {
         // Auto-expire if no packet has arrived for over a second.
        if (DateTime.Now - LastMessageTime > TimeSpan.FromSeconds(1)) _connected = false;
        return _connected;
    } private set => _connected = value; }
    public DateTime LastMessageTime { get; private set; } = DateTime.MinValue;
    public IPAddress IpAddress { get; set; }
}