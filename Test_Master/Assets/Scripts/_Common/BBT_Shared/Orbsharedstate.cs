/*
 * Shared state between GhostOrbController and PlayerOrbController.
 * Ensures that lockedCubes and recentlyDropped are truly shared by both
 * controllers—regardless of static class boundaries.
 */
using System.Collections.Generic;
using UnityEngine;

public static class OrbSharedState
{
    // Orbs currently being carried – cannot be picked up by any other orb
    public static HashSet<int> lockedCubes = new HashSet<int>();

    // Dice that were recently rolled – cooldown in seconds
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

    // Which page is currently frozen (prevents ReactionCube from spawning there)
    public static bool ghostFrozen = false;
    public static bool playerFrozen = false;

    // Active challenge flags per side
    // true = this page currently has an active challenge
    public static bool playerSideHasReaction = false;
    public static bool ghostSideHasReaction = false;
    public static bool playerSideHasPeg = false;

    public static void Reset()
    {
        lockedCubes.Clear();
        recentlyDropped.Clear();
    }
}