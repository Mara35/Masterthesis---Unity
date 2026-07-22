using UnityEngine;

/// <summary>
/// A grabbable block for the Box and Block Test. Tracks whether it was carried across the center
/// partition in a valid way: it must start on the right, cross the partition line while held, pass
/// through the PartitionZone trigger, and end on the left. An invalid release snaps it back to its
/// grab pose and flashes it red. Blocks without a partition (e.g. peg cubes) skip this logic.
/// </summary>

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class BlockItem : MonoBehaviour
{
    private Rigidbody rb;
    private BoxCollider col;
    private Renderer blockRenderer;

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
        blockRenderer = GetComponent<Renderer>();
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

        if (partition != null)
            startedOnRightSide = transform.position.x > partition.position.x;
        else
            startedOnRightSide = false;

        crossedPartitionWhileHeld = false;
        passedThroughPartitionZone = false;
        IsValidlyTransferred = false;
    }

    public void OnPassedThroughPartitionZone()
    {
        if (isHeld)
        {
            passedThroughPartitionZone = true;
            Debug.Log("[BlockItem] PartitionZone crossed");
        }
    }

    private void Update()
    {
        if (!isHeld) return;

        if (partition != null && startedOnRightSide && transform.position.x < partition.position.x)
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

        bool valid = false;

        if (partition != null)
        {
            bool nowOnLeftSide = transform.position.x < partition.position.x;
            // Valid only if the full sequence happened: started right, crossed while held,
            // went through the zone trigger, and ended left.
            valid = startedOnRightSide
                 && crossedPartitionWhileHeld
                 && passedThroughPartitionZone
                 && nowOnLeftSide;
        }
        // partition == null: PegCube or other objects without transfer logic
        // valid remains false; evaluation is handled by a separate system (PegChallengeZone, etc.)

        IsValidlyTransferred = valid;

        if (!valid && partition != null)
        {
            transform.position = grabPosition;
            transform.rotation = grabRotation;
            Debug.Log($"[BlockItem] Invalid - Reset.");
            if (blockRenderer != null)
                StartCoroutine(FlashInvalid());
        }
        else if (valid)
        {
            Debug.Log("[BlockItem] valid Transfer");
        }

        return valid;
    }
    private System.Collections.IEnumerator FlashInvalid()  
    {
        Color original = blockRenderer.material.color;
        blockRenderer.material.color = Color.red;
        yield return new WaitForSeconds(0.3f);
        blockRenderer.material.color = original;
    }
}