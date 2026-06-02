using UnityEngine;

public class ProgressBarUpright : MonoBehaviour
{
    [Tooltip("Height above the parent cube (world space)")]
    public float heightOffset = 0.08f;

    private Transform parentCube;

    private void Start()
    {
        parentCube = transform.parent;
    }

    private void LateUpdate()
    {
        if (parentCube == null) return;

        // Position: always directly above the cube in World-Y
        transform.position = parentCube.position + Vector3.up * heightOffset;

        // Rotation: always flat (do not inherit rotation from the parent cube)
        transform.rotation = Quaternion.identity;
    }
}