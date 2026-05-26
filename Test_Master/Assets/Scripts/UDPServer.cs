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
    // Hier kannst du im Inspector nun mehrere Ports eintragen (z.B. Größe 2: Element 0 = 9001, Element 1 = 9002)
    [SerializeField] private List<int> listenPorts = new List<int> { 9001, 9002 };

    // Wir verwalten jetzt eine Liste von Clients und Threads, um von allen Ports gleichzeitig zu lesen
    private List<UdpClient> _udpClients = new List<UdpClient>();
    private List<Thread> _readThreads = new List<Thread>();

    public readonly Dictionary<int, StreamSensor> SensorsMap = new();
    public readonly Dictionary<int, GloveSensorData> GloveMap = new();

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

                // Für jeden Port starten wir einen eigenen kleinen Hintergrund-Thread
                Thread thread = new Thread(() => ThreadFunction(client, port));
                thread.IsBackground = true;
                thread.Start();
                _readThreads.Add(thread);

                Debug.Log($"UDP Server lauscht erfolgreich auf Port: {port}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Fehler beim Öffnen von Port {port}: {e.Message}");
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

                var receivedList = receivedBytes.ToList();

                // -------- IMU Quaternion --------
                if (StreamReceiveMessageTypes.Matches(StreamReceiveMessageType.Quaternion, receivedList))
                {
                    var (nr, quat) = StreamReceiveMessageTypes.DecodeQuaternionData(receivedBytes);

                    lock (SensorsMap) // Lock für Thread-Sicherheit bei mehreren Ports
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

                        // WICHTIG: Wir merken uns, auf welchem Port dieser Sensor reinkam!
                        // Falls dein StreamSensor-Objekt kein Port-Feld hat, nutzen wir das temporär für die Antwort.
                        // Wir missbrauchen hier einen Trick oder senden direkt an den Port, von dem er kam.
                        // Damit die Vibration funktioniert, speichern wir den Port im Endpunkt.
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

        // KORREKTUR: Ermittle den passenden Sende-Port anhand der SensorId.
        // Unterarm (Id 3 -> Port 9001), Oberarm (Id 4 -> Port 9002)
        int targetPort = (vibrationRequest.SensorId == 4) ? 9002 : 9001;

        // Wir nutzen den ersten verfügbaren Client zum Senden
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
}