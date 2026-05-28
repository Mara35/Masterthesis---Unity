using UnityEngine;

/// <summary>
/// Moves the HandTarget GameObject using the keyboard.
/// Replaces HandProxyKeyboardControl – same keys, same logic.
/// The HandTarget is the IK target for the XBot arm (Two Bone IK Constraint).
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HandTargetKeyboardControl : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 0.5f;
    public float fastMultiplier = 2f;

    [Header("Clamping (optional)")]
    public bool useBounds = false;
    public Vector3 boundsMin = new Vector3(-1f, 0.5f, -1f);
    public Vector3 boundsMax = new Vector3(1f, 2f, 1f);

    public bool HasMovedThisFrame { get; private set; }

    private Rigidbody rb;
    private Vector3 movement;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
                
        rb.useGravity = false;
        rb.isKinematic = true; 
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    private void Update()
    {
        HasMovedThisFrame = false;

        float x = 0f;
        float y = 0f;
        float z = 0f;

        // Same key mapping as HandProxyKeyboardControl
        if (Input.GetKey(KeyCode.A)) x = -1f;
        if (Input.GetKey(KeyCode.D)) x = 1f;

        if (Input.GetKey(KeyCode.F)) y = -1f;
        if (Input.GetKey(KeyCode.R)) y = 1f;

        if (Input.GetKey(KeyCode.S)) z = -1f;
        if (Input.GetKey(KeyCode.W)) z = 1f;

        movement = new Vector3(x, y, z).normalized;

        if (movement.magnitude > 0.001f)
            HasMovedThisFrame = true;
    }

    private void FixedUpdate()
    {
        float speed = moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            speed *= fastMultiplier;

        Vector3 newPosition = rb.position + movement * speed * Time.fixedDeltaTime;

        if (useBounds)
            newPosition = Vector3.Min(Vector3.Max(newPosition, boundsMin), boundsMax);

        rb.MovePosition(newPosition);
    }
}