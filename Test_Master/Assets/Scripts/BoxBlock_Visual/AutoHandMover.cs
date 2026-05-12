using UnityEngine;
using System;

/// <summary>
/// Bewegt den HandProxy in einer natürlichen Bogenbewegung über die CenterPartition.
/// NEU: Synchronisiert jeden Frame das HandIKTarget mit der eigenen Position,
///      damit der Two Bone IK Constraint (Animation Rigging) den XBot-Arm steuert.
/// </summary>
public class AutoHandMover : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Bewegung")]
    [SerializeField] private float moveSpeed = 1.2f;
    [SerializeField] private float snapDistance = 0.03f;

    [Header("Partition & Bogenhöhe")]
    [Tooltip("Referenz auf das CenterPartition-GameObject. " +
             "Wenn leer, wird automatisch nach 'CenterPartition' in der Szene gesucht.")]
    [SerializeField] private Transform centerPartition = null;

    [Tooltip("Wie weit der Scheitel über der Partition-Oberkante liegt (Sicherheitsabstand).")]
    [SerializeField] private float partitionClearance = 0.15f;

    [Tooltip("Mindesthöhe des Scheitels über der höchsten der beiden Endpositionen.")]
    [SerializeField] private float minArcHeight = 0.25f;

    [Header("Kurvenform")]
    [Tooltip("Kontrollpunkt A: wie weit der Aufstieg seitlich zum Peak versetzt ist (0 = senkrecht).")]
    [Range(0f, 1f)]
    [SerializeField] private float cp1SideBias = 0.1f;

    [Tooltip("Kontrollpunkt B: wie weit der Abstieg seitlich zum Ziel versetzt ist (0 = senkrecht).")]
    [Range(0f, 1f)]
    [SerializeField] private float cp2SideBias = 0.3f;

    [Header("Pausen (Sekunden)")]
    [SerializeField] private float pauseAfterGrab = 0.3f;
    [SerializeField] private float pauseAfterDrop = 0.4f;

    [Header("Würfel-Offset zur Hand (Weltkoordinaten)")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0f, 0.05f, 0f);

    // -----------------------------------------------------------------------
    // NEU: IK Target + Hand Grip
    // -----------------------------------------------------------------------

    [Header("IK Target (Animation Rigging)")]
    [Tooltip("HandIKTarget zuweisen.")]
    [SerializeField] private Transform ikTarget;

    [Header("Hand Greifen")]
    [Tooltip("Das GameObject mit HandGrip.cs drauf.")]
    [SerializeField] private HandGrip handGrip;

    [Header("IK Offset Korrektur")]
    [Tooltip("mixamorig:RightHand zuweisen – misst live den Versatz zwischen Ghost und echter Hand.")]
    [SerializeField] private Transform realHandBone;

    [Tooltip("Mindesthöhe der Hand – verhindert dass Unterarm durch Vorderwand geht.")]
    [SerializeField] private float minHandHeight = 0.5f;

    [Tooltip("Zusätzlicher Z-Offset – Hand weiter nach +Z schieben vom Avatar weg.")]
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

    // Zwei-Segment-Bogen
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
                Debug.Log("[AutoHandMover] CenterPartition automatisch gefunden: " + go.name);
            }
            else
            {
                Debug.LogWarning("[AutoHandMover] CenterPartition nicht gesetzt und nicht gefunden!");
            }
        }

        // ikTarget auto-suchen falls nicht gesetzt
        if (ikTarget == null)
        {
            var go = GameObject.Find("HandIKTarget");
            if (go != null)
            {
                ikTarget = go.transform;
                Debug.Log("[AutoHandMover] HandIKTarget automatisch gefunden.");
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
                SyncIKTarget();                         // NEU
                if (Arrived(blockGoal))
                {
                    if (handGrip != null) handGrip.SetWristBend(true); // Handgelenk knicken
                    pauseTimer = pauseAfterGrab;
                    state = State.Grabbing;
                }
                break;

            case State.Grabbing:
                pauseTimer -= Time.deltaTime;

                // Faust schließt sich im letzten 30% der Pause (Hand ist schon am Würfel)
                // Wert erhöhen (z.B. 0.5f) um früher zu greifen
                if (pauseTimer <= pauseAfterGrab * 0.3f)
                    if (handGrip != null) handGrip.Grip();

                if (pauseTimer <= 0f)
                {
                    GrabBlock();
                    if (handGrip != null) handGrip.SetWristBend(false); // Handgelenk strecken
                    BuildArc(transform.position, dropGoal);
                    state = State.ArcCarry;
                }
                break;

            case State.ArcCarry:
                AdvanceArc();
                SyncIKTarget();                         // NEU
                CarryBlock();
                if (arcT >= 1f)
                {
                    transform.position = arcEnd;
                    SyncIKTarget();                     // NEU
                    CarryBlock();
                    pauseTimer = pauseAfterDrop;
                    state = State.Dropping;
                }
                break;

            case State.Dropping:
                pauseTimer -= Time.deltaTime;
                if (pauseTimer <= pauseAfterDrop * 0.5f)
                    if (handGrip != null) handGrip.SetWristBend(true); // Handgelenk knicken
                if (pauseTimer <= 0f)
                {
                    DropBlock();
                    if (handGrip != null) handGrip.SetWristBend(false); // Handgelenk strecken
                    BuildArc(transform.position, homePos);
                    state = State.ArcReturn;
                }
                break;

            case State.ArcReturn:
                AdvanceArc();
                SyncIKTarget();                         // NEU
                if (arcT >= 1f)
                {
                    transform.position = arcEnd;
                    SyncIKTarget();                     // NEU
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
            Debug.LogWarning("[AutoHandMover] Noch beschäftigt.");
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

    // -----------------------------------------------------------------------
    // Arc – Zwei verkettete quadratische Bézier-Segmente
    // -----------------------------------------------------------------------

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
        if (heldRb != null) { heldRb.isKinematic = true; heldRb.useGravity = false; }
        if (heldCol != null) heldCol.enabled = false;
        // Grip wird bereits im Grabbing-State ausgelöst (früher)
    }

    private void CarryBlock()
    {
        if (heldBlock != null)
            heldBlock.position = transform.position + holdOffset;
    }

    private void DropBlock()
    {
        if (heldBlock == null) return;

        heldBlock.position = dropGoal;

        if (heldRb != null) { heldRb.isKinematic = false; heldRb.useGravity = true; }
        if (heldCol != null) heldCol.enabled = true;

        heldBlock = null;
        heldRb = null;
        heldCol = null;

        if (handCol != null) handCol.isTrigger = false;
        if (handGrip != null) handGrip.Release();      // NEU
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void MoveTo(Vector3 goal) =>
        transform.position = Vector3.MoveTowards(
            transform.position, goal, moveSpeed * Time.deltaTime);

    private bool Arrived(Vector3 goal) =>
        Vector3.Distance(transform.position, goal) <= snapDistance;

    /// <summary>
    /// NEU: Synchronisiert HandIKTarget mit der aktuellen HandProxy-Position.
    /// Der Two Bone IK Constraint auf RightArmIK übernimmt den Rest automatisch.
    /// </summary>
    private void SyncIKTarget()
    {
        if (ikTarget == null) return;

        Vector3 pos = transform.position;

        // Mindesthöhe – Unterarm kommt von oben statt durch die Vorderwand
        pos.y = Mathf.Max(pos.y, minHandHeight);

        // Z-Offset – Hand weiter vom Avatar weg Richtung Tisch
        pos.z += handZOffset;

        if (realHandBone != null)
        {
            // Live-Versatz zwischen Ghost und echter Hand kompensieren (nur X und Z)
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