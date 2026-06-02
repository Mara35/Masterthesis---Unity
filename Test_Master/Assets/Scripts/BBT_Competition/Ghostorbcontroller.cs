/*
 * summary:
 * 
 * Attach to:  GhostOrb GameObject
 *
 * Searches for cubes by POSITION (left side of the partition) – not by parent hierarchy.
 * This also detects cubes that have been moved over by the PlayerOrb.
 *
 * Setup in the Inspector:
 *   - ghostTargetZone  ? XBot's StartZone (right side, drop zone)
 *   - cubeTag          ? Tag for all cubes (e.g., “Block”), or leave blank for name search
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GhostOrbController : MonoBehaviour
{
    // -----------------------------------------------------------------------
    // Inspector
    // -----------------------------------------------------------------------

    [Header("Scene-Reference")]
    [Tooltip("Drop-off area on the XBot side (XBot's StartZone)")]
    public Transform ghostTargetZone;

    [Tooltip("FreezeZone for your own FreezeCubes (left for Player, right for Ghost)")]
    public Transform freezeZone;

    [Tooltip("The Ghost's dedicated drop-off side (TargetZone – left side)")]
    public Transform ghostOwnZone;

    [Tooltip("Tag of all Cube-GameObjects")]
    public string cubeTag = "Block";

    [Header("Movement")]
    [Range(0.1f, 1.0f)]
    public float speed = 1.0f;

    [Tooltip("How high the orb rises above the top edge of the partition")]
    public float liftHeight = 0.15f;

    [Tooltip("Minimal Y-Hight of orb")]
    public float minSafeY = 0.9f;

    [Tooltip("Radius to pick up a cube")]
    public float pickupRadius = 0.12f;

    [Tooltip("Radius required to reach a waypoint")]
    public float waypointRadius = 0.05f;

    [Tooltip("Pause(s) before the next cube is selected")]
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
    private bool isSequenceChallenge = false;
    private List<SequenceCube> sequenceCubes = new List<SequenceCube>();
    private float sequenceMistakeChance = 0.3f;
    private int sequenceNextIdx = 0;



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

            // Collect all renderers in Children and find the highest point
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
                // Fallback: Find the table surface using BoxCollider
                BoxCollider bc = cp.GetComponentInChildren<BoxCollider>();
                if (bc != null)
                    partitionTopY = bc.bounds.max.y;
                else
                    partitionTopY = cp.transform.position.y + 0.05f;
            }
        }
        else
        {
            // CenterPartition not found: Estimate table surface using all renderers
            Debug.LogWarning("[Orb] 'CenterPartition' not found – looking for table surface.");
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
                partitionTopY = 0.8f; // last Fallback
            }
            partitionX = 0f;
        }

        flyHeight = partitionTopY + liftHeight;

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
        // FreezeCube is top priority
        // Priority 2: Sequence Challenge
        if (isSequenceChallenge)
        {
            // Find the next cube based on sequenceNumber (in the correct order)
            int nextNumber = sequenceNextIdx + 1; // sequenceNextIdx = how many have already been transferred
            SequenceCube correctCube = null;
            SequenceCube wrongCube = null;

            foreach (SequenceCube s in sequenceCubes)
            {
                if (s == null || s.IsTransferred) continue;
                if (s.sequenceNumber == nextNumber) correctCube = s;
                else if (wrongCube == null) wrongCube = s;
            }

            // Making mistakes?
            SequenceCube toPickup = null;
            if (Random.value < sequenceMistakeChance && wrongCube != null)
                toPickup = wrongCube;   // intentionally incorrect
            else if (correctCube != null)
                toPickup = correctCube; // correct
            else
                toPickup = wrongCube;   //  No more proper ones available

            if (toPickup != null)
            {
                int pickIdx = sequenceCubes.IndexOf(toPickup);

                SequenceCube sc = pickIdx >= 0 ? sequenceCubes[pickIdx] : null;
                if (sc != null && !sc.IsTransferred && OrbSharedState.IsAvailable(sc.gameObject.GetInstanceID())
                    && sc.SpawnedOnGhostSide()) // only own side
                {
                    targetCube = sc.gameObject;
                    targetRb = targetCube.GetComponent<Rigidbody>();
                    isStealingMalus = false;
                    isCarryingFreeze = false;
                    OrbSharedState.Lock(targetCube.GetInstanceID());
                    sequenceNextIdx++;
                    state = State.MovingToCube;
                    Debug.Log($"[GhostOrb] Sequence cubes #{sc.sequenceNumber} pick up (mistakeChance={sequenceMistakeChance}).");
                    return;
                }
            } // end if toPickup
        }

        // Priority 3: ReactionCube in its own field
        GameObject reactionTarget = FindReactionCubeOnOwnSide();
        if (reactionTarget != null)
        {
            targetCube = reactionTarget;
            targetRb = targetCube.GetComponent<Rigidbody>();
            isStealingMalus = false;
            isCarryingFreeze = false;
            OrbSharedState.Lock(targetCube.GetInstanceID());
            Debug.Log($"[GhostOrb] ReactionCube found – highest Priority!");

            if (transform.position.x > partitionX)
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

        // 50% Chance: Try to steal the green cube from the opponent's side
        GameObject stealTarget = null;
        if (freezeTarget == null && Random.value < 0.5f)
            stealTarget = FindMalusCubeOnEnemySide();

        targetCube = freezeTarget ?? stealTarget ?? FindNearestCubeOnMySide();

        // Fallback: Ignore cooldown if no cube is found (prevents stalling)
        if (targetCube == null)
            targetCube = FindNearestCubeOnMySide(ignoreCooldown: true);

        if (targetCube == null) return;

        isStealingMalus = (stealTarget != null);
        isCarryingFreeze = (freezeTarget != null);
        targetRb = targetCube.GetComponent<Rigidbody>();

        //  Block immediately so opponents don't choose the same cube
        OrbSharedState.Lock(targetCube.GetInstanceID());

        // Orb on the wrong side? Fly back first
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

    // Look for a bonus cube (BonusCube with a negative pointValue) on the OPPONENT'S side
    private GameObject FindMalusCubeOnEnemySide()
    {
        GameObject nearest = null;
        float bestDist = float.MaxValue;

        foreach (BonusCube bc in FindObjectsOfType<BonusCube>())
        {
            if (bc.pointValue > 0) continue; // only negative (green) Cubes
            if (!bc.gameObject.activeInHierarchy) continue;
            if (!OrbSharedState.IsAvailable(bc.gameObject.GetInstanceID())) continue;

            // Opposite side = right side of the partition (Ghost is on the left)
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
    // Movement / Carrying the cubes
    // -----------------------------------------------------------------------

    private void MoveTowards(Vector3 target)
    {
        Vector3 next = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        next.y = Mathf.Max(next.y, minSafeY); // never below the tabletop
        transform.position = next;
    }

    private void CarryCube()
    {
        if (targetCube != null)
            // // Hold the cube slightly above the orb's center so it doesn't pass through the geometry
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

        // inform ReactionCube who is carrying it (for correct behavior and visuals)
        ReactionCube rc = targetCube.GetComponent<ReactionCube>();
        if (rc != null) rc.RegisterCarrier(true);

        Vector3 pos = transform.position;
        // Determine the drop-off location
        if (isCarryingFreeze)
            dropTarget = GetFreezeZonePosition();
        else if (isStealingMalus)
            dropTarget = GetRandomOwnSidePosition();
        else
            dropTarget = GetRandomDropPosition();
        liftTarget = new Vector3(pos.x, flyHeight, pos.z);
        crossTarget = new Vector3(dropTarget.x, flyHeight, dropTarget.z);
        state = State.LiftUp;

        Debug.Log($"[GhostOrb] {(isStealingMalus ? "Stealing Malus" : "Transfer")}: {targetCube.name}  Target: {dropTarget}");
    }

    private void Drop()
    {
        // Safety check – the cube may already be destroyed (e.g., ReactionCube)
        if (targetCube == null)
        {
            targetRb = null;
            targetCube = null;
            StartCoroutine(ReactionPause());
            return;
        }

        targetCube.transform.position = dropTarget;

        if (targetRb != null)
            targetRb.isKinematic = false;

        OrbSharedState.Unlock(targetCube.GetInstanceID());
        isCarryingFreeze = false;
        isStealingMalus = false;

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
    // Find cubes by POSITION (not by parent)
    // Finds all cubes to the left of the partition—regardless of their parent
    // -----------------------------------------------------------------------

    private GameObject FindNearestCubeOnMySide(bool ignoreCooldown = false)
    {
        GameObject nearest = null;
        float bestDist = float.MaxValue;

        // Search for all cubes with tag
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

            // Only dice on the left (ghost) side
            if (cube.transform.position.x >= partitionX) continue;

            // Skip the cube currently being carried
            if (cube == targetCube) continue;
            // Skip locked or recently discarded dice (shared state)
            if (ignoreCooldown ? !OrbSharedState.IsAvailableIgnoreCooldown(cube.GetInstanceID()) : !OrbSharedState.IsAvailable(cube.GetInstanceID())) continue;

            float d = Vector3.Distance(transform.position, cube.transform.position);
            if (d < bestDist) { bestDist = d; nearest = cube; }
        }

        return nearest;
    }

    // Fallback if no tag is set: searches for all GameObjects that start with “Block”
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

    // Random position on your own (ghost) side – for stolen green cubes
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
            if (rc.transform.position.x > partitionX) continue; // Ghost-Side = left

            float d = Vector3.Distance(transform.position, rc.transform.position);
            if (d < bestDist) { bestDist = d; nearest = rc; }
        }
        return nearest;
    }

    private GameObject FindFreezeCubeOnOwnSide()
    {
        // Search for FreezeCube on both sides – take the next available one
        // (freezeZone is only used for storage, not for searching)
        GameObject nearest = null;
        float bestDist = float.MaxValue;
        GameObject[] freezeCubes = null;

        try { freezeCubes = GameObject.FindGameObjectsWithTag("Freeze"); }
        catch { Debug.LogWarning("[Orb] The ‘Freeze’ tag is not registered!"); return null; }

        if (freezeCubes == null || freezeCubes.Length == 0) return null;

        foreach (GameObject fc in freezeCubes)
        {
            if (!fc.activeInHierarchy) continue;
            if (!OrbSharedState.IsAvailable(fc.GetInstanceID())) continue;
            float d = Vector3.Distance(transform.position, fc.transform.position);
            if (d < bestDist) { bestDist = d; nearest = fc; }
        }

        if (nearest != null)
            Debug.Log($"[GhostOrb] FreezeCube found: {nearest.name}");

        return nearest;
    }

    private Vector3 GetFreezeZonePosition()
    {
        if (freezeZone == null)
        {
            Debug.LogWarning("[Orb] freezeZone not assigned! Please set it in the Inspector.");
            return transform.position;
        }

        Debug.Log($"[Orb] Place FreezeCube in {freezeZone.name}.");

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
        // Fallback: to the left of the partition
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

                // Inset: Subtract 20% from the size of each side
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
        isActive = false;
        OrbSharedState.ghostFrozen = true;

        if (targetCube != null && targetRb != null)
        {
            targetRb.isKinematic = false;
            OrbSharedState.Unlock(targetCube.GetInstanceID());
            targetCube = null;
            targetRb = null;
        }

        StopAllCoroutines();
        StartCoroutine(FreezeRoutine(seconds));
        Debug.Log($"[GhostOrb] freezed for {seconds}s.");
    }

    private System.Collections.IEnumerator FreezeRoutine(float seconds)
    {
        yield return new UnityEngine.WaitForSeconds(seconds);

        isActive = true;
        state = State.Idle;
        OrbSharedState.ghostFrozen = false;
        Debug.Log($"[GhostOrb] freeze ended.");
    }

    public void StartSequenceChallenge(List<SequenceCube> cubes, float mistakeChance)
    {
        sequenceCubes = new List<SequenceCube>(cubes);
        sequenceMistakeChance = mistakeChance;
        sequenceNextIdx = 0;
        isSequenceChallenge = true;
        Debug.Log($"[GhostOrb]  Sequence Challenge has begun! MistakeChance={mistakeChance}");
    }

    public void EndSequenceChallenge()
    {
        isSequenceChallenge = false;
        sequenceCubes.Clear();
        sequenceNextIdx = 0;
        Debug.Log($"[GhostOrb] Sequence Challenge ended.");
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