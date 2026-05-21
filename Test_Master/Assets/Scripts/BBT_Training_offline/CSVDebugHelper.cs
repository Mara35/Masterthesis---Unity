using UnityEngine;

/// <summary>
/// Temporäres Debug-Script: auf CSVReplayController GO legen.
/// Zeigt in der Console ob Bones sich wirklich ändern.
/// Nach dem Debuggen löschen.
/// </summary>
public class CSVDebugHelper : MonoBehaviour
{
    public Transform upperArmBone;
    public Transform indexMCPBone;

    private Quaternion lastUpperArm;
    private Quaternion lastIndex;
    private int frameCount = 0;

    void Start()
    {
        if (upperArmBone) lastUpperArm = upperArmBone.localRotation;
        if (indexMCPBone) lastIndex = indexMCPBone.localRotation;
        Debug.Log("[DEBUG] Start-Rotation UpperArm: " + (upperArmBone ? upperArmBone.localRotation.eulerAngles.ToString() : "NULL"));
        Debug.Log("[DEBUG] Start-Rotation Index MCP: " + (indexMCPBone ? indexMCPBone.localRotation.eulerAngles.ToString() : "NULL"));
    }

    void Update()
    {
        frameCount++;
        // Alle 60 Frames loggen
        if (frameCount % 60 != 0) return;

        if (upperArmBone)
        {
            float diff = Quaternion.Angle(lastUpperArm, upperArmBone.localRotation);
            Debug.Log($"[DEBUG] UpperArm Rotation: {upperArmBone.localRotation.eulerAngles} | ?={diff:F2}°");
            lastUpperArm = upperArmBone.localRotation;
        }

        if (indexMCPBone)
        {
            float diff = Quaternion.Angle(lastIndex, indexMCPBone.localRotation);
            Debug.Log($"[DEBUG] Index MCP Rotation: {indexMCPBone.localRotation.eulerAngles} | ?={diff:F2}°");
            lastIndex = indexMCPBone.localRotation;
        }
    }
}