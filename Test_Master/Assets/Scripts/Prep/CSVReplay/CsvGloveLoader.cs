using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// LEGACY. Parses a glove recording CSV into GloveFrames (timestamp + finger angles) for offline
/// replay. Uses InvariantCulture for locale-independent decimals.
/// </summary>
public static class CsvGloveLoader
{
    public static List<GloveFrame> LoadFromText(string csvText)
    {
        var frames = new List<GloveFrame>();
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
            if (parts.Length < 16)
                continue;

            var frame = new GloveFrame();
            frame.Time = float.Parse(parts[0], CultureInfo.InvariantCulture) / 1000f;

            for (int a = 0; a < 15; a++)
            {
                frame.Angles[a] = float.Parse(parts[a + 1], CultureInfo.InvariantCulture);
            }

            frames.Add(frame);
        }

        return frames;
    }
}