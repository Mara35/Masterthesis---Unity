using UnityEngine;

public class ProgressBarUpright : MonoBehaviour
{
    [Tooltip("Höhe über dem Eltern-Würfel (World Space)")]
    public float heightOffset = 0.08f;

    private Transform parentCube;

    private void Start()
    {
        parentCube = transform.parent;
    }

    private void LateUpdate()
    {
        if (parentCube == null) return;

        // Position: immer direkt über dem Würfel in World-Y
        transform.position = parentCube.position + Vector3.up * heightOffset;

        // Rotation: immer flach (keine Rotation des Eltern-Würfels übernehmen)
        transform.rotation = Quaternion.identity;
    }
}