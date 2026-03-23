using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class CsvArmLoader
{
    public static List<ArmFrame> LoadFromText(string csvText)
    {
        var frames = new List<ArmFrame>();
        if (string.IsNullOrWhiteSpace(csvText))
            return frames;

        string[] lines = csvText.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            return frames;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] parts = line.Split(',');
            if (parts.Length < 6)
                continue;

            float time = float.Parse(parts[0], CultureInfo.InvariantCulture) / 1000f;
            int sensorId = int.Parse(parts[1], CultureInfo.InvariantCulture);
            float qx = float.Parse(parts[2], CultureInfo.InvariantCulture);
            float qy = float.Parse(parts[3], CultureInfo.InvariantCulture);
            float qz = float.Parse(parts[4], CultureInfo.InvariantCulture);
            float qw = float.Parse(parts[5], CultureInfo.InvariantCulture);

            frames.Add(new ArmFrame
            {
                Time = time,
                SensorId = sensorId,
                Rotation = new Quaternion(qx, qy, qz, qw)
            });
        }

        return frames;
    }
}