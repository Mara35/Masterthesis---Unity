using UnityEngine;
using System;

public class AutoHandMover : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Bewegung")]
    [SerializeField] private float moveSpeed = 1.2f;
    [SerializeField] private float liftHeight = 0.15f;   // wie hoch über Würfel anheben
    [SerializeField] private float snapDistance = 0.03f;

    [Header("Pausen (Sekunden)")]
    [SerializeField] private float pauseAfterGrab = 0.3f;
    [SerializeField] private float pauseAfterDrop = 0.4f;

    [Header("Würfel-Offset zur Hand (Weltkoordinaten)")]
    [Tooltip("Versatz des Würfels relativ zur Handmitte während des Tragens.\n" +
             "Y = 0 ? Würfelmitte auf Handmitte. Erhöhe Y wenn der Würfel zu tief hängt.")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0f, 0.05f, 0f);

    // -----------------------------------------------------------------------
    // State Machine
    // -----------------------------------------------------------------------

    private enum State
    {
        Idle, MovingToBlock, Grabbing, LiftingUp,
        MovingToTarget, Dropping, Returning
    }

    private State state = State.Idle;
    private Transform heldBlock = null;
    private Rigidbody heldRb = null;
    private Collider heldCol = null;
    private Collider handCol = null;

    private Vector3 blockGoal;
    private Vector3 liftGoal;
    private Vector3 dropGoal;
    private Vector3 homePos;
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

        // Manuelle Steuerung deaktivieren
        var kb = GetComponent<HandProxyKeyboardControl>();
        if (kb != null) kb.enabled = false;

        var grb = GetComponent<SimpleGrabber>();
        if (grb != null) grb.enabled = false;
    }

    private void Update()
    {
        switch (state)
        {
            case State.Idle: break;

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
                    liftGoal = transform.position + Vector3.up * liftHeight;
                    state = State.LiftingUp;
                }
                break;

            case State.LiftingUp:
                MoveTo(liftGoal);
                CarryBlock();   // Würfel folgt der Hand
                if (Arrived(liftGoal))
                    state = State.MovingToTarget;
                break;

            case State.MovingToTarget:
                MoveTo(dropGoal);
                CarryBlock();
                if (Arrived(dropGoal))
                {
                    pauseTimer = pauseAfterDrop;
                    state = State.Dropping;
                }
                break;

            case State.Dropping:
                pauseTimer -= Time.deltaTime;
                if (pauseTimer <= 0f)
                {
                    DropBlock();
                    state = State.Returning;
                }
                break;

            case State.Returning:
                MoveTo(homePos);
                if (Arrived(homePos))
                {
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
        if (!IsIdle) { Debug.LogWarning("[AutoHandMover] Noch beschäftigt."); return; }

        heldBlock = block;
        heldRb = block.GetComponent<Rigidbody>();
        heldCol = block.GetComponent<Collider>();
        blockGoal = block.position;   // Hand fährt direkt zur Würfelposition
        dropGoal = dropPosition;
        onDone = callback;

        // Hand-Collider als Trigger setzen ? kein physikalisches Abprallen
        if (handCol != null) handCol.isTrigger = true;

        state = State.MovingToBlock;
    }

    // -----------------------------------------------------------------------
    // Grab / Carry / Drop
    // -----------------------------------------------------------------------

    private void GrabBlock()
    {
        if (heldBlock == null) return;

        // Physik deaktivieren ? Würfel fällt nicht mehr durch Tisch
        if (heldRb != null)
        {
            heldRb.isKinematic = true;
            heldRb.useGravity = false;
        }

        // Würfel-Collider deaktivieren ? kein Kollidieren mit Hand
        if (heldCol != null) heldCol.enabled = false;
    }

    /// <summary>
    /// Hält den Würfel relativ zur Handposition (kein Parenting ? kein Offset-Problem).
    /// </summary>
    private void CarryBlock()
    {
        if (heldBlock != null)
            heldBlock.position = transform.position + holdOffset;
    }

    private void DropBlock()
    {
        if (heldBlock == null) return;

        // Würfel exakt auf Ablagepunkt setzen
        heldBlock.position = dropGoal;

        // Physik wieder aktivieren ? Würfel liegt auf Tisch (nicht durch ihn hindurch)
        if (heldRb != null)
        {
            heldRb.isKinematic = false;
            heldRb.useGravity = true;
        }

        // Würfel-Collider wieder aktivieren
        if (heldCol != null) heldCol.enabled = true;

        heldBlock = null;
        heldRb = null;
        heldCol = null;

        // Hand-Collider wieder normal (kein Trigger mehr)
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
}