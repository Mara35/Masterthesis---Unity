using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class BlockItem : MonoBehaviour
{
    private Rigidbody rb;
    private Collider col;

    private bool isHeld = false;
    private bool counted = false;
    private bool inTargetZone = false;

    public bool CanBeGrabbed => !isHeld && !counted;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    public void Grab(Transform holdPoint)
    {
        isHeld = true;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;

        // Wichtig: Collider aus, damit keine Kollision mit der Hand entsteht
        col.enabled = false;

        transform.SetParent(holdPoint);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public bool Release()
    {
        transform.SetParent(null);

        isHeld = false;

        rb.isKinematic = false;
        rb.useGravity = true;

        // Collider wieder aktivieren
        col.enabled = true;

        return inTargetZone && !counted;
    }

    public void MarkCounted()
    {
        counted = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("TargetZone"))
        {
            inTargetZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("TargetZone"))
        {
            inTargetZone = false;
        }
    }
}