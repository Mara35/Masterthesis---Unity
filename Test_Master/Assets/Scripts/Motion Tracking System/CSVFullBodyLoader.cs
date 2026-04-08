using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class CsvFullBodyLoader
{
    public static List<FullBodyFrame> LoadFromText(string csvText, float assumedDtSeconds = 0.02f)
    {
        var frames = new List<FullBodyFrame>();

        if (string.IsNullOrWhiteSpace(csvText))
            return frames;

        string[] lines = csvText.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            return frames;

        // Wir überspringen die Header-Zeile
        for (int row = 1; row < lines.Length; row++)
        {
            string line = lines[row].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            string[] parts = line.Split(',');

            // Erwartetes Format:
            // Spalte 0 = Index
            // Spalte 1 = Platzhalter / NaN
            // danach 10 Sensoren * 4 Werte = 40 Spalten
            // insgesamt mindestens 42 Spalten
            if (parts.Length < 42)
                continue;

            var frame = new FullBodyFrame
            {
                Time = (row - 1) * assumedDtSeconds
            };

            for (int sensorId = 1; sensorId <= 10; sensorId++)
            {
                int baseCol = 2 + (sensorId - 1) * 4;

                float iVal = ParseFloat(parts[baseCol + 0]);
                float kVal = ParseFloat(parts[baseCol + 1]);
                float jVal = ParseFloat(parts[baseCol + 2]);
                float realVal = ParseFloat(parts[baseCol + 3]);

                // Datenschema aus deinem IMU-System:
                // Unity x = i
                // Unity y = -k
                // Unity z = j
                // Unity w = real
                Quaternion q = new Quaternion(iVal, -kVal, jVal, realVal);

                frame.Rotations[sensorId] = q;
            }

            frames.Add(frame);
        }

        return frames;
    }

    private static float ParseFloat(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return 0f;

        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            return value;

        return 0f;
    }
}