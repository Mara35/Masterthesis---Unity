using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Enum for storing all the types of stream messages
/// </summary>
public enum StreamReceiveMessageType
{
    Quaternion
}

/// <summary>
/// Static class for handling messages received from the stream sensors.
/// Provides information about the message ids, message lengths, and methods to check which type a message has and then decode it to its data.
/// </summary>
public static class StreamReceiveMessageTypes
{
    public static readonly Dictionary<StreamReceiveMessageType, byte[]> StartSequences = new()
    {
        {StreamReceiveMessageType.Quaternion, new byte[]{0xFF, 0x00, 0xFF}},
        
    };


    public static readonly Dictionary<StreamReceiveMessageType, int> Lengths = new()
    {
        {StreamReceiveMessageType.Quaternion, 20},
    };


    public static bool Matches(StreamReceiveMessageType type, List<byte> data)
    {
        var startSequence = StartSequences[type];
        if (data.Count < startSequence.Length) return false;
        return !startSequence.Where((t, i) => data[i] != t).Any();
    }


    private static float[] ConvertBytesToFloats(int startIndex, int num, byte[] data)
    {
        var ret = new float[num];
        for (var i = 0; i < num; i++)
        {
            ret[i] = BitConverter.ToSingle(data, startIndex);
            startIndex += 4;
        }

        return ret;
    }


    public static (int, Quaternion) DecodeQuaternionData(byte[] data) {
        var fquat = ConvertBytesToFloats(4, 4, data);
        return (data[3], new Quaternion(fquat[0], fquat[1], fquat[2], fquat[3]));
    }
}
