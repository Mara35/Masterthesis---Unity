using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class BlockItem : MonoBehaviour
{
    private Rigidbody rb;
    private Collider col;

    private bool isHeld = false;

    private bool startedOnRightSide;
    private bool crossedPartitionWhileHeld = false;

    public Transform partition;

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

        startedOnRightSide = transform.position.x > partition.position.x;
        crossedPartitionWhileHeld = false;
    }

    private void Update()
    {
        if (!isHeld) return;

        if (startedOnRightSide && transform.position.x < partition.position.x)
        {
            crossedPartitionWhileHeld = true;
        }
    }

    public bool Release()
    {
        transform.SetParent(null);

        isHeld = false;

        rb.isKinematic = false;
        rb.useGravity = true;
        col.enabled = true;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        bool nowOnLeftSide = transform.position.x < partition.position.x;

        
        bool valid = startedOnRightSide && crossedPartitionWhileHeld && nowOnLeftSide;

        return valid;
    }
}