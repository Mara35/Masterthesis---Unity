using System;
using System.Net;
using UnityEngine;

/// <summary>
/// Class that represents the state of a stream sensor.#
/// Holds information like its <see cref="Id"/>, the rotation <see cref="Quaternion"/>, the <see cref="BatteryPercentage"/> and whether it is <see cref="Connected"/>.
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
            if (value is { x: 0, y: 0, z: 0, w: 0 })
            {
                Connected = false;
            }
            else
            {
                Connected = true;
                LastMessageTime = DateTime.Now;
            }
            _quaternion = value;
        }
    }

    private bool _connected = false;
    public bool Connected { get {
        if (DateTime.Now - LastMessageTime > TimeSpan.FromSeconds(1)) _connected = false;
        return _connected;
    } private set => _connected = value; }
    public DateTime LastMessageTime { get; private set; } = DateTime.MinValue;
    public IPAddress IpAddress { get; set; }
}