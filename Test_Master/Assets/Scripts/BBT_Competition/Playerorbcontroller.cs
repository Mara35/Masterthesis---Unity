/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       PlayerOrbController.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Attach to:  PlayerOrb GameObject (Testkugel für XBot-Seite)
 *
 * Spiegelverkehrt zum GhostOrbController:
 *   - Sucht Würfel auf der RECHTEN Seite (positive X, XBot-Seite)
 *   - Legt sie auf der LINKEN Seite ab (Ghost-Seite)
 *
 * Später wird dieses Script durch die echte Sensor-Steuerung ersetzt.
 * Die Spiellogik (Würfelerkennung per Position) bleibt identisch.
 *
 * Setup im Inspector:
 *   - playerTargetZone ? StartZone des Ghost (linke Seite, Ablagebereich)
 *   - cubeTag          ? Tag aller Würfel (z.B. "Block")
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerOrbController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Szenen-Referenzen")]
    [Tooltip("Ablagebereich auf der Ghost-Seite (linke Seite)")]
    public Transform playerTargetZone;

    [Tooltip("Eigene Ablageseite des Players (StartZone – rechte Seite)")]
    public Transform playerOwnZone;

    [Tooltip("Tag aller Würfel-GameObjects")]
    public string cubeTag = "Block";

    [Header("Bewegung")]
    [Range(0.1f, 1.0f)]
    public float speed = 1.0f;

    [Tooltip("Wie hoch der Orb über die Partition-Oberkante hebt (Meter)")]
    public float liftHeight = 0.15f;

    [Tooltip("Minimale Y-Höhe des Orbs – manuell auf Tischoberfläche setzen")]
    public float minSafeY = 0.9f;

    [Tooltip("Radius zum Aufnehmen eines Würfels")]
    public float pickupRadius = 0.12f;

    [Tooltip("Radius zum Erreichen eines Wegpunkts")]
    public float waypointRadius = 0.05f;

    [Tooltip("Pause (s) bevor der nächste Würfel gesucht wird")]
    [Range(0f, 2f)]
    public float reactionDelay = 0.5f;

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private enum State
    {
        Idle, MovingToCube, LiftUp, CrossOver, LowerDown,
        ReturnLift, ReturnCross
    }
    private State state = State.Idle;

    private GameObject targetCube;
    private Rigidbody targetRb;

    private Vector3 liftTarget;
    private Vector3 crossTarget;
    private Vector3 dropTarget;
    private Vector3 returnTarget;

    private float flyHeight;
    private float partitionTopY;
    private float partitionX;
    private Transform centerPartition;

    private bool isActive = false;
    private bool isStealingMalus = false;



    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        GameObject cp = GameObject.Find("CenterPartition");
        if (cp != null)
        {
            centerPartition = cp.transform;
            partitionX = cp.transform.position.x;
            Renderer r = cp.GetComponentInChildren<Renderer>();
            partitionTopY = r != null ? r.bounds.max.y : cp.transform.position.y + 0.1f;
        }
        else
        {
            Debug.LogWarning("[PlayerOrb] 'CenterPartition' nicht gefunden!");
            partitionTopY = 1.0f;
            partitionX = 0f;
        }

        flyHeight = partitionTopY + liftHeight;

        isActive = true; // TODO: entfernen – nur zum Testen (wird später von CompetitionGameManager gesteuert)
    }

    private void Update()
    {
        if (!isActive) return;

        switch (state)
        {
            case State.Idle: HandleIdle(); break;
            case State.MovingToCube: HandleMoveToCube(); break;
            case State.LiftUp: HandleLiftUp(); break;
            case State.CrossOver: HandleCrossOver(); break;
            case State.LowerDown: HandleLowerDown(); break;
            case State.ReturnLift: HandleReturnLift(); break;
            case State.ReturnCross: HandleReturnCross(); break;
        }
    }

    // -----------------------------------------------------------------------
    // State Handlers
    // -----------------------------------------------------------------------

    private void HandleIdle()
    {
        // 50% Chance: versuche grünen Würfel vom Gegner zu klauen
        GameObject stealTarget = null;
        if (Random.value < 0.5f)
            stealTarget = FindMalusCubeOnEnemySide();

        targetCube = (stealTarget != null) ? stealTarget : FindNearestCubeOnMySide();

        // Fallback: Cooldown ignorieren falls kein Würfel gefunden (verhindert Stillstand)
        if (targetCube == null)
            targetCube = FindNearestCubeOnMySide(ignoreCooldown: true);

        if (targetCube == null) return;

        isStealingMalus = (stealTarget != null);
        targetRb = targetCube.GetComponent<Rigidbody>();

        // Orb auf falscher Seite ? erst zurückfliegen
        if (transform.position.x < partitionX)
        {
            returnTarget = new Vector3(targetCube.transform.position.x, flyHeight,
                                       targetCube.transform.position.z);
            liftTarget = new Vector3(transform.position.x, flyHeight, transform.position.z);
            crossTarget = returnTarget;
            state = State.ReturnLift;
        }
        else
        {
            state = State.MovingToCube;
        }
    }

    // Sucht einen Malus-Würfel auf der GEGNERISCHEN Seite (links = Ghost-Seite)
    private GameObject FindMalusCubeOnEnemySide()
    {
        GameObject nearest = null;
        float bestDist = float.MaxValue;

        foreach (BonusCube bc in FindObjectsOfType<BonusCube>())
        {
            if (bc.pointValue > 0) continue; // nur negative (rote) Würfel
            if (!bc.gameObject.activeInHierarchy) continue;
            if (!OrbSharedState.IsAvailable(bc.gameObject.GetInstanceID())) continue;

            // Gegnerische Seite = links der Partition (Player ist rechts)
            if (bc.transform.position.x >= partitionX) continue;

            float d = Vector3.Distance(transform.position, bc.transform.position);
            if (d < bestDist) { bestDist = d; nearest = bc.gameObject; }
        }

        return nearest;
    }

    private void HandleMoveToCube()
    {
        if (targetCube == null) { state = State.Idle; return; }

        MoveTowards(targetCube.transform.position);

        if (Vector3.Distance(transform.position, targetCube.transform.position) <= pickupRadius)
            PickUp();
    }

    private void HandleLiftUp()
    {
        MoveTowards(liftTarget);
        CarryCube();

        if (Vector3.Distance(transform.position, liftTarget) <= waypointRadius)
        {
            transform.position = liftTarget;
            state = State.CrossOver;
        }
    }

    private void HandleCrossOver()
    {
        Vector3 flatTarget = new Vector3(crossTarget.x, flyHeight, crossTarget.z);
        Vector3 next = Vector3.MoveTowards(transform.position, flatTarget, speed * Time.deltaTime);
        next.y = flyHeight;
        transform.position = next;
        CarryCube();

        if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                              new Vector3(flatTarget.x, 0, flatTarget.z)) <= waypointRadius)
        {
            transform.position = new Vector3(flatTarget.x, flyHeight, flatTarget.z);
            state = State.LowerDown;
        }
    }

    private void HandleLowerDown()
    {
        MoveTowards(dropTarget);
        CarryCube();

        if (Vector3.Distance(transform.position, dropTarget) <= waypointRadius)
            Drop();
    }

    private void HandleReturnLift()
    {
        MoveTowards(liftTarget);

        if (Vector3.Distance(transform.position, liftTarget) <= waypointRadius)
        {
            transform.position = liftTarget;
            state = State.ReturnCross;
        }
    }

    private void HandleReturnCross()
    {
        Vector3 flatTarget = new Vector3(crossTarget.x, flyHeight, crossTarget.z);
        Vector3 next = Vector3.MoveTowards(transform.position, flatTarget, speed * Time.deltaTime);
        next.y = flyHeight;
        transform.position = next;

        if (Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                              new Vector3(flatTarget.x, 0, flatTarget.z)) <= waypointRadius)
        {
            transform.position = flatTarget;
            state = State.MovingToCube;
        }
    }

    // -----------------------------------------------------------------------
    // Bewegung / Würfel mitführen
    // -----------------------------------------------------------------------

    private void MoveTowards(Vector3 target)
    {
        Vector3 next = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        next.y = Mathf.Max(next.y, minSafeY); // nie unter Tischoberfläche
        transform.position = next;
    }

    private void CarryCube()
    {
        if (targetCube != null)
            // Würfel leicht über dem Orb-Mittelpunkt halten damit er nicht durch Geometrie drückt
            targetCube.transform.position = transform.position + Vector3.up * 0.01f;
    }

    // -----------------------------------------------------------------------
    // Pickup / Drop
    // -----------------------------------------------------------------------

    private void PickUp()
    {
        if (targetRb != null)
        {
            targetRb.velocity = Vector3.zero;
            targetRb.isKinematic = true;
        }

        OrbSharedState.Lock(targetCube.GetInstanceID());

        Vector3 pos = transform.position;
        // Gestohlen: ins eigene Feld (rechts) ablegen, sonst normal ins Gegnerfeld
        dropTarget = isStealingMalus ? GetRandomOwnSidePosition() : GetRandomDropPosition();
        liftTarget = new Vector3(pos.x, flyHeight, pos.z);
        crossTarget = new Vector3(dropTarget.x, flyHeight, dropTarget.z);
        state = State.LiftUp;

        Debug.Log($"[PlayerOrb] {(isStealingMalus ? "Klaue Malus" : "Transfer")}: {targetCube.name}  Ziel: {dropTarget}");
    }

    private void Drop()
    {
        targetCube.transform.position = dropTarget;

        if (targetRb != null)
            targetRb.isKinematic = false;

        OrbSharedState.Unlock(targetCube.GetInstanceID());

        Debug.Log($"[PlayerOrb] Abgelegt: {targetCube.name} an {dropTarget}");

        targetCube = null;
        targetRb = null;

        StartCoroutine(ReactionPause());
    }

    private IEnumerator ReactionPause()
    {
        isActive = false;
        yield return new WaitForSeconds(reactionDelay);
        isActive = true;
        state = State.Idle;
    }

    // -----------------------------------------------------------------------
    // Würfelsuche per POSITION – rechte Seite (positive X)
    // -----------------------------------------------------------------------

    private GameObject FindNearestCubeOnMySide(bool ignoreCooldown = false)
    {
        GameObject nearest = null;
        float bestDist = float.MaxValue;

        GameObject[] allCubes;
        try
        {
            allCubes = string.IsNullOrEmpty(cubeTag)
                ? FindAllBlocksByName()
                : GameObject.FindGameObjectsWithTag(cubeTag);
        }
        catch
        {
            allCubes = FindAllBlocksByName();
        }

        foreach (GameObject cube in allCubes)
        {
            if (!cube.activeInHierarchy) continue;

            // Nur Würfel auf der rechten (XBot-)Seite
            if (cube.transform.position.x <= partitionX) continue;

            // Würfel der gerade getragen wird überspringen
            if (cube == targetCube) continue;
            // Gesperrte oder kürzlich abgelegte Würfel überspringen (shared state)
            if (ignoreCooldown ? !OrbSharedState.IsAvailableIgnoreCooldown(cube.GetInstanceID()) : !OrbSharedState.IsAvailable(cube.GetInstanceID())) continue;

            float d = Vector3.Distance(transform.position, cube.transform.position);
            if (d < bestDist) { bestDist = d; nearest = cube; }
        }

        return nearest;
    }

    private GameObject[] FindAllBlocksByName()
    {
        var result = new List<GameObject>();
        foreach (GameObject go in FindObjectsOfType<GameObject>())
        {
            if (go.name.StartsWith("Block"))
                result.Add(go);
        }
        return result.ToArray();
    }

    // Zufällige Position auf der eigenen (Player-)Seite – für gestohlene rote Würfel
    private Vector3 GetRandomOwnSidePosition()
    {
        Transform zone = playerOwnZone;
        if (zone != null)
        {
            Collider col = zone.GetComponent<Collider>();
            if (col != null)
            {
                Bounds b = col.bounds;
                float insetX = Mathf.Max(0.05f, b.size.x * 0.2f);
                float insetZ = Mathf.Max(0.05f, b.size.z * 0.2f);
                return new Vector3(
                    Random.Range(b.min.x + insetX, b.max.x - insetX),
                    b.max.y + 0.02f,
                    Random.Range(b.min.z + insetZ, b.max.z - insetZ)
                );
            }
        }
        // Fallback: rechts der Partition
        return new Vector3(partitionX + 0.15f, partitionTopY + 0.05f, Random.Range(-0.1f, 0.1f));
    }

    private Vector3 GetRandomDropPosition()
    {
        if (playerTargetZone != null)
        {
            Collider col = playerTargetZone.GetComponent<Collider>();
            if (col != null)
            {
                Bounds b = col.bounds;

                // Inset: 20% der Größe von jeder Seite abziehen
                float insetX = Mathf.Max(0.06f, b.size.x * 0.2f);
                float insetZ = Mathf.Max(0.06f, b.size.z * 0.2f);

                return new Vector3(
                    Random.Range(b.min.x + insetX, b.max.x - insetX),
                    b.max.y + 0.02f,
                    Random.Range(b.min.z + insetZ, b.max.z - insetZ)
                );
            }
        }

        float fx = partitionX - 0.15f;
        return new Vector3(fx, partitionTopY + 0.05f, Random.Range(-0.08f, 0.08f));
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void StartPlaying() { isActive = true; state = State.Idle; }

    public void StopPlaying()
    {
        isActive = false;
        if (targetCube != null && targetRb != null)
            targetRb.isKinematic = false;
        targetCube = null;
        targetRb = null;
    }

    // -----------------------------------------------------------------------
    // Gizmos
    // -----------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);

        if (!Application.isPlaying || dropTarget == Vector3.zero) return;

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(liftTarget, 0.03f);
        Gizmos.DrawLine(transform.position, liftTarget);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(crossTarget, 0.03f);
        Gizmos.DrawLine(liftTarget, crossTarget);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(dropTarget, 0.03f);
        Gizmos.DrawLine(crossTarget, dropTarget);
    }
}