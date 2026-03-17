using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using STREAM;
using UnityEngine;

public class UDPServer : MonoBehaviour {
    [SerializeField] private int listenPort = 9001;
    private UdpClient _udpClient;
    
    public readonly Dictionary<int, StreamSensor> SensorsMap = new();

    private Thread _readThread;
    private bool _reading = false;


    private void OnEnable() {
        _reading = false;
        _udpClient?.Close();
        _udpClient = new UdpClient(listenPort);
        if (_readThread is { IsAlive: true }) {
            _readThread.Interrupt();
        }
        _reading = true;
        _readThread = new Thread(ThreadFunction);
        _readThread.Start();
    }

    private void OnDisable() {
        _reading = false;
        if (_readThread is { IsAlive: true }) {
            _readThread.Interrupt();
        }

        _udpClient?.Close();
    }

    private void ThreadFunction() {
        try {
            while (_reading) {
                var clientEndpoint = new IPEndPoint(IPAddress.Any, 0);
                var receivedBytes = _udpClient.Receive(ref clientEndpoint);
                if (StreamReceiveMessageTypes.Matches(StreamReceiveMessageType.Quaternion, receivedBytes.ToList()))
                {
                    var (nr, quat) = StreamReceiveMessageTypes.DecodeQuaternionData(receivedBytes);
                    if (!SensorsMap.ContainsKey(nr))
                    {
                        SensorsMap[nr] = new StreamSensor
                        {
                            Id = (byte) nr,
                        };
                    }
                    SensorsMap[nr].IpAddress = clientEndpoint.Address;
                    SensorsMap[nr].Quaternion = quat;
                }
            }
        } catch (Exception e) {
            Debug.LogError($"Error: {e.Message}");
        }
    }


    // Close the UDP client when the application quits
    private void OnApplicationQuit()
    {
        _udpClient?.Close();
    }

    public void SendVibrateOne(VibrationRequest vibrationRequest)
    {
        if(!SensorsMap.TryGetValue(vibrationRequest.SensorId, out var sensor)) return;
        var data = StreamSendMessages.CreateVibrateOneRequest(vibrationRequest);
        _udpClient.Send(data, data.Length, new IPEndPoint(sensor.IpAddress, listenPort));
    }
}