using UnityEngine;
using System;

/// <summary>
/// Move the HandProxy in a natural arcing motion across the center partition.
/// Pick up all 5 cubes one after the other and place them on the other side of the box.
/// </summary>
public class AutoHandMover : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.2f;
    [SerializeField] private float snapDistance = 0.03f;

    [Header("Partition & Arch Height")]
    [Tooltip("Reference to the CenterPartition GameObject." +
             "If left blank, the system will automatically search for 'CenterPartition' in the scene.")]
    [SerializeField] private Transform centerPartition = null;

    [Tooltip("The distance between the crest and the top edge of the partition (safety margin).")]
    [SerializeField] private float partitionClearance = 0.15f;

    [Tooltip("Minimum height of the crest above the higher of the two end positions.")]
    [SerializeField] private float minArcHeight = 0.25f;

    [Header("Shape of curve")]
    [Tooltip("Checkpoint A: how far the ascent is offset to the side of the peak (0 = perpendicular).")]
    [Range(0f, 1f)]
    [SerializeField] private float cp1SideBias = 0.1f;

    [Tooltip("Checkpoint B: how far the descent is offset to the side of the target (0 = perpendicular).")]
    [Range(0f, 1f)]
    [SerializeField] private float cp2SideBias = 0.3f;

    [Header("Pauses (seconds)")]
    [SerializeField] private float pauseAfterGrab = 0.3f;
    [SerializeField] private float pauseAfterDrop = 0.4f;

    [Header("Cube Offset (World Coordinates)")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0f, 0.05f, 0f);

    // -----------------------------------------------------------------------
    // IK Target + Hand Grip
    // -----------------------------------------------------------------------

    [Header("IK Target (Animation Rigging)")]
    [Tooltip("assign HandIKTarget")]
    [SerializeField] private Transform ikTarget;

    [Header("Hand Grasping")]
    [Tooltip("The GameObject holding HandGrip.cs")]
    [SerializeField] private HandGrip handGrip;

    [Header("IK Offset Correction")]
    [Tooltip("mixamorig: Assign RightHand. Measures the offset between the ghost hand and the real hand in real time.")]
    [SerializeField] private Transform realHandBone;

    [Tooltip("Minimum hand height. Prevents the forearm from passing through the front panel.")]
    [SerializeField] private float minHandHeight = 0.5f;

    [Tooltip("Additional Z-offset: Move your hand further along the +Z axis, away from the avatar.")]
    [SerializeField] private float handZOffset = 0.1f;

    // -----------------------------------------------------------------------
    // State Machine
    // -----------------------------------------------------------------------

    private enum State
    {
        Idle,
        MovingToBlock,
        Grabbing,
        ArcCarry,
        Dropping,
        ArcReturn
    }

    private State state = State.Idle;

    // Held object
    private Transform heldBlock = null;
    private Rigidbody heldRb = null;
    private Collider heldCol = null;
    private Collider handCol = null;

    // Positions
    private Vector3 blockGoal;
    private Vector3 dropGoal;
    private Vector3 homePos;

    // Two-segment arch
    private Vector3 arcStart, arcEnd, peakPos, cpA, cpB;
    private float arcT = 0f;
    private float segALen = 1f;
    private float segBLen = 1f;
    private float totalLen = 1f;
    private float tSplit = 0.5f;

    // Misc
    private float pauseTimer;
    private Action onDone;

    public bool IsIdle => state == State.Idle;
    public bool IsCarrying => state == State.ArcCarry;

    private bool passedPartitionZone = false;
    private Vector3 blockStartPosition;
    private Quaternion blockStartRotation;
    public void NotifyPartitionPassed() { passedPartitionZone = true; }

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        homePos = transform.position;
        handCol = GetComponent<Collider>();

        if (centerPartition == null)
        {
            var go = GameObject.Find("CenterPartition");
            if (go != null)
            {
                centerPartition = go.transform;
                Debug.Log("[AutoHandMover] CenterPartition found automatically: " + go.name);
            }
            else
            {
                Debug.LogWarning("[AutoHandMover] CenterPartition is not set and was not found!");
            }
        }

        // auto-find ikTarget if not assigned
        if (ikTarget == null)
        {
            var go = GameObject.Find("HandIKTarget");
            if (go != null)
            {
                ikTarget = go.transform;
                Debug.Log("[AutoHandMover] HandIKTarget found automatically.");
            }
        }

        var kb = GetComponent<HandProxyKeyboardControl>();
        if (kb != null) kb.enabled = false;

        var grb = GetComponent<SimpleGrabber>();
        if (grb != null) grb.enabled = false;
    }

    private void Update()
    {
        switch (state)
        {
            case State.MovingToBlock:
                MoveTo(blockGoal);
                SyncIKTarget();                         
                if (Arrived(blockGoal))
                {
                    if (handGrip != null) handGrip.SetWristBend(true); // Wrist bending
                    pauseTimer = pauseAfterGrab;
                    state = State.Grabbing;
                }
                break;

            case State.Grabbing:
                pauseTimer -= Time.deltaTime;

                // Faust folds in the last 40% of the break (his hand is already on the dice)
                if (pauseTimer <= pauseAfterGrab * 0.4f)
                    if (handGrip != null) handGrip.Grip();

                if (pauseTimer <= 0f)
                {
                    GrabBlock();
                    if (handGrip != null) handGrip.SetWristBend(false); // Wrist extension
                    if (handGrip != null) handGrip.SetElbowBend(true);  // Wrist bending for return trip
                    BuildArc(transform.position, dropGoal);
                    state = State.ArcCarry;
                }
                break;

            case State.ArcCarry:
                AdvanceArc();
                SyncIKTarget();                        
                CarryBlock();
                if (arcT >= 1f)
                {
                    transform.position = arcEnd;
                    SyncIKTarget();                     
                    CarryBlock();
                    pauseTimer = pauseAfterDrop;
                    state = State.Dropping;
                }
                break;

            case State.Dropping:
                pauseTimer -= Time.deltaTime;
                if (pauseTimer <= pauseAfterDrop * 0.5f)
                    if (handGrip != null) handGrip.SetWristBend(true); // Wrist bending
                if (pauseTimer <= 0f)
                {
                    DropBlock();
                    if (handGrip != null) handGrip.SetWristBend(false); // Wrist extension
                    if (handGrip != null) handGrip.SetElbowBend(false); // Wrist bending for return trip
                    BuildArc(transform.position, homePos);
                    state = State.ArcReturn;
                }
                break;

            case State.ArcReturn:
                AdvanceArc();
                SyncIKTarget();                         
                if (arcT >= 1f)
                {
                    transform.position = arcEnd;
                    SyncIKTarget();                     
                    state = State.Idle;
                    onDone?.Invoke();
                    onDone = null;
                }
                break;

            case State.Idle:
            default:
                break;
        }
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void RunSequence(Transform block, Vector3 dropPosition, Action callback)
    {
        if (!IsIdle)
        {
            Debug.LogWarning("[AutoHandMover] Still busy.");
            return;
        }

        heldBlock = block;
        heldRb = block.GetComponent<Rigidbody>();
        heldCol = block.GetComponent<Collider>();
        blockGoal = block.position;
        dropGoal = dropPosition;
        onDone = callback;

        if (handCol != null) handCol.isTrigger = true;

        state = State.MovingToBlock;
    }

    // Builds the carry arc as two quadratic Bezier segments (start->peak, peak->end). The peak is
    // placed above the partition line with clearance, and the segments are length-measured so the
    // hand moves at constant speed across the whole arc (tSplit = where segment A ends in [0,1]).
    private void BuildArc(Vector3 start, Vector3 end)
    {
        arcStart = start;
        arcEnd = end;
        arcT = 0f;

        if (centerPartition != null)
        {
            Vector3 partPos = centerPartition.position;
            float spanX = Mathf.Abs(end.x - start.x);
            float spanZ = Mathf.Abs(end.z - start.z);

            float peakX, peakZ;
            if (spanZ >= spanX)
            {
                peakZ = partPos.z;
                peakX = Mathf.Lerp(start.x, end.x, 0.5f);
            }
            else
            {
                peakX = partPos.x;
                peakZ = Mathf.Lerp(start.z, end.z, 0.5f);
            }

            float peakYMin = Mathf.Max(start.y, end.y) + minArcHeight;
            float peakYPartition = partPos.y + partitionClearance;
            float peakY = Mathf.Max(peakYMin, peakYPartition);

            peakPos = new Vector3(peakX, peakY, peakZ);
        }
        else
        {
            peakPos = Vector3.Lerp(start, end, 0.5f);
            peakPos.y = Mathf.Max(start.y, end.y) + minArcHeight;
        }

        cpA = new Vector3(
            Mathf.Lerp(start.x, peakPos.x, cp1SideBias),
            peakPos.y,
            Mathf.Lerp(start.z, peakPos.z, cp1SideBias)
        );

        cpB = new Vector3(
            Mathf.Lerp(peakPos.x, end.x, cp2SideBias),
            peakPos.y,
            Mathf.Lerp(peakPos.z, end.z, cp2SideBias)
        );

        const int segs = 20;
        segALen = 0f;
        Vector3 prev = arcStart;
        for (int i = 1; i <= segs; i++)
        {
            float lt = i / (float)segs;
            Vector3 p = QuadBezier(arcStart, cpA, peakPos, lt);
            segALen += Vector3.Distance(prev, p);
            prev = p;
        }

        segBLen = 0f;
        prev = peakPos;
        for (int i = 1; i <= segs; i++)
        {
            float lt = i / (float)segs;
            Vector3 p = QuadBezier(peakPos, cpB, arcEnd, lt);
            segBLen += Vector3.Distance(prev, p);
            prev = p;
        }

        segALen = Mathf.Max(segALen, 0.001f);
        segBLen = Mathf.Max(segBLen, 0.001f);
        totalLen = segALen + segBLen;
        tSplit = segALen / totalLen;
    }

    private void AdvanceArc()
    {
        float dtT = (moveSpeed * Time.deltaTime) / totalLen;
        arcT = Mathf.Clamp01(arcT + dtT);

        Vector3 pos;
        if (arcT <= tSplit)
        {
            float lt = (tSplit > 0f) ? arcT / tSplit : 1f;
            pos = QuadBezier(arcStart, cpA, peakPos, lt);
        }
        else
        {
            float lt = (1f - tSplit > 0f) ? (arcT - tSplit) / (1f - tSplit) : 1f;
            pos = QuadBezier(peakPos, cpB, arcEnd, lt);
        }

        transform.position = pos;
    }

    private static Vector3 QuadBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    // -----------------------------------------------------------------------
    // Grab / Carry / Drop
    // -----------------------------------------------------------------------

    private void GrabBlock()
    {
        if (heldBlock == null) return;
        blockStartPosition = heldBlock.position;
        blockStartRotation = heldBlock.rotation;
        passedPartitionZone = false;
        if (heldRb != null) { heldRb.isKinematic = true; heldRb.useGravity = false; }
        if (heldCol != null) heldCol.enabled = false;
    }

    private void CarryBlock()
    {
        if (heldBlock != null)
            heldBlock.position = transform.position + holdOffset;
    }

    private void DropBlock()
    {
        if (heldBlock == null) return;
        float partX = centerPartition != null ? centerPartition.position.x : 0f;
        bool nowOnLeftSide = dropGoal.x < partX;
        bool valid = passedPartitionZone && nowOnLeftSide;
        if (valid)
        {
            heldBlock.position = dropGoal;
            Debug.Log("[AutoHandMover] Valid transfer:" + heldBlock.name);
        }
        else
        {
            heldBlock.position = blockStartPosition;
            heldBlock.rotation = blockStartRotation;
            Debug.Log("[AutoHandMover] Invalid Transfer: " + heldBlock.name + " Reset. partition=" + passedPartitionZone + " leftSide=" + nowOnLeftSide);
        }
        if (heldRb != null) { heldRb.isKinematic = false; heldRb.useGravity = true; }
        if (heldCol != null) { heldCol.enabled = true; heldCol.isTrigger = false; }
        heldBlock = null; heldRb = null; heldCol = null;
        if (handCol != null) handCol.isTrigger = false;
        if (handGrip != null) handGrip.Release();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void MoveTo(Vector3 goal) =>
        transform.position = Vector3.MoveTowards(
            transform.position, goal, moveSpeed * Time.deltaTime);

    private bool Arrived(Vector3 goal) =>
        Vector3.Distance(transform.position, goal) <= snapDistance;


    // Synchronizes HandIKTarget with the current HandProxy position.
    // The Two Bone IK Constraint on RightArmIK handles the rest automatically.

    private void SyncIKTarget()
    {
        if (ikTarget == null) return;

        Vector3 pos = transform.position;

        // Minimum height: The forearm comes from above rather than through the front wall
        pos.y = Mathf.Max(pos.y, minHandHeight);

        // Z-offset: Move the hand further away from the avatar toward the table
        pos.z += handZOffset;

        if (realHandBone != null)
        {
            // Compensate for live offset between the ghost and the real hand (X and Z only)
            Vector3 currentOffset = transform.position - realHandBone.position;
            ikTarget.position = pos + new Vector3(currentOffset.x, 0f, currentOffset.z);
        }
        else
        {
            ikTarget.position = pos;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.cyan;
        Vector3 prev = arcStart;
        for (int i = 1; i <= 20; i++)
        {
            float t = i / 20f;
            Vector3 p = QuadBezier(arcStart, cpA, peakPos, t);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }

        Gizmos.color = Color.green;
        prev = peakPos;
        for (int i = 1; i <= 20; i++)
        {
            float t = i / 20f;
            Vector3 p = QuadBezier(peakPos, cpB, arcEnd, t);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(cpA, 0.025f);
        Gizmos.DrawWireSphere(cpB, 0.025f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(peakPos, 0.035f);
    }
#endif
}