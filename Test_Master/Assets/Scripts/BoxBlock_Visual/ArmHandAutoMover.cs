using UnityEngine;
using System;

/// <summary>
/// Bewegt das HandIKTarget entlang der Bézier-Kurve.
/// Two Bone IK Constraint (Animation Rigging) übernimmt die gesamte
/// Arm-Rotation – kein manuelles Bone-Steering nötig.
///
/// Setup im Inspector:
///   handIKTarget    = HandIKTarget GameObject
///   handBone        = mixamorig:RightHand  (nur für Würfel-Position)
///   fingerMCPBones  = RightHandIndex1, Middle1, Ring1, Pinky1, Thumb1
///   homePosOverride = X 0.3 / Y 0.765 / Z -0.25  (von HomePosArm)
/// </summary>
public class ArmHandAutoMover : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("IK Target (wird bewegt)")]
    [Tooltip("Das HandIKTarget GameObject das im Two Bone IK Constraint als Target eingetragen ist")]
    [SerializeField] private Transform handIKTarget;

    [Header("Hand Bone (nur für Würfel-Mitführen)")]
    [Tooltip("mixamorig:RightHand")]
    [SerializeField] private Transform handBone;

    [Header("Finger Bones (je erster Joint)")]
    [Tooltip("RightHandIndex1, RightHandMiddle1, RightHandRing1, RightHandPinky1, RightHandThumb1")]
    [SerializeField] private Transform[] fingerMCPBones;

    [Header("Finger Greif-Winkel")]
    [SerializeField] private Vector3 fingerFlexAxis = new Vector3(1f, 0f, 0f);
    [SerializeField] private float fingerCloseAngle = 65f;
    [SerializeField] private float fingerSpeed = 3f;

    [Header("Bewegung")]
    [SerializeField] private float moveSpeed = 1.2f;
    [SerializeField] private float snapDistance = 0.03f;

    [Header("Ruheposition (Weltkoordinaten)")]
    [Tooltip("Von HomePosArm übernehmen: X 0.3 / Y 0.765 / Z -0.25")]
    [SerializeField] private Vector3 homePosOverride = new Vector3(0.3f, 0.765f, -0.25f);

    [Header("Partition & Bogenhöhe")]
    [SerializeField] private Transform centerPartition = null;
    [SerializeField] private float partitionClearance = 0.15f;
    [SerializeField] private float minArcHeight = 0.25f;

    [Header("Kurvenform")]
    [Range(0f, 1f)][SerializeField] private float cp1SideBias = 0.1f;
    [Range(0f, 1f)][SerializeField] private float cp2SideBias = 0.3f;

    [Header("Pausen (Sekunden)")]
    [SerializeField] private float pauseAfterGrab = 0.3f;
    [SerializeField] private float pauseAfterDrop = 0.4f;

    [Header("Würfel-Offset zur Hand")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0f, -0.05f, 0.05f);

    // -----------------------------------------------------------------------
    // State Machine
    // -----------------------------------------------------------------------

    private enum State { Idle, MovingToBlock, Grabbing, ArcCarry, Dropping, ArcReturn }
    private State state = State.Idle;

    private Transform heldBlock;   // aktiv gehalten (ab GrabBlock)
    private Rigidbody heldRb;
    private Collider heldCol;

    private Transform pendingBlock; // Ziel-Block, noch nicht gegriffen
    private Rigidbody pendingRb;
    private Collider pendingCol;

    private Vector3 blockGoal;
    private Vector3 dropGoal;
    private Vector3 homePos;

    // Bézier
    private Vector3 arcStart, arcEnd, peakPos, cpA, cpB;
    private float arcT, segALen, segBLen, totalLen, tSplit;

    // Finger
    private float fingerGrip = 0f;
    private float fingerGripTarget = 0f;
    private Quaternion[] fingerBaseRots;

    private float pauseTimer;
    private Action onDone;

    public bool IsIdle => state == State.Idle;

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (centerPartition == null)
        {
            var go = GameObject.Find("CenterPartition");
            if (go != null) centerPartition = go.transform;
        }

        var kb = GetComponent<HandProxyKeyboardControl>();
        if (kb != null) kb.enabled = false;
        var grb = GetComponent<SimpleGrabber>();
        if (grb != null) grb.enabled = false;
    }

    private void Start()
    {
        // Finger-Base-Rotationen cachen
        if (fingerMCPBones != null)
        {
            fingerBaseRots = new Quaternion[fingerMCPBones.Length];
            for (int i = 0; i < fingerMCPBones.Length; i++)
                if (fingerMCPBones[i] != null)
                    fingerBaseRots[i] = fingerMCPBones[i].localRotation;
        }

        // Ruheposition
        homePos = homePosOverride != Vector3.zero ? homePosOverride : Vector3.zero;

        // HandIKTarget sofort auf Ruheposition
        if (handIKTarget != null)
            handIKTarget.position = homePos;
    }

    private void LateUpdate()
    {
        TickStateMachine();
        AnimateFingers();

        // Würfel mitführen
        if (heldBlock != null)
        {
            Vector3 handPos = handBone != null ? handBone.position : handIKTarget.position;
            heldBlock.position = handPos + holdOffset;
        }
    }

    // -----------------------------------------------------------------------
    // State Machine
    // -----------------------------------------------------------------------

    private void TickStateMachine()
    {
        if (handIKTarget == null) return;

        switch (state)
        {
            case State.MovingToBlock:
                handIKTarget.position = Vector3.MoveTowards(
                    handIKTarget.position, blockGoal, moveSpeed * Time.deltaTime);
                if (Vector3.Distance(handIKTarget.position, blockGoal) <= snapDistance)
                {
                    handIKTarget.position = blockGoal;
                    fingerGripTarget = 0f;
                    pauseTimer = pauseAfterGrab;
                    state = State.Grabbing;
                }
                break;

            case State.Grabbing:
                fingerGripTarget = 1f;
                pauseTimer -= Time.deltaTime;
                if (pauseTimer <= 0f)
                {
                    GrabBlock();
                    BuildArc(handIKTarget.position, dropGoal);
                    state = State.ArcCarry;
                }
                break;

            case State.ArcCarry:
                AdvanceArc();
                if (arcT >= 1f)
                {
                    handIKTarget.position = arcEnd;
                    pauseTimer = pauseAfterDrop;
                    state = State.Dropping;
                }
                break;

            case State.Dropping:
                pauseTimer -= Time.deltaTime;
                if (pauseTimer <= 0f)
                {
                    fingerGripTarget = 0f;
                    DropBlock();
                    BuildArc(handIKTarget.position, homePos);
                    state = State.ArcReturn;
                }
                break;

            case State.ArcReturn:
                AdvanceArc();
                if (arcT >= 1f)
                {
                    handIKTarget.position = arcEnd;
                    state = State.Idle;
                    onDone?.Invoke();
                    onDone = null;
                }
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
            Debug.LogWarning("[ArmHandAutoMover] Noch beschäftigt.");
            return;
        }
        // Noch NICHT heldBlock – Würfel soll erst beim Greifen mitfliegen
        pendingBlock = block;
        pendingRb = block.GetComponent<Rigidbody>();
        pendingCol = block.GetComponent<Collider>();
        heldBlock = null;
        blockGoal = block.position;
        dropGoal = dropPosition;
        onDone = callback;

        fingerGripTarget = 0f;
        state = State.MovingToBlock;
    }

    // -----------------------------------------------------------------------
    // Finger-Animation
    // -----------------------------------------------------------------------

    private void AnimateFingers()
    {
        fingerGrip = Mathf.MoveTowards(fingerGrip, fingerGripTarget, fingerSpeed * Time.deltaTime);
        if (fingerMCPBones == null || fingerBaseRots == null) return;

        for (int i = 0; i < fingerMCPBones.Length; i++)
        {
            if (fingerMCPBones[i] == null) continue;
            float angle = Mathf.Lerp(0f, fingerCloseAngle, fingerGrip);
            fingerMCPBones[i].localRotation =
                fingerBaseRots[i] * Quaternion.AngleAxis(angle, fingerFlexAxis);
        }
    }

    // -----------------------------------------------------------------------
    // Bézier-Bogen
    // -----------------------------------------------------------------------

    private void BuildArc(Vector3 start, Vector3 end)
    {
        arcStart = start;
        arcEnd = end;
        arcT = 0f;

        if (centerPartition != null)
        {
            Vector3 p = centerPartition.position;
            float spanX = Mathf.Abs(end.x - start.x);
            float spanZ = Mathf.Abs(end.z - start.z);
            float px, pz;
            if (spanZ >= spanX) { pz = p.z; px = Mathf.Lerp(start.x, end.x, 0.5f); }
            else { px = p.x; pz = Mathf.Lerp(start.z, end.z, 0.5f); }
            float py = Mathf.Max(Mathf.Max(start.y, end.y) + minArcHeight, p.y + partitionClearance);
            peakPos = new Vector3(px, py, pz);
        }
        else
        {
            peakPos = Vector3.Lerp(start, end, 0.5f);
            peakPos.y = Mathf.Max(start.y, end.y) + minArcHeight;
        }

        cpA = new Vector3(Mathf.Lerp(start.x, peakPos.x, cp1SideBias), peakPos.y,
                          Mathf.Lerp(start.z, peakPos.z, cp1SideBias));
        cpB = new Vector3(Mathf.Lerp(peakPos.x, end.x, cp2SideBias), peakPos.y,
                          Mathf.Lerp(peakPos.z, end.z, cp2SideBias));

        const int segs = 20;
        segALen = 0f;
        Vector3 prev = arcStart;
        for (int i = 1; i <= segs; i++)
        {
            Vector3 pt = QuadBezier(arcStart, cpA, peakPos, i / (float)segs);
            segALen += Vector3.Distance(prev, pt); prev = pt;
        }
        segBLen = 0f; prev = peakPos;
        for (int i = 1; i <= segs; i++)
        {
            Vector3 pt = QuadBezier(peakPos, cpB, arcEnd, i / (float)segs);
            segBLen += Vector3.Distance(prev, pt); prev = pt;
        }
        segALen = Mathf.Max(segALen, 0.001f);
        segBLen = Mathf.Max(segBLen, 0.001f);
        totalLen = segALen + segBLen;
        tSplit = segALen / totalLen;
    }

    private void AdvanceArc()
    {
        arcT = Mathf.Clamp01(arcT + (moveSpeed * Time.deltaTime) / totalLen);
        Vector3 pos = arcT <= tSplit
            ? QuadBezier(arcStart, cpA, peakPos, tSplit > 0f ? arcT / tSplit : 1f)
            : QuadBezier(peakPos, cpB, arcEnd,
                (1f - tSplit) > 0f ? (arcT - tSplit) / (1f - tSplit) : 1f);
        handIKTarget.position = pos;
    }

    private static Vector3 QuadBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    // -----------------------------------------------------------------------
    // Grab / Drop
    // -----------------------------------------------------------------------

    private void GrabBlock()
    {
        // Jetzt erst wirklich greifen – pending ? held
        heldBlock = pendingBlock;
        heldRb = pendingRb;
        heldCol = pendingCol;
        pendingBlock = null; pendingRb = null; pendingCol = null;

        if (heldBlock == null) return;
        if (heldRb != null) { heldRb.isKinematic = true; heldRb.useGravity = false; }
        if (heldCol != null) heldCol.enabled = false;
    }

    private void DropBlock()
    {
        if (heldBlock == null) return;
        heldBlock.position = dropGoal;
        if (heldRb != null) { heldRb.isKinematic = false; heldRb.useGravity = true; }
        if (heldCol != null) heldCol.enabled = true;
        heldBlock = null; heldRb = null; heldCol = null;
    }

    // -----------------------------------------------------------------------
    // Gizmos
    // -----------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Ruheposition (magenta) – auch außerhalb Play-Mode
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(homePosOverride, 0.05f);

        if (!Application.isPlaying) return;

        Gizmos.color = Color.cyan;
        Vector3 prev = arcStart;
        for (int i = 1; i <= 20; i++)
        {
            Vector3 p = QuadBezier(arcStart, cpA, peakPos, i / 20f);
            Gizmos.DrawLine(prev, p); prev = p;
        }
        Gizmos.color = Color.green;
        prev = peakPos;
        for (int i = 1; i <= 20; i++)
        {
            Vector3 p = QuadBezier(peakPos, cpB, arcEnd, i / 20f);
            Gizmos.DrawLine(prev, p); prev = p;
        }
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(peakPos, 0.035f);
        if (handIKTarget != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(handIKTarget.position, 0.04f);
        }
    }
#endif
}