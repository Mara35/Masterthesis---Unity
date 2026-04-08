using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FullBodyFrame
{
    public float Time;
    public Dictionary<int, Quaternion> Rotations = new();
}