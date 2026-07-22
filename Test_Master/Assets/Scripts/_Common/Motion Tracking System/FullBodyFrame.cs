using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One recorded full-body sample: a timestamp and a rotation per sensor id.
/// </summary>

[Serializable]
public class FullBodyFrame
{
    public float Time;
    public Dictionary<int, Quaternion> Rotations = new();
}