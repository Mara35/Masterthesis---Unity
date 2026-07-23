using System;

/// <summary>
/// LEGACY. One recorded glove sample: a timestamp and the ten finger angles. Used only by the
/// CSV replay path.
/// </summary>

[Serializable]
public class GloveFrame
{
    public float Time;
    public float[] Angles = new float[15];
}