using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HandProxyKeyboardControl : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 0.5f;
    public float fastMultiplier = 2f;
    public bool HasMovedThisFrame { get; private set; }

    private Rigidbody rb;
    private Vector3 movement;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        HasMovedThisFrame = false;  
        float x = 0f;
        float y = 0f;
        float z = 0f;

        if (Input.GetKey(KeyCode.A)) x = -1f;
        if (Input.GetKey(KeyCode.D)) x = 1f;

        if (Input.GetKey(KeyCode.F)) y = -1f;
        if (Input.GetKey(KeyCode.R)) y = 1f;

        if (Input.GetKey(KeyCode.S)) z = -1f;
        if (Input.GetKey(KeyCode.W)) z = 1f;

        movement = new Vector3(x, y, z).normalized;

        if (movement.magnitude > 0.001f)
        {
            HasMovedThisFrame = true;
        }

    }
    
    private void FixedUpdate()
    {
        float speed = moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            speed *= fastMultiplier;
        }

        Vector3 newPosition = rb.position + movement * speed * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);
    }
}