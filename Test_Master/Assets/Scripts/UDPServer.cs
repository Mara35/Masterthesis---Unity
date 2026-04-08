using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using STREAM;
using UnityEngine;

public class UDPServer : MonoBehaviour
{
    [SerializeField] private int listenPort = 9001;
    private UdpClient _udpClient;

    public readonly Dictionary<int, StreamSensor> SensorsMap = new();
    public readonly Dictionary<int, GloveSensorData> GloveMap = new();

    private Thread _readThread;
    private bool _reading = false;

    private void OnEnable()
    {
        _reading = false;
        _udpClient?.Close();

        _udpClient = new UdpClient(listenPort);

        if (_readThread is { IsAlive: true })
        {
            _readThread.Interrupt();
        }

        _reading = true;
        _readThread = new Thread(ThreadFunction);
        _readThread.IsBackground = true;
        _readThread.Start();
    }

    private void OnDisable()
    {
        _reading = false;

        if (_readThread is { IsAlive: true })
        {
            _readThread.Interrupt();
        }

        _udpClient?.Close();
    }

    private void ThreadFunction()
    {
        try
        {
            while (_reading)
            {
                var clientEndpoint = new IPEndPoint(IPAddress.Any, 0);
                var receivedBytes = _udpClient.Receive(ref clientEndpoint);

                if (receivedBytes == null || receivedBytes.Length == 0)
                    continue;

                var receivedList = receivedBytes.ToList();

                // -------- IMU Quaternion --------
                if (StreamReceiveMessageTypes.Matches(StreamReceiveMessageType.Quaternion, receivedList))
                {
                    var (nr, quat) = StreamReceiveMessageTypes.DecodeQuaternionData(receivedBytes);

                    if (!SensorsMap.ContainsKey(nr))
                    {
                        SensorsMap[nr] = new StreamSensor
                        {
                            Id = (byte)nr,
                        };
                    }

                    SensorsMap[nr].IpAddress = clientEndpoint.Address;
                    SensorsMap[nr].Quaternion = quat;

                    continue;
                }

                // -------- Glove Daten --------
                if (StreamReceiveMessageTypes.Matches(StreamReceiveMessageType.Glove, receivedList))
                {
                    var gloveData = StreamReceiveMessageTypes.DecodeGloveData(receivedBytes);

                    if (!GloveMap.ContainsKey(gloveData.Id))
                    {
                        GloveMap[gloveData.Id] = new GloveSensorData
                        {
                            Id = gloveData.Id
                        };
                    }

                    GloveMap[gloveData.Id].IpAddress = clientEndpoint.Address;

                    GloveMap[gloveData.Id].Thumb_MCP = gloveData.Thumb_MCP;
                    GloveMap[gloveData.Id].Thumb_PIP = gloveData.Thumb_PIP;

                    GloveMap[gloveData.Id].Index_MCP = gloveData.Index_MCP;
                    GloveMap[gloveData.Id].Index_PIP = gloveData.Index_PIP;

                    GloveMap[gloveData.Id].Middle_MCP = gloveData.Middle_MCP;
                    GloveMap[gloveData.Id].Middle_PIP = gloveData.Middle_PIP;

                    GloveMap[gloveData.Id].Ring_MCP = gloveData.Ring_MCP;
                    GloveMap[gloveData.Id].Ring_PIP = gloveData.Ring_PIP;

                    GloveMap[gloveData.Id].Pinky_MCP = gloveData.Pinky_MCP;
                    GloveMap[gloveData.Id].Pinky_PIP = gloveData.Pinky_PIP;

                    continue;
                }

                Debug.LogWarning($"Unknown UDP packet received. Length: {receivedBytes.Length}");
            }
        }
        catch (ThreadInterruptedException)
        {
            // Normal beim Stoppen
        }
        catch (SocketException)
        {
            // Normal beim Stoppen / Schlieﬂen
        }
        catch (Exception e)
        {
            Debug.LogError($"UDPServer Error: {e.Message}");
        }
    }

    private void OnApplicationQuit()
    {
        _reading = false;
        _udpClient?.Close();
    }

    public void SendVibrateOne(VibrationRequest vibrationRequest)
    {
        if (!SensorsMap.TryGetValue(vibrationRequest.SensorId, out var sensor))
            return;

        var data = StreamSendMessages.CreateVibrateOneRequest(vibrationRequest);
        _udpClient.Send(data, data.Length, new IPEndPoint(sensor.IpAddress, listenPort));
    }

    public bool TryGetSensor(int sensorId, out StreamSensor sensor)
    {
        return SensorsMap.TryGetValue(sensorId, out sensor);
    }

    public bool TryGetGlove(int gloveId, out GloveSensorData glove)
    {
        return GloveMap.TryGetValue(gloveId, out glove);
    }
}