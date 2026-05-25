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

    [Tooltip("FreezeZone für eigene FreezeCubes (links für Player, rechts für Ghost)")]
    public Transform freezeZone;

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
    private bool isCarryingFreeze = false;
    private bool isPegChallenge = false;
    private bool isSequenceChallenge = false;
    private List<SequenceCube> sequenceCubes = new List<SequenceCube>();
    private float sequenceMistakeChance = 0.3f;
    private int sequenceNextIdx = 0;

    // Peg Challenge State
    private List<GameObject> pendingPegs = new List<GameObject>();
    private List<Vector3> pegZoneTargets = new List<Vector3>();



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

        isActive = true; // PlayerOrb startet sofort

    }

    private void Update()
    {
        if (!isActive) return;

        // Peg Challenge Interrupt: nur einmal unterbrechen wenn Challenge startet
        if (isPegChallenge && state != State.Idle && targetCube != null)
        {
            PegChallengeCube currentPeg = targetCube.GetComponent<PegChallengeCube>();
            // Nur unterbrechen wenn aktuell kein Peg getragen wird
            if (currentPeg == null)
            {
                targetRb.isKinematic = false;
                OrbSharedState.Unlock(targetCube.GetInstanceID());
                targetCube = null;
                targetRb = null;
                isStealingMalus = false;
                isCarryingFreeze = false;
                state = State.Idle;
            }
        }

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
        // Höchste Priorität: Peg Challenge
        if (isPegChallenge)
        {
            GameObject nextPeg = FindNextUnplacedPeg();
            if (nextPeg != null && OrbSharedState.IsAvailable(nextPeg.GetInstanceID()))
            {
                targetCube = nextPeg;
                targetRb = nextPeg.GetComponent<Rigidbody>();
                isStealingMalus = false;
                isCarryingFreeze = false;
                OrbSharedState.Lock(nextPeg.GetInstanceID());

                if (targetRb != null)
                    targetRb.constraints = RigidbodyConstraints.FreezeRotation;

                // Erst zum Peg fahren (MovingToCube), dropTarget wird in PickUp gesetzt
                state = State.MovingToCube;
                return;
            }
        }

        // Priorität 2: Sequence Challenge
        if (isSequenceChallenge && sequenceNextIdx < sequenceCubes.Count)
        {
            // Mit mistakeChance falsche Reihenfolge wählen
            int pickIdx = sequenceNextIdx;
            if (Random.value < sequenceMistakeChance && sequenceCubes.Count > 1)
            {
                // Zufälligen noch nicht transferierten Würfel nehmen
                List<int> available = new List<int>();
                for (int i = 0; i < sequenceCubes.Count; i++)
                    if (sequenceCubes[i] != null && !sequenceCubes[i].IsTransferred && i != sequenceNextIdx)
                        available.Add(i);
                if (available.Count > 0)
                    pickIdx = available[Random.Range(0, available.Count)];
            }

            SequenceCube sc = sequenceCubes[pickIdx];
            if (sc != null && !sc.IsTransferred && OrbSharedState.IsAvailable(sc.gameObject.GetInstanceID()))
            {
                targetCube = sc.gameObject;
                targetRb = targetCube.GetComponent<Rigidbody>();
                isStealingMalus = false;
                isCarryingFreeze = false;
                OrbSharedState.Lock(targetCube.GetInstanceID());
                sequenceNextIdx++;
                state = State.MovingToCube;
                Debug.Log($"[PlayerOrb] Sequence Würfel #{sc.sequenceNumber} aufnehmen.");
                return;
            }
        }

        // Priorität 3: ReactionCube im eigenen Feld
        GameObject reactionTarget = FindReactionCubeOnOwnSide();
        if (reactionTarget != null)
        {
            targetCube = reactionTarget;
            targetRb = targetCube.GetComponent<Rigidbody>();
            isStealingMalus = false;
            isCarryingFreeze = false;
            OrbSharedState.Lock(targetCube.GetInstanceID());
            Debug.Log($"[PlayerOrb] ReactionCube gefunden – höchste Priorität!");

            if (transform.position.x < partitionX)
            {
                returnTarget = new Vector3(targetCube.transform.position.x, flyHeight, targetCube.transform.position.z);
                liftTarget = new Vector3(transform.position.x, flyHeight, transform.position.z);
                crossTarget = returnTarget;
                state = State.ReturnLift;
            }
            else
                state = State.MovingToCube;
            return;
        }

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

        // ReactionCube informieren wer ihn trägt
        ReactionCube rc = targetCube.GetComponent<ReactionCube>();
        if (rc != null) rc.RegisterCarrier(false);

        Vector3 pos = transform.position;
        // Ablageposition bestimmen
        PegChallengeCube pegComp = targetCube.GetComponent<PegChallengeCube>();
        if (pegComp != null && isPegChallenge)
            dropTarget = FindMatchingZonePosition(pegComp.colorId);
        else if (isCarryingFreeze)
            dropTarget = GetFreezeZonePosition();
        else if (isStealingMalus)
            dropTarget = GetRandomOwnSidePosition();
        else
            dropTarget = GetRandomDropPosition();

        liftTarget = new Vector3(pos.x, flyHeight, pos.z);
        crossTarget = new Vector3(dropTarget.x, flyHeight, dropTarget.z);
        state = State.LiftUp;

        string actionLabel = pegComp != null ? "Peg " + pegComp.colorId : isStealingMalus ? "Klaue Malus" : "Transfer";
        Debug.Log($"[PlayerOrb] {actionLabel}: {targetCube.name} ? {dropTarget}");
    }

    private void Drop()
    {
        // Sicherheitscheck – Würfel könnte bereits zerstört sein (z.B. ReactionCube)
        if (targetCube == null)
        {
            targetRb = null;
            targetCube = null;
            StartCoroutine(ReactionPause());
            return;
        }

        // Peg Challenge: Zylinder senkrecht in Zone stecken und fixieren
        PegChallengeCube peg = targetCube.GetComponent<PegChallengeCube>();
        if (peg != null && isPegChallenge)
        {
            targetCube.transform.position = dropTarget;
            targetCube.transform.rotation = Quaternion.identity;
            if (targetRb != null)
            {
                targetRb.constraints = RigidbodyConstraints.None;
                targetRb.isKinematic = true;
                targetRb.useGravity = false;
                targetRb.velocity = Vector3.zero;
            }
            peg.IsPlaced = true;
            // Peg NICHT entsperren – bleibt gesperrt damit er nicht nochmal aufgenommen wird
            Debug.Log($"[PlayerOrb] Peg {peg.colorId} abgelegt und gesperrt.");
        }
        else
        {
            targetCube.transform.position = dropTarget;
            if (targetRb != null)
                targetRb.isKinematic = false;
            OrbSharedState.Unlock(targetCube.GetInstanceID());
        }

        isCarryingFreeze = false;
        isStealingMalus = false;

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
    private GameObject FindReactionCubeOnOwnSide()
    {
        GameObject nearest = null;
        float bestDist = float.MaxValue;
        GameObject[] cubes = null;

        try { cubes = GameObject.FindGameObjectsWithTag("Reaction"); }
        catch { return null; }

        if (cubes == null || cubes.Length == 0) return null;

        foreach (GameObject rc in cubes)
        {
            if (!rc.activeInHierarchy) continue;
            if (!OrbSharedState.IsAvailable(rc.GetInstanceID())) continue;
            if (rc.transform.position.x < partitionX) continue; // Player-Seite = rechts

            float d = Vector3.Distance(transform.position, rc.transform.position);
            if (d < bestDist) { bestDist = d; nearest = rc; }
        }
        return nearest;
    }

    private GameObject FindFreezeCubeOnOwnSide()
    {
        // FreezeCube auf beiden Seiten suchen – nächsten verfügbaren nehmen
        // (freezeZone wird nur für Ablage gebraucht, nicht für die Suche)
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
            Debug.Log($"[PlayerOrb] FreezeCube gefunden: {nearest.name}");

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

    public void Freeze(float seconds)
    {
        isActive = false;
        OrbSharedState.playerFrozen = true;

        if (targetCube != null && targetRb != null)
        {
            targetRb.isKinematic = false;
            OrbSharedState.Unlock(targetCube.GetInstanceID());
            targetCube = null;
            targetRb = null;
        }

        StopAllCoroutines();
        StartCoroutine(FreezeRoutine(seconds));
        Debug.Log($"[PlayerOrb] Eingefroren für {seconds}s.");
    }

    private System.Collections.IEnumerator FreezeRoutine(float seconds)
    {
        yield return new UnityEngine.WaitForSeconds(seconds);

        isActive = true;
        state = State.Idle;
        OrbSharedState.playerFrozen = false;
        Debug.Log($"[PlayerOrb] Freeze beendet.");
    }

    private GameObject FindNextUnplacedPeg()
    {
        foreach (GameObject peg in pendingPegs)
        {
            if (peg == null) continue;
            PegChallengeCube comp = peg.GetComponent<PegChallengeCube>();
            if (comp != null && !comp.IsPlaced && OrbSharedState.IsAvailable(peg.GetInstanceID()))
                return peg;
        }
        return null;
    }

    private Vector3 FindMatchingZonePosition(int colorId)
    {
        foreach (PegChallengeZone zone in FindObjectsOfType<PegChallengeZone>())
        {
            if (zone.colorId == colorId && !zone.IsOccupied)
            {
                // Exakte Zone-Mitte nehmen, leicht über dem Tisch
                Vector3 pos = zone.transform.position;
                pos.y = zone.transform.position.y + 0.02f;
                return pos;
            }
        }
        return pegZoneTargets.Count > 0 ? pegZoneTargets[0] : GetRandomDropPosition();
    }

    public void StartPegChallenge(List<GameObject> pegs, List<Vector3> zonePositions)
    {
        pendingPegs = new List<GameObject>(pegs);
        pegZoneTargets = new List<Vector3>(zonePositions);
        isPegChallenge = true;
        Debug.Log("[PlayerOrb] Peg Challenge gestartet!");
    }

    public void EndPegChallenge()
    {
        isPegChallenge = false;
        pendingPegs.Clear();
        pegZoneTargets.Clear();
        Debug.Log("[PlayerOrb] Peg Challenge beendet.");
    }

    public void StartSequenceChallenge(List<SequenceCube> cubes, float mistakeChance)
    {
        sequenceCubes = new List<SequenceCube>(cubes);
        sequenceMistakeChance = mistakeChance;
        sequenceNextIdx = 0;
        isSequenceChallenge = true;
        Debug.Log($"[PlayerOrb] Sequence Challenge gestartet! MistakeChance={mistakeChance}");
    }

    public void EndSequenceChallenge()
    {
        isSequenceChallenge = false;
        sequenceCubes.Clear();
        sequenceNextIdx = 0;
        Debug.Log($"[PlayerOrb] Sequence Challenge beendet.");
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