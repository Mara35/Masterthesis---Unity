using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class BlockItem : MonoBehaviour
{
    private Rigidbody rb;
    private BoxCollider col;

    private bool isHeld = false;
    private bool startedOnRightSide;
    private bool crossedPartitionWhileHeld = false;
    private bool passedThroughPartitionZone = false;

    public bool IsValidlyTransferred { get; private set; } = false;

    public Transform partition;

    private Vector3 grabPosition;
    private Quaternion grabRotation;

    public bool CanBeGrabbed => !isHeld;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<BoxCollider>();
    }

    public void Grab(Transform holdPoint)
    {
        isHeld = true;
        grabPosition = transform.position;
        grabRotation = transform.rotation;

        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;
        col.isTrigger = true;

        transform.SetParent(holdPoint);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        startedOnRightSide = transform.position.x > partition.position.x;
        crossedPartitionWhileHeld = false;
        passedThroughPartitionZone = false;
        IsValidlyTransferred = false;
    }

    public void OnPassedThroughPartitionZone()
    {
        if (isHeld)
        {
            passedThroughPartitionZone = true;
            Debug.Log("[BlockItem] PartitionZone passiert");
        }
    }

    private void Update()
    {
        if (!isHeld) return;

        if (startedOnRightSide && transform.position.x < partition.position.x)
            crossedPartitionWhileHeld = true;
    }

    public bool Release()
    {
        transform.SetParent(null);

        isHeld = false;
        col.isTrigger = false;
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        bool nowOnLeftSide = transform.position.x < partition.position.x;

        bool valid = startedOnRightSide
                  && crossedPartitionWhileHeld
                  && passedThroughPartitionZone
                  && nowOnLeftSide;

        IsValidlyTransferred = valid;

        if (!valid)
        {
            transform.position = grabPosition;
            transform.rotation = grabRotation;
            Debug.Log($"[BlockItem] Ungueltig - Reset.");
        }
        else
        {
            Debug.Log("[BlockItem] GUELTIGER Transfer");
        }

        return valid;
    }
}