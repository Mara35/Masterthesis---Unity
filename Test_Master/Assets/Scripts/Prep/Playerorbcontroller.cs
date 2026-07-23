/*
 * Attach to:  PlayerOrb GameObject (test orb for the XBot side)
 *
 * Mirrored relative to the GhostOrbController:
 *   - Searches for cubes on the RIGHT side (positive X, XBot side)
 *   - Places them on the LEFT side (Ghost side)
 *
 * This script is replaced by the actual sensor control.
 * The game logic (cube detection by position) remains the same.
 *
 * Setup in the Inspector:
 *   - playerTargetZone -> Ghost's start zone (left side, drop zone)
 *   - cubeTag          -> Tag for all cubes (e.g., Block)
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LEGACY. The original player-controlled competition orb: auto-searches cubes on the XBot (right)
/// side, carries them over the partition to the ghost (left) side, and handles the freeze, peg and
/// sequence challenges. Replaced by the hand + GloveGrabber; kept for reference.
/// </summary>
public class PlayerOrbController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Scene Reference")]
    [Tooltip("Drop area on the ghost side (left side)")]
    public Transform playerTargetZone;

    [Tooltip("FreezeZone for own FreezeCubes (left for player, right for ghost)")]
    public Transform freezeZone;

    [Tooltip("The player's own drop side (StartZone - right side)")]
    public Transform playerOwnZone;

    [Tooltip("Tag of all cube GameObjects")]
    public string cubeTag = "Block";

    [Header("Movement")]
    [Range(0.1f, 1.0f)]
    public float speed = 1.0f;

    [Tooltip("How high the orb lifts above the partition top edge (meters)")]
    public float liftHeight = 0.15f;

    [Tooltip("Minimum Y height of the orb - set manually to the table surface")]
    public float minSafeY = 0.9f;

    [Tooltip("Radius to pick up a cube")]
    public float pickupRadius = 0.12f;

    [Tooltip("Radius to reach a waypoint")]
    public float waypointRadius = 0.05f;

    [Tooltip("Pause (s) before searching for the next cube")]
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
            Debug.LogWarning("[PlayerOrb] 'CenterPartition' not found!");
            partitionTopY = 1.0f;
            partitionX = 0f;
        }

        flyHeight = partitionTopY + liftHeight;

        isActive = true; // PlayerOrb starts immediately

    }

    private void Update()
    {
        if (!isActive) return;

        // Peg challenge interrupt: interrupt only once when the challenge starts
        if (isPegChallenge && state != State.Idle && targetCube != null)
        {
            PegChallengeCube currentPeg = targetCube.GetComponent<PegChallengeCube>();
            // Only interrupt if no peg is currently being carried
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
        // Highest priority: peg challenge
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

                // Move to the peg first (MovingToCube); dropTarget is set in PickUp
                state = State.MovingToCube;
                return;
            }
        }

        // Priority 2: sequence challenge
        if (isSequenceChallenge)
        {
            // Find the next cube by sequenceNumber (correct order)
            int nextNumber = sequenceNextIdx + 1; // sequenceNextIdx = how many have already been transferred
            SequenceCube correctCube = null;
            SequenceCube wrongCube = null;

            foreach (SequenceCube s in sequenceCubes)
            {
                if (s == null || s.IsTransferred) continue;
                if (s.sequenceNumber == nextNumber) correctCube = s;
                else if (wrongCube == null) wrongCube = s;
            }

            // Make a mistake?
            SequenceCube toPickup = null;
            if (Random.value < sequenceMistakeChance && wrongCube != null)
                toPickup = wrongCube;   // intentionally wrong one
            else if (correctCube != null)
                toPickup = correctCube; // correct one
            else
                toPickup = wrongCube;   // no correct one left

            if (toPickup != null)
            {
                int pickIdx = sequenceCubes.IndexOf(toPickup);

                SequenceCube sc = pickIdx >= 0 ? sequenceCubes[pickIdx] : null;
                if (sc != null && !sc.IsTransferred && OrbSharedState.IsAvailable(sc.gameObject.GetInstanceID())
                    && !sc.SpawnedOnGhostSide()) // own side only
                {
                    targetCube = sc.gameObject;
                    targetRb = targetCube.GetComponent<Rigidbody>();
                    isStealingMalus = false;
                    isCarryingFreeze = false;
                    OrbSharedState.Lock(targetCube.GetInstanceID());
                    sequenceNextIdx++;
                    state = State.MovingToCube;
                    Debug.Log($"[PlayerOrb] Pick up sequence cube #{sc.sequenceNumber} (mistakeChance={sequenceMistakeChance}).");
                    return;
                }
            } // end if toPickup
        }

        // Priority 3: ReactionCube on own field
        GameObject reactionTarget = FindReactionCubeOnOwnSide();
        if (reactionTarget != null)
        {
            targetCube = reactionTarget;
            targetRb = targetCube.GetComponent<Rigidbody>();
            isStealingMalus = false;
            isCarryingFreeze = false;
            OrbSharedState.Lock(targetCube.GetInstanceID());
            Debug.Log($"[PlayerOrb] ReactionCube found - highest priority!");

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

        // 50% chance: try to steal a red cube from the opponent
        GameObject stealTarget = null;
        if (freezeTarget == null && Random.value < 0.5f)
            stealTarget = FindMalusCubeOnEnemySide();

        targetCube = freezeTarget ?? stealTarget ?? FindNearestCubeOnMySide();

        // Fallback: ignore cooldown if no cube found (prevents a standstill)
        if (targetCube == null)
            targetCube = FindNearestCubeOnMySide(ignoreCooldown: true);

        if (targetCube == null) return;

        isStealingMalus = (stealTarget != null);
        isCarryingFreeze = (freezeTarget != null);
        targetRb = targetCube.GetComponent<Rigidbody>();

        // Lock immediately so no other orb picks the same cube
        OrbSharedState.Lock(targetCube.GetInstanceID());

        // Orb on the wrong side -> fly back first
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

    // Finds a malus cube on the OPPONENT's side (left = ghost side)
    private GameObject FindMalusCubeOnEnemySide()
    {
        GameObject nearest = null;
        float bestDist = float.MaxValue;

        foreach (BonusCube bc in FindObjectsOfType<BonusCube>())
        {
            if (bc.pointValue > 0) continue; // negative (red) cubes only
            if (!bc.gameObject.activeInHierarchy) continue;
            if (!OrbSharedState.IsAvailable(bc.gameObject.GetInstanceID())) continue;

            // Opponent side = left of the partition (player is on the right)
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
    // Movement / carrying the cube
    // -----------------------------------------------------------------------

    private void MoveTowards(Vector3 target)
    {
        Vector3 next = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        next.y = Mathf.Max(next.y, minSafeY); // never below the table surface
        transform.position = next;
    }

    private void CarryCube()
    {
        if (targetCube != null)
            // hold the cube slightly above the orb center so it doesn't clip through geometry
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

        // tell the ReactionCube who is carrying it
        ReactionCube rc = targetCube.GetComponent<ReactionCube>();
        if (rc != null) rc.RegisterCarrier(false);

        Vector3 pos = transform.position;
        // determine the drop position
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

        string actionLabel = pegComp != null ? "Peg " + pegComp.colorId : isStealingMalus ? "Steal malus" : "Transfer";
        Debug.Log($"[PlayerOrb] {actionLabel}: {targetCube.name} -> {dropTarget}");
    }

    private void Drop()
    {
        // Safety check - the cube may already be destroyed (e.g. ReactionCube)
        if (targetCube == null)
        {
            targetRb = null;
            targetCube = null;
            StartCoroutine(ReactionPause());
            return;
        }

        // Peg challenge: place the cylinder upright into the zone and fix it
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
            // Do NOT unlock the peg - it stays locked so it isn't picked up again
            Debug.Log($"[PlayerOrb] Peg {peg.colorId} placed and locked.");
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

        Debug.Log($"[PlayerOrb] Dropped: {targetCube.name} at {dropTarget}");

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
    // Cube search by POSITION - right side (positive X)
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

            // Only cubes on the right (XBot) side
            if (cube.transform.position.x <= partitionX) continue;

            // Skip the cube currently being carried
            if (cube == targetCube) continue;
            // Skip locked or recently dropped cubes (shared state)
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

    // Random position on the own (player) side - for stolen red cubes
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
            if (rc.transform.position.x < partitionX) continue; // player side = right

            float d = Vector3.Distance(transform.position, rc.transform.position);
            if (d < bestDist) { bestDist = d; nearest = rc; }
        }
        return nearest;
    }

    private GameObject FindFreezeCubeOnOwnSide()
    {
        // Search FreezeCubes on both sides - take the nearest available one
        // (freezeZone is only needed for dropping, not for the search)
        GameObject nearest = null;
        float bestDist = float.MaxValue;
        GameObject[] freezeCubes = null;

        try { freezeCubes = GameObject.FindGameObjectsWithTag("Freeze"); }
        catch { Debug.LogWarning("[Orb] Tag 'Freeze' not registered!"); return null; }

        if (freezeCubes == null || freezeCubes.Length == 0) return null;

        foreach (GameObject fc in freezeCubes)
        {
            if (!fc.activeInHierarchy) continue;
            if (!OrbSharedState.IsAvailable(fc.GetInstanceID())) continue;
            float d = Vector3.Distance(transform.position, fc.transform.position);
            if (d < bestDist) { bestDist = d; nearest = fc; }
        }

        if (nearest != null)
            Debug.Log($"[PlayerOrb] FreezeCube found: {nearest.name}");

        return nearest;
    }

    private Vector3 GetFreezeZonePosition()
    {
        if (freezeZone == null)
        {
            Debug.LogWarning("[Orb] freezeZone not assigned! Please set it in the Inspector.");
            return transform.position;
        }

        Debug.Log($"[Orb] Dropping FreezeCube in {freezeZone.name}.");

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
        // Fallback: right of the partition
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

                // Inset: subtract 20% of the size from each side
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
        Debug.Log($"[PlayerOrb] Frozen for {seconds}s.");
    }

    private System.Collections.IEnumerator FreezeRoutine(float seconds)
    {
        yield return new UnityEngine.WaitForSeconds(seconds);

        isActive = true;
        state = State.Idle;
        OrbSharedState.playerFrozen = false;
        Debug.Log($"[PlayerOrb] Freeze ended.");
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
                // Take the exact zone center, slightly above the table
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
        Debug.Log("[PlayerOrb] Peg challenge started!");
    }

    public void EndPegChallenge()
    {
        isPegChallenge = false;
        pendingPegs.Clear();
        pegZoneTargets.Clear();
        Debug.Log("[PlayerOrb] Peg challenge ended.");
    }

    public void StartSequenceChallenge(List<SequenceCube> cubes, float mistakeChance)
    {
        sequenceCubes = new List<SequenceCube>(cubes);
        sequenceMistakeChance = mistakeChance;
        sequenceNextIdx = 0;
        isSequenceChallenge = true;
        Debug.Log($"[PlayerOrb] Sequence challenge started! MistakeChance={mistakeChance}");
    }

    public void EndSequenceChallenge()
    {
        isSequenceChallenge = false;
        sequenceCubes.Clear();
        sequenceNextIdx = 0;
        Debug.Log($"[PlayerOrb] Sequence challenge ended.");
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