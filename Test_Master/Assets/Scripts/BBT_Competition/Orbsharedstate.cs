using System.Collections.Generic;
using UnityEngine;

public static class OrbSharedState
{
    // Würfel die gerade getragen werden – für keinen anderen Orb greifbar
    public static HashSet<int> lockedCubes = new HashSet<int>();

    // Würfel die kürzlich abgelegt wurden – Cooldown in Sekunden
    public static Dictionary<int, float> recentlyDropped = new Dictionary<int, float>();

    public static float dropCooldown = 1.5f;

    public static void Lock(int instanceId)
    {
        lockedCubes.Add(instanceId);
        recentlyDropped.Remove(instanceId);
    }

    public static void Unlock(int instanceId)
    {
        lockedCubes.Remove(instanceId);
        recentlyDropped[instanceId] = Time.time + dropCooldown;
    }

    public static bool IsAvailable(int instanceId)
    {
        if (lockedCubes.Contains(instanceId)) return false;
        if (recentlyDropped.TryGetValue(instanceId, out float cooldownEnd) && Time.time < cooldownEnd) return false;
        return true;
    }

    public static bool IsAvailableIgnoreCooldown(int instanceId)
    {
        return !lockedCubes.Contains(instanceId);
    }

    public static void Reset()
    {
        lockedCubes.Clear();
        recentlyDropped.Clear();
    }
}