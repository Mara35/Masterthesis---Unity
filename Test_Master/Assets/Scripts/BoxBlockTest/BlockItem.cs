using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class BlockItem : MonoBehaviour
{
    private Rigidbody rb;
    private Collider col;

    private bool isHeld = false;

    public bool CanBeGrabbed => !isHeld;

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

        col.enabled = false;

        transform.SetParent(holdPoint);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void Release()
    {
        transform.SetParent(null);

        isHeld = false;

        rb.isKinematic = false;
        rb.useGravity = true;
        col.enabled = true;
    }
}