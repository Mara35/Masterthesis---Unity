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
    [SerializeField] private List<int> listenPorts = new List<int> { 9001 };


    private List<UdpClient> _udpClients = new List<UdpClient>();
    private List<Thread> _readThreads = new List<Thread>();

    public readonly Dictionary<int, StreamSensor> SensorsMap = new();
    public readonly Dictionary<int, GloveSensorData> GloveMap = new();

    // MESS: Empfangs-Zeitstempel (ms) pro Gerät, gesetzt im Empfangs-Thread.
    public readonly Dictionary<int, double> SensorRecvMs = new();
    public readonly Dictionary<int, double> GloveRecvMs = new();

    // MESS: gemeinsame monotone Uhr für Empfangs-Thread und Main-Thread.
    public static double NowMs() =>
        System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

    private bool _reading = false;

    private void OnEnable()
    {
        CleanUp();
        _reading = true;

        foreach (int port in listenPorts)
        {
            try
            {
                var client = new UdpClient(port);
                _udpClients.Add(client);


                Thread thread = new Thread(() => ThreadFunction(client, port));
                thread.IsBackground = true;
                thread.Start();
                _readThreads.Add(thread);

                Debug.Log($"UDP server is successfully waiting on port: {port}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error opening port {port}: {e.Message}");
            }
        }
    }

    private void OnDisable()
    {
        CleanUp();
    }

    private void CleanUp()
    {
        _reading = false;

        foreach (var thread in _readThreads)
        {
            if (thread is { IsAlive: true })
            {
                thread.Interrupt();
            }
        }
        _readThreads.Clear();

        foreach (var client in _udpClients)
        {
            client?.Close();
        }
        _udpClients.Clear();
    }

    private void ThreadFunction(UdpClient client, int localPort)
    {
        try
        {
            while (_reading)
            {
                var clientEndpoint = new IPEndPoint(IPAddress.Any, 0);
                var receivedBytes = client.Receive(ref clientEndpoint);

                if (receivedBytes == null || receivedBytes.Length == 0)
                    continue;

                double recvMs = NowMs();   // MESS: Empfangszeit sofort nehmen

                var receivedList = receivedBytes.ToList();

                // -------- IMU Quaternion --------
                if (StreamReceiveMessageTypes.Matches(StreamReceiveMessageType.Quaternion, receivedList))
                {
                    var (nr, quat) = StreamReceiveMessageTypes.DecodeQuaternionData(receivedBytes);

                    lock (SensorsMap)
                    {
                        if (!SensorsMap.ContainsKey(nr))
                        {
                            SensorsMap[nr] = new StreamSensor
                            {
                                Id = (byte)nr,
                            };
                        }

                        SensorsMap[nr].IpAddress = clientEndpoint.Address;
                        SensorsMap[nr].Quaternion = quat;

                        SensorRecvMs[nr] = recvMs;   // MESS
                    }
                    continue;
                }

                // -------- Glove Daten --------
                if (StreamReceiveMessageTypes.Matches(StreamReceiveMessageType.Glove, receivedList))
                {
                    var gloveData = StreamReceiveMessageTypes.DecodeGloveData(receivedBytes);

                    lock (GloveMap)
                    {
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

                        GloveRecvMs[gloveData.Id] = recvMs;   // MESS
                    }
                    continue;
                }

                Debug.LogWarning($"Unknown UDP packet received on port {localPort}. Length: {receivedBytes.Length}");
            }
        }
        catch (ThreadInterruptedException) { }
        catch (SocketException) { }
        catch (Exception e)
        {
            Debug.LogError($"UDPServer Error auf Port {localPort}: {e.Message}");
        }
    }

    private void OnApplicationQuit()
    {
        CleanUp();
    }

    public void SendVibrateOne(VibrationRequest vibrationRequest)
    {
        if (!SensorsMap.TryGetValue(vibrationRequest.SensorId, out var sensor))
            return;

        var data = StreamSendMessages.CreateVibrateOneRequest(vibrationRequest);


        int targetPort =  9001;


        if (_udpClients.Count > 0)
        {
            _udpClients[0].Send(data, data.Length, new IPEndPoint(sensor.IpAddress, targetPort));
        }
    }

    public bool TryGetSensor(int sensorId, out StreamSensor sensor)
    {
        lock (SensorsMap)
        {
            return SensorsMap.TryGetValue(sensorId, out sensor);
        }
    }

    public bool TryGetGlove(int gloveId, out GloveSensorData glove)
    {
        lock (GloveMap)
        {
            return GloveMap.TryGetValue(gloveId, out glove);
        }
    }

    // MESS: Empfangszeit auslesen (für UnityInternalLatencyLogger)
    public bool TryGetSensorRecvMs(int sensorId, out double ms)
    {
        lock (SensorsMap)
        {
            return SensorRecvMs.TryGetValue(sensorId, out ms);
        }
    }

    public bool TryGetGloveRecvMs(int gloveId, out double ms)
    {
        lock (GloveMap)
        {
            return GloveRecvMs.TryGetValue(gloveId, out ms);
        }
    }
}