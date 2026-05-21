/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       GhostOrbController.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Attach to:  GhostOrb GameObject (nur EINMAL!)
 *
 * Sucht Würfel per POSITION (links der Partition) – nicht per Parent-Hierarchie.
 * Dadurch werden auch Würfel erkannt die vom PlayerOrb rübergelegt wurden.
 *
 * Setup im Inspector:
 *   - ghostTargetZone  ? StartZone des XBot (rechte Seite, Ablagebereich)
 *   - cubeTag          ? Tag aller Würfel (z.B. "Block"), oder leer lassen für Name-Suche
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostOrbController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Szenen-Referenzen")]
    [Tooltip("Ablagebereich auf der XBot-Seite (StartZone des XBot)")]
    public Transform ghostTargetZone;

    [Tooltip("FreezeZone für eigene FreezeCubes (links für Player, rechts für Ghost)")]
    public Transform freezeZone;

    [Tooltip("Eigene Ablageseite des Ghost (TargetZone – linke Seite)")]
    public Transform ghostOwnZone;

    [Tooltip("Tag aller Würfel-GameObjects (in Unity unter Inspector ? Tag setzen)")]
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
    private bool isCarryingFreeze = false;



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

            // Alle Renderer in Children sammeln und höchsten Punkt finden
            Renderer[] renderers = cp.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                float maxY = float.MinValue;
                foreach (Renderer r in renderers)
                    maxY = Mathf.Max(maxY, r.bounds.max.y);
                partitionTopY = maxY;
            }
            else
            {
                // Fallback: Tisch-Oberfläche über BoxCollider suchen
                BoxCollider bc = cp.GetComponentInChildren<BoxCollider>();
                if (bc != null)
                    partitionTopY = bc.bounds.max.y;
                else
                    partitionTopY = cp.transform.position.y + 0.05f;
            }
        }
        else
        {
            // CenterPartition nicht gefunden: Tischoberfläche über alle Renderer schätzen
            Debug.LogWarning("[Orb] 'CenterPartition' nicht gefunden – suche Tischoberfläche.");
            GameObject table = GameObject.Find("Table");
            if (table != null)
            {
                Renderer[] rs = table.GetComponentsInChildren<Renderer>();
                float maxY = 0f;
                foreach (Renderer r in rs) maxY = Mathf.Max(maxY, r.bounds.max.y);
                partitionTopY = maxY;
            }
            else
            {
                partitionTopY = 0.8f; // letzter Fallback
            }
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
        // FreezeCube auf eigener Seite hat höchste Priorität
        GameObject freezeTarget = FindFreezeCubeOnOwnSide();

        // 50% Chance: versuche roten Würfel vom Gegner zu klauen
        GameObject stealTarget = null;
        if (freezeTarget == null && Random.value < 0.5f)
            stealTarget = FindMalusCubeOnEnemySide();

        targetCube = freezeTarget ?? stealTarget ?? FindNearestCubeOnMySide();

        // Fallback: Cooldown ignorieren falls kein Würfel gefunden (verhindert Stillstand)
        if (targetCube == null)
            targetCube = FindNearestCubeOnMySide(ignoreCooldown: true);

        if (targetCube == null) return;

        isStealingMalus = (stealTarget != null);
        isCarryingFreeze = (freezeTarget != null);
        targetRb = targetCube.GetComponent<Rigidbody>();

        // Sofort sperren damit kein anderer Orb denselben Würfel wählt
        OrbSharedState.Lock(targetCube.GetInstanceID());

        // Orb auf falscher Seite ? erst zurückfliegen
        if (transform.position.x > partitionX)
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

    // Sucht einen Malus-Würfel (BonusCube mit negativem pointValue) auf der GEGNERISCHEN Seite
    private GameObject FindMalusCubeOnEnemySide()
    {
        GameObject nearest = null;
        float bestDist = float.MaxValue;

        foreach (BonusCube bc in FindObjectsOfType<BonusCube>())
        {
            if (bc.pointValue > 0) continue; // nur negative (rote) Würfel
            if (!bc.gameObject.activeInHierarchy) continue;
            if (!OrbSharedState.IsAvailable(bc.gameObject.GetInstanceID())) continue;

            // Gegnerische Seite = rechts der Partition (Ghost ist links)
            if (bc.transform.position.x <= partitionX) continue;

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

        Vector3 pos = transform.position;
        // Ablageposition bestimmen
        if (isCarryingFreeze)
            dropTarget = GetFreezeZonePosition();
        else if (isStealingMalus)
            dropTarget = GetRandomOwnSidePosition();
        else
            dropTarget = GetRandomDropPosition();
        liftTarget = new Vector3(pos.x, flyHeight, pos.z);
        crossTarget = new Vector3(dropTarget.x, flyHeight, dropTarget.z);
        state = State.LiftUp;

        Debug.Log($"[GhostOrb] {(isStealingMalus ? "Klaue Malus" : "Transfer")}: {targetCube.name}  Ziel: {dropTarget}");
    }

    private void Drop()
    {
        targetCube.transform.position = dropTarget;

        if (targetRb != null)
            targetRb.isKinematic = false;

        OrbSharedState.Unlock(targetCube.GetInstanceID());

        Debug.Log($"[GhostOrb] Abgelegt: {targetCube.name} an {dropTarget}");

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
    // Würfelsuche per POSITION (nicht per Parent)
    // Findet alle Würfel links der Partition – egal welcher Parent
    // -----------------------------------------------------------------------

    private GameObject FindNearestCubeOnMySide(bool ignoreCooldown = false)
    {
        GameObject nearest = null;
        float bestDist = float.MaxValue;

        // Alle Würfel per Tag suchen
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

            // Nur Würfel auf der linken (Ghost-)Seite
            if (cube.transform.position.x >= partitionX) continue;

            // Würfel der gerade getragen wird überspringen
            if (cube == targetCube) continue;
            // Gesperrte oder kürzlich abgelegte Würfel überspringen (shared state)
            if (ignoreCooldown ? !OrbSharedState.IsAvailableIgnoreCooldown(cube.GetInstanceID()) : !OrbSharedState.IsAvailable(cube.GetInstanceID())) continue;

            float d = Vector3.Distance(transform.position, cube.transform.position);
            if (d < bestDist) { bestDist = d; nearest = cube; }
        }

        return nearest;
    }

    // Fallback falls kein Tag gesetzt: sucht alle GameObjects die mit "Block" beginnen
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

    // Zufällige Position auf der eigenen (Ghost-)Seite – für gestohlene rote Würfel
    private GameObject FindFreezeCubeOnOwnSide()
    {
        // Nur suchen wenn FreezeZone korrekt zugewiesen ist
        if (freezeZone == null) return null;

        GameObject nearest = null;
        float bestDist = float.MaxValue;
        GameObject[] freezeCubes = null;

        try { freezeCubes = GameObject.FindGameObjectsWithTag("Freeze"); }
        catch { Debug.LogWarning("[Orb] Tag 'Freeze' nicht registriert!"); return null; }

        if (freezeCubes == null || freezeCubes.Length == 0) return null;

        foreach (GameObject fc in freezeCubes)
        {
            if (!fc.activeInHierarchy) continue;
            if (!OrbSharedState.IsAvailable(fc.GetInstanceID())) continue;
            float d = Vector3.Distance(transform.position, fc.transform.position);
            if (d < bestDist) { bestDist = d; nearest = fc; }
        }

        if (nearest != null)
            Debug.Log($"[Orb] FreezeCube gefunden: {nearest.name} Dist={bestDist:F2}");

        return nearest;
    }

    private Vector3 GetFreezeZonePosition()
    {
        if (freezeZone == null)
        {
            Debug.LogWarning("[Orb] freezeZone nicht zugewiesen! Bitte im Inspector setzen.");
            return transform.position;
        }

        Debug.Log($"[Orb] Lege FreezeCube in {freezeZone.name} ab.");

        Collider col = freezeZone.GetComponent<Collider>();
        if (col != null)
        {
            Bounds b = col.bounds;
            return new Vector3(
                b.center.x,
                b.max.y + 0.02f,
                b.center.z
            );
        }
        return freezeZone.position + Vector3.up * 0.05f;
    }

    private Vector3 GetRandomOwnSidePosition()
    {
        Transform zone = ghostOwnZone;
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
        // Fallback: links der Partition
        return new Vector3(partitionX - 0.15f, partitionTopY + 0.05f, Random.Range(-0.1f, 0.1f));
    }

    private Vector3 GetRandomDropPosition()
    {
        if (ghostTargetZone != null)
        {
            Collider col = ghostTargetZone.GetComponent<Collider>();
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

        float fx = partitionX + 0.15f;
        return new Vector3(fx, partitionTopY + 0.05f, Random.Range(-0.08f, 0.08f));
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    public void StartPlaying() { isActive = true; state = State.Idle; }

    public void Freeze(float seconds)
    {
        // Sofort einfrieren – nicht erst in der Coroutine
        isActive = false;

        // Würfel loslassen falls gerade getragen
        if (targetCube != null && targetRb != null)
        {
            targetRb.isKinematic = false;
            OrbSharedState.Unlock(targetCube.GetInstanceID());
            targetCube = null;
            targetRb = null;
        }

        StopAllCoroutines(); // laufende Bewegungs-Coroutines stoppen
        StartCoroutine(FreezeRoutine(seconds));
        Debug.Log($"[GhostOrb] Eingefroren für {seconds}s.");
    }

    private System.Collections.IEnumerator FreezeRoutine(float seconds)
    {
        yield return new UnityEngine.WaitForSeconds(seconds);

        isActive = true;
        state = State.Idle;
        Debug.Log($"[GhostOrb] Freeze beendet.");
    }

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
        Gizmos.color = Color.cyan;
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