using UnityEngine;
using System;

/// <summary>
/// Bewegt den HandProxy in einer natürlichen Bogenbewegung über die CenterPartition.
///
/// Strategie: ZWEI verkettete quadratische Bézier-Segmente mit einem festen
/// Zwischenpunkt direkt über der Partition.
///
///   Segment A:  arcStart  ? überPeak  ? peak        (Aufstieg)
///   Segment B:  peak      ? überEnd   ? arcEnd       (Abstieg)
///
/// „peak" liegt EXAKT über der CenterPartition auf sicherer Höhe.
/// Dadurch ist es physikalisch unmöglich, dass die Kurve die Wand schneidet.
///
/// Falls centerPartition nicht per Inspector gesetzt ist, sucht das Skript
/// beim Start automatisch nach einem GameObject namens "CenterPartition"
/// in der eigenen Szene.
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

    // Zwei-Segment-Bogen:
    //   Segment A (t in [0,0.5]): quadratische Bézier arcStart ? cpA ? peak
    //   Segment B (t in [0.5,1]): quadratische Bézier peak ? cpB ? arcEnd
    private Vector3 arcStart, arcEnd, peakPos, cpA, cpB;
    private float arcT = 0f;
    private float segALen = 1f;   // Länge Segment A
    private float segBLen = 1f;   // Länge Segment B
    private float totalLen = 1f;
    private float tSplit = 0.5f; // t-Wert an dem Segment A endet (= segALen/totalLen)

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

        // Partition auto-suchen wenn nicht per Inspector gesetzt
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
                Debug.LogWarning("[AutoHandMover] CenterPartition nicht gesetzt und nicht gefunden! " +
                                 "Bitte im Inspector zuweisen.");
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
                if (Arrived(blockGoal))
                {
                    pauseTimer = pauseAfterGrab;
                    state = State.Grabbing;
                }
                break;

            case State.Grabbing:
                pauseTimer -= Time.deltaTime;
                if (pauseTimer <= 0f)
                {
                    GrabBlock();
                    BuildArc(transform.position, dropGoal);
                    state = State.ArcCarry;
                }
                break;

            case State.ArcCarry:
                AdvanceArc();
                CarryBlock();
                if (arcT >= 1f)
                {
                    transform.position = arcEnd;
                    CarryBlock();
                    pauseTimer = pauseAfterDrop;
                    state = State.Dropping;
                }
                break;

            case State.Dropping:
                pauseTimer -= Time.deltaTime;
                if (pauseTimer <= 0f)
                {
                    DropBlock();
                    BuildArc(transform.position, homePos);
                    state = State.ArcReturn;
                }
                break;

            case State.ArcReturn:
                AdvanceArc();
                if (arcT >= 1f)
                {
                    transform.position = arcEnd;
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

    /// <summary>
    /// Baut den Zwei-Segment-Bogen.
    ///
    /// Der Peak liegt EXAKT über der CenterPartition bei sicherer Höhe.
    /// Segment A geht von start senkrecht nach oben zum peak.
    /// Segment B geht vom peak sanft abfallend zum end.
    ///
    /// Da der Peak ein echter Kurvenpunkt (kein Kontrollpunkt) ist,
    /// kann die Bézier-Kurve NIEMALS durch die Wand führen.
    /// </summary>
    private void BuildArc(Vector3 start, Vector3 end)
    {
        arcStart = start;
        arcEnd = end;
        arcT = 0f;

        // ?? 1. Peak direkt über der Partition ??????????????????????????????
        if (centerPartition != null)
        {
            Vector3 partPos = centerPartition.position;

            // Trennachse erkennen: In welcher Achse liegt die größte Bewegungsspanne?
            float spanX = Mathf.Abs(end.x - start.x);
            float spanZ = Mathf.Abs(end.z - start.z);

            float peakX, peakZ;
            if (spanZ >= spanX)
            {
                // Hauptbewegung in Z ? Trennwand liegt bei festem partPos.z
                peakZ = partPos.z;
                // X des Peaks: X-Anteil der Partition zwischen Start und Ziel interpolieren,
                // aber sicherheitshalber einfach Start-X nehmen (Hand bleibt in ihrer Spur)
                peakX = Mathf.Lerp(start.x, end.x, 0.5f);
            }
            else
            {
                // Hauptbewegung in X ? Trennwand liegt bei festem partPos.x
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
            // Fallback: Scheitelpunkt in der Mitte
            peakPos = Vector3.Lerp(start, end, 0.5f);
            peakPos.y = Mathf.Max(start.y, end.y) + minArcHeight;
        }

        // ?? 2. Kontrollpunkte der beiden quadratischen Segmente ?????????????
        //
        // cpA steuert den Aufstieg (Segment A: start?peak).
        //   cp1SideBias = 0  ? cpA liegt direkt über start (senkrechter Aufstieg)
        //   cp1SideBias = 1  ? cpA liegt direkt beim peak (sofort schräg)
        cpA = new Vector3(
            Mathf.Lerp(start.x, peakPos.x, cp1SideBias),
            peakPos.y,                                      // immer auf Peak-Höhe!
            Mathf.Lerp(start.z, peakPos.z, cp1SideBias)
        );

        // cpB steuert den Abstieg (Segment B: peak?end).
        //   cp2SideBias = 0  ? cpB liegt direkt beim peak (senkrechter Abstieg)
        //   cp2SideBias = 1  ? cpB liegt direkt beim end  (sanft schräg)
        cpB = new Vector3(
            Mathf.Lerp(peakPos.x, end.x, cp2SideBias),
            peakPos.y,                                      // immer auf Peak-Höhe!
            Mathf.Lerp(peakPos.z, end.z, cp2SideBias)
        );

        // ?? 3. Bogenlänge schätzen (für gleichmäßige Geschwindigkeit) ???????
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
        tSplit = segALen / totalLen;   // t-Wert an dem Segment A endet
    }

    /// <summary>
    /// Bewegt arcT vorwärts und berechnet die aktuelle Handposition.
    /// t in [0, tSplit]   ? Segment A (Aufstieg)
    /// t in [tSplit, 1]   ? Segment B (Abstieg)
    /// </summary>
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

    /// <summary>Quadratische Bézier: P0, P1 (Kontrollpunkt), P2</summary>
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
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private void MoveTo(Vector3 goal) =>
        transform.position = Vector3.MoveTowards(
            transform.position, goal, moveSpeed * Time.deltaTime);

    private bool Arrived(Vector3 goal) =>
        Vector3.Distance(transform.position, goal) <= snapDistance;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Segment A (cyan)
        Gizmos.color = Color.cyan;
        Vector3 prev = arcStart;
        for (int i = 1; i <= 20; i++)
        {
            float t = i / 20f;
            Vector3 p = QuadBezier(arcStart, cpA, peakPos, t);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }

        // Segment B (green)
        Gizmos.color = Color.green;
        prev = peakPos;
        for (int i = 1; i <= 20; i++)
        {
            float t = i / 20f;
            Vector3 p = QuadBezier(peakPos, cpB, arcEnd, t);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }

        // Kontrollpunkte & Peak (yellow/red)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(cpA, 0.025f);
        Gizmos.DrawWireSphere(cpB, 0.025f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(peakPos, 0.035f);
    }
#endif
}