using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Bewegt HandIKTarget entlang einer glatten Catmull-Rom-Kurve durch
/// automatisch berechnete Wegpunkte, die Boxwände umgehen.
///
/// Wegpunkt-Strategie (keine manuellen Punkte nötig):
///   Aufheben:  start ? liftAboveBlock ? overWall ? block
///   Ablegen:   block ? overWall ? liftAboveDrop ? drop
///   Rückkehr:  drop  ? midAir        ? home
///
/// Die "over wall"-Punkte werden aus den Box-Bounds berechnet,
/// sodass die Hand immer über den Rand geht, nie durch.
/// </summary>
public class ArmHandAutoMover : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("IK Target")]
    [SerializeField] private Transform handIKTarget;

    [Header("Hand Bone (nur für Würfel-Mitführen)")]
    [SerializeField] private Transform handBone;

    [Header("Handgelenk-Knick in der Box")]
    [Tooltip("mixamorig:RightHand – wird in der Box nach unten geneigt")]
    [SerializeField] private Transform wristBone;
    [Tooltip("Lokale Rotation des Handgelenks wenn Hand IN der Box ist (nach unten zeigen)")]
    [SerializeField] private Vector3 wristInBoxRotation = new Vector3(60f, 0f, 0f);
    [Tooltip("Lokale Rotation des Handgelenks in Ruhe (außerhalb Box)")]
    [SerializeField] private Vector3 wristNeutralRotation = new Vector3(0f, 0f, 0f);
    [Tooltip("Wie schnell das Handgelenk knickt/streckt (Grad/s)")]
    [SerializeField] private float wristSpeed = 180f;

    [Header("Finger Bones")]
    [SerializeField] private Transform[] fingerMCPBones;

    [Header("Finger Greif-Winkel")]
    [SerializeField] private Vector3 fingerFlexAxis = new Vector3(1f, 0f, 0f);
    [SerializeField] private float fingerCloseAngle = 65f;
    [SerializeField] private float fingerSpeed = 3f;

    [Header("Bewegung")]
    [SerializeField] private float moveSpeed = 1.2f;
    [SerializeField] private float snapDistance = 0.03f;

    [Header("Ruheposition (Weltkoordinaten)")]
    [SerializeField] private Vector3 homePosOverride = new Vector3(0.3f, 0.765f, -0.25f);

    [Header("Box-Wände (für Wand-Kollisionsvermeidung)")]
    [Tooltip("BoxRoot-Collider der die gesamte Box umfasst")]
    [SerializeField] private Collider boxCollider;
    [Tooltip("Wie weit über dem Boxrand die Hand fährt")]
    [SerializeField] private float wallClearance = 0.12f;
    [Tooltip("Wie weit über dem Würfel die Hand angehoben wird")]
    [SerializeField] private float liftHeight = 0.18f;

    [Header("Pausen (Sekunden)")]
    [SerializeField] private float pauseAfterGrab = 0.3f;
    [SerializeField] private float pauseAfterDrop = 0.4f;

    [Header("Würfel-Offset zur Hand")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0f, -0.05f, 0.05f);
    [Tooltip("Wie weit über dem Würfel-Mittelpunkt die Hand stoppt (Würfel Scale Y = 0.025)")]
    [SerializeField] private float grabHeightOffset = 0.025f;

    // -----------------------------------------------------------------------
    // State Machine
    // -----------------------------------------------------------------------

    private enum State { Idle, MovingToBlock, Grabbing, ArcCarry, Dropping, ArcReturn }
    private State state = State.Idle;

    private Transform pendingBlock;
    private Rigidbody pendingRb;
    private Collider pendingCol;

    private Transform heldBlock;
    private Rigidbody heldRb;
    private Collider heldCol;

    private Vector3 blockGoal;
    private Vector3 dropGoal;
    private Vector3 homePos;

    // Spline
    private List<Vector3> splinePoints = new List<Vector3>();
    private float splineLength;
    private float splineT;        // 0..1 über gesamte Kurve

    // Finger
    private float fingerGrip = 0f;
    private float fingerGripTarget = 0f;
    private Quaternion[] fingerBaseRots;

    // Handgelenk
    private float wristBendTarget = 0f; // 0 = neutral, 1 = in Box gebogen
    private float wristBendCurrent = 0f;

    private float pauseTimer;
    private Action onDone;

    public bool IsIdle => state == State.Idle;

    // -----------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        var kb = GetComponent<HandProxyKeyboardControl>();
        if (kb != null) kb.enabled = false;
        var grb = GetComponent<SimpleGrabber>();
        if (grb != null) grb.enabled = false;
    }

    private void Start()
    {
        if (fingerMCPBones != null)
        {
            fingerBaseRots = new Quaternion[fingerMCPBones.Length];
            for (int i = 0; i < fingerMCPBones.Length; i++)
                if (fingerMCPBones[i] != null)
                    fingerBaseRots[i] = fingerMCPBones[i].localRotation;
        }

        homePos = homePosOverride != Vector3.zero ? homePosOverride : transform.position;
        if (handIKTarget != null) handIKTarget.position = homePos;
    }

    private void LateUpdate()
    {
        TickStateMachine();
        AnimateFingers();
        AnimateWrist();

        if (heldBlock != null)
        {
            Vector3 anchor = handBone != null ? handBone.position : handIKTarget.position;
            heldBlock.position = anchor + holdOffset;
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
                AdvanceSpline();
                wristBendTarget = IsAboveBox(handIKTarget.position) ? 1f : 0f;
                if (splineT >= 1f)
                {
                    handIKTarget.position = blockGoal;
                    fingerGripTarget = 0f;
                    pauseTimer = pauseAfterGrab;
                    state = State.Grabbing;
                }
                break;

            case State.Grabbing:
                fingerGripTarget = 1f;
                wristBendTarget = 1f;
                pauseTimer -= Time.deltaTime;
                if (pauseTimer <= 0f)
                {
                    GrabBlock();
                    BuildSpline(handIKTarget.position, dropGoal, carrying: true);
                    state = State.ArcCarry;
                }
                break;

            case State.ArcCarry:
                AdvanceSpline();
                wristBendTarget = IsAboveBox(handIKTarget.position) ? 1f : 0f;
                if (splineT >= 1f)
                {
                    handIKTarget.position = dropGoal;
                    pauseTimer = pauseAfterDrop;
                    state = State.Dropping;
                }
                break;

            case State.Dropping:
                wristBendTarget = 1f;
                pauseTimer -= Time.deltaTime;
                if (pauseTimer <= 0f)
                {
                    fingerGripTarget = 0f;
                    DropBlock();
                    BuildSpline(handIKTarget.position, homePos, carrying: false);
                    state = State.ArcReturn;
                }
                break;

            case State.ArcReturn:
                AdvanceSpline();
                wristBendTarget = IsAboveBox(handIKTarget.position) ? 1f : 0f;
                if (splineT >= 1f)
                {
                    handIKTarget.position = homePos;
                    wristBendTarget = 0f;
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

        pendingBlock = block;
        pendingRb = block.GetComponent<Rigidbody>();
        pendingCol = block.GetComponent<Collider>();
        heldBlock = null;
        // Y-Offset damit Hand über dem Würfel stoppt, nicht im Würfel
        blockGoal = block.position + Vector3.up * grabHeightOffset;
        dropGoal = dropPosition + Vector3.up * grabHeightOffset;
        onDone = callback;

        fingerGripTarget = 0f;

        // Kurve: home ? über Wandrand ? über Block ? Block
        BuildSpline(handIKTarget.position, blockGoal, carrying: false);
        state = State.MovingToBlock;
    }

    // -----------------------------------------------------------------------
    // Spline-Aufbau
    // -----------------------------------------------------------------------

    /// <summary>
    /// Berechnet Wegpunkte die die Boxwände umgehen.
    /// Nutzt Catmull-Rom für glatte Übergänge.
    /// </summary>
    private void BuildSpline(Vector3 start, Vector3 end, bool carrying)
    {
        splinePoints.Clear();
        splineT = 0f;

        float wallTop = GetWallTop();

        // Peak = direkt über dem Mittelpunkt zwischen Start und End
        // Das ist immer korrekt egal wo Start/End liegen
        Vector3 mid = Vector3.Lerp(start, end, 0.5f);
        Vector3 peak = new Vector3(mid.x, wallTop, mid.z);

        splinePoints.Add(start);
        splinePoints.Add(peak);
        splinePoints.Add(end);

        splineLength = Vector3.Distance(start, peak) + Vector3.Distance(peak, end);
        splineLength = Mathf.Max(splineLength, 0.001f);
    }

    /// <summary>
    /// Fährt den Spline mit gleichmäßiger Geschwindigkeit ab.
    /// Catmull-Rom interpoliert zwischen den Wegpunkten ? weiche Kurven.
    /// </summary>
    private void AdvanceSpline()
    {
        splineT = Mathf.Clamp01(splineT + (moveSpeed * Time.deltaTime) / splineLength);

        int count = splinePoints.Count;
        float scaledT = splineT * (count - 1);
        int seg = Mathf.Min((int)scaledT, count - 2);
        float localT = scaledT - seg;

        // Catmull-Rom Kontrollpunkte (gespiegelt an den Rändern)
        Vector3 p0 = splinePoints[Mathf.Max(seg - 1, 0)];
        Vector3 p1 = splinePoints[seg];
        Vector3 p2 = splinePoints[Mathf.Min(seg + 1, count - 1)];
        Vector3 p3 = splinePoints[Mathf.Min(seg + 2, count - 1)];

        handIKTarget.position = CatmullRom(p0, p1, p2, p3, localT);
    }

    // -----------------------------------------------------------------------
    // Box-Geometrie Helpers
    // -----------------------------------------------------------------------

    private float GetWallTop()
    {
        if (boxCollider != null)
            return boxCollider.bounds.max.y + wallClearance;
        var cp = GameObject.Find("CenterPartition");
        if (cp != null) return cp.transform.position.y + wallClearance + 0.1f;
        return homePosOverride.y + liftHeight;
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

    private void AnimateWrist()
    {
        if (wristBone == null) return;

        // Sanft zwischen neutral und gebogen interpolieren
        wristBendCurrent = Mathf.MoveTowards(
            wristBendCurrent, wristBendTarget,
            (wristSpeed * Time.deltaTime) / 90f); // normiert auf [0,1]

        Vector3 targetEuler = Vector3.Lerp(wristNeutralRotation, wristInBoxRotation, wristBendCurrent);
        wristBone.localRotation = Quaternion.Euler(targetEuler);
    }

    /// <summary>
    /// Prüft ob die Hand-Position XZ innerhalb der Box-Bounds liegt
    /// (unabhängig von Y – Hand kann über dem Rand sein).
    /// </summary>
    private bool IsAboveBox(Vector3 worldPos)
    {
        if (boxCollider == null) return false;
        Bounds b = boxCollider.bounds;
        return worldPos.x > b.min.x && worldPos.x < b.max.x &&
               worldPos.z > b.min.z && worldPos.z < b.max.z;
    }

    // -----------------------------------------------------------------------
    // Grab / Drop
    // -----------------------------------------------------------------------

    private void GrabBlock()
    {
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
    // Catmull-Rom Interpolation
    // -----------------------------------------------------------------------

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t
        );
    }

    // -----------------------------------------------------------------------
    // Gizmos
    // -----------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Ruheposition
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(homePosOverride, 0.05f);

        if (!Application.isPlaying || splinePoints == null || splinePoints.Count < 2) return;

        // Spline-Kurve
        Gizmos.color = Color.cyan;
        int count = splinePoints.Count;
        for (int seg = 0; seg < count - 1; seg++)
        {
            Vector3 p0 = splinePoints[Mathf.Max(seg - 1, 0)];
            Vector3 p1 = splinePoints[seg];
            Vector3 p2 = splinePoints[Mathf.Min(seg + 1, count - 1)];
            Vector3 p3 = splinePoints[Mathf.Min(seg + 2, count - 1)];

            Vector3 prev = p1;
            for (int s = 1; s <= 10; s++)
            {
                float lt = s / 10f;
                Vector3 pt = CatmullRom(p0, p1, p2, p3, lt);
                Gizmos.DrawLine(prev, pt);
                prev = pt;
            }
        }

        // Wegpunkte
        Gizmos.color = Color.yellow;
        foreach (var pt in splinePoints)
            Gizmos.DrawWireSphere(pt, 0.025f);

        // Aktuelles Ziel
        if (handIKTarget != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(handIKTarget.position, 0.04f);
        }
    }
#endif
}