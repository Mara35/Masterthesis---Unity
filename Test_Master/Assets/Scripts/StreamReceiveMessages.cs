using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Enum for storing all the types of stream messages
/// </summary>
public enum StreamReceiveMessageType
{
    Quaternion,
    Glove
}

/// <summary>
/// Static class for handling messages received from the stream sensors.
/// Provides information about the message ids, message lengths, and methods to check which type a message has and then decode it to its data.
/// </summary>

public static class StreamReceiveMessageTypes
{
    // -----------------------------
    // MATCHING
    // -----------------------------
    public static bool Matches(StreamReceiveMessageType type, List<byte> data)
    {
        if (data == null || data.Count < 4)
            return false;

        switch (type)
        {
            case StreamReceiveMessageType.Quaternion:
                return data[0] == 0xFF && data[1] == 0x00 && data[2] == 0xFF;

            case StreamReceiveMessageType.Glove:
                return data[0] == 0xFF && data[1] == 0x01 && data[2] == 0xFF;

            default:
                return false;
        }
    }

    // -----------------------------
    // QUATERNION (dein bestehender Code)
    // -----------------------------
    public static (int, Quaternion) DecodeQuaternionData(byte[] data)
    {
        int id = data[3];

        float x = BitConverter.ToSingle(data, 4);
        float y = BitConverter.ToSingle(data, 8);
        float z = BitConverter.ToSingle(data, 12);
        float w = BitConverter.ToSingle(data, 16);

        return (id, new Quaternion(x, y, z, w));
    }

    // -----------------------------
    // GLOVE DECODING
    // -----------------------------
    public static GloveSensorData DecodeGloveData(byte[] data)
    {
        if (data.Length < 44)
        {
            Debug.LogError("Glove packet too small!");
            return null;
        }

        GloveSensorData glove = new GloveSensorData();

        glove.Id = data[3];

        // Achtung: Reihenfolge MUSS exakt zu ESP passen
        glove.Thumb_MCP = BitConverter.ToSingle(data, 4);
        glove.Thumb_PIP = BitConverter.ToSingle(data, 8);

        glove.Index_MCP = BitConverter.ToSingle(data, 12);
        glove.Index_PIP = BitConverter.ToSingle(data, 16);

        glove.Middle_MCP = BitConverter.ToSingle(data, 20);
        glove.Middle_PIP = BitConverter.ToSingle(data, 24);

        glove.Ring_MCP = BitConverter.ToSingle(data, 28);
        glove.Ring_PIP = BitConverter.ToSingle(data, 32);

        glove.Pinky_MCP = BitConverter.ToSingle(data, 36);
        glove.Pinky_PIP = BitConverter.ToSingle(data, 40);

        return glove;
    }
}