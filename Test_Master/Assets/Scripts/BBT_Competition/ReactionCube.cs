using System.Collections;
using UnityEngine;

/// <summary>
/// A cube that must be picked up within timeLimit. Success awards +bonusSuccess, failure -bonusFail;
/// shows a countdown progress bar and lingers briefly before destroying itself. RegisterCarrier records
/// who grabbed it (player vs ghost).
/// </summary>
public class ReactionCube : MonoBehaviour
{
    [Header("Settings")]
    public float timeLimit = 3f;
    public float lingerDuration = 1f;

    [Tooltip("Bonus points for success (positive)")]
    public int bonusSuccess = 2;

    [Tooltip("Bonus points for failure (negative)")]
    public int bonusFail = 2;

    [Header("Visually")]
    public GameObject progressBarGO;

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private float timeRemaining;
    private bool isPickedUp = false;
    private bool isExpired = false;
    private Renderer cubeRenderer;
    private Vector3 barOriginalScale;
    private float partitionX;
    private bool spawnedOnGhostSide; // saved upon spawn

    // Who picked it up? (set in PickUp)
    private bool pickedUpByGhost = false;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        // Ensure that bonusFail is always positive
        bonusFail = Mathf.Abs(bonusFail);

        timeRemaining = timeLimit;
        cubeRenderer = GetComponent<Renderer>();

        GameObject cp = GameObject.Find("CenterPartition");
        partitionX = cp != null ? cp.transform.position.x : 0f;
        spawnedOnGhostSide = transform.position.x < partitionX; 

        if (progressBarGO != null)
            barOriginalScale = progressBarGO.transform.localScale;

        //Block a second reaction cube from spawning on the same side while this one is live.
        if (transform.position.x < partitionX)
            OrbSharedState.ghostSideHasReaction = true;
        else
            OrbSharedState.playerSideHasReaction = true;

        StartCoroutine(CountdownRoutine());
        StartCoroutine(BlinkRoutine());
    }

    private void Update()
    {
        if (isPickedUp || isExpired) return;

        // A cube is "picked up" when its Rigidbody was switched to kinematic by an orb/grabber.
        // carrierRegistered tells us who grabbed it; if nobody registered, fall back to distance.
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && rb.isKinematic && carrierRegistered)
            OnPickedUp();
        else if (rb != null && rb.isKinematic && !carrierRegistered)
        {
            // Safety fallback: Determine carrier by position
            GhostOrbController ghost = FindObjectOfType<GhostOrbController>();
            if (ghost != null && Vector3.Distance(ghost.transform.position, transform.position) < 0.3f)
                RegisterCarrier(true);
            else
                RegisterCarrier(false);
        }
    }

    // Is called directly by the Orb when it picks up the cube
    private bool carrierRegistered = false;

    public void RegisterCarrier(bool isGhost)
    {
        pickedUpByGhost = isGhost;
        carrierRegistered = true;
    }

    // -----------------------------------------------------------------------
    // Countdown
    // -----------------------------------------------------------------------

    private IEnumerator CountdownRoutine()
    {
        while (timeRemaining > 0f && !isPickedUp)
        {
            timeRemaining -= Time.deltaTime;

            if (progressBarGO != null)
            {
                float t = Mathf.Clamp01(timeRemaining / timeLimit);
                Vector3 newScale = barOriginalScale;
                newScale.x = barOriginalScale.x * t;
                progressBarGO.transform.localScale = newScale;

                Renderer barRenderer = progressBarGO.GetComponent<Renderer>();
                if (barRenderer != null)
                    barRenderer.material.color = Color.Lerp(Color.red, Color.green, t);
            }

            yield return null;
        }

        if (!isPickedUp)
            OnExpired();
    }

    private IEnumerator BlinkRoutine()
    {
        while (!isPickedUp && !isExpired)
        {
            float t = Mathf.Clamp01(timeRemaining / timeLimit);
            float blinkRate = Mathf.Lerp(0.1f, 0.4f, t);

            if (cubeRenderer != null)
                cubeRenderer.enabled = !cubeRenderer.enabled;

            yield return new WaitForSeconds(blinkRate);
        }

        if (cubeRenderer != null)
            cubeRenderer.enabled = true;
    }

    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------

    private void ClearReactionFlag()
    {
        if (spawnedOnGhostSide)
            OrbSharedState.ghostSideHasReaction = false;
        else
            OrbSharedState.playerSideHasReaction = false;
    }

    private void OnPickedUp()
    {
        if (isPickedUp || isExpired) return;
        isPickedUp = true;

        // Stop the countdown and flashing
        StopAllCoroutines();
        if (progressBarGO != null) progressBarGO.SetActive(false);
        if (cubeRenderer != null) cubeRenderer.enabled = true;
        ClearReactionFlag();

        // Success: +bonusSuccess 
        if (pickedUpByGhost)
        {
            CompetitionGameManager.ghostBonusPoints += bonusSuccess;
            Debug.Log($"[ReactionCube] Ghost successful! Bonus Points Ghost = {CompetitionGameManager.ghostBonusPoints}");
        }
        else
        {
            CompetitionGameManager.playerBonusPoints += bonusSuccess;
            Debug.Log($"[ReactionCube] Player successful! BonusPoints Player = {CompetitionGameManager.playerBonusPoints}");
        }

        // Wait 1s after release, then destroy
        StartCoroutine(LingerAndDestroy());
    }

    private IEnumerator LingerAndDestroy()
    {
        // Wait until the cube is placed (isKinematic becomes false)
        Rigidbody rb = GetComponent<Rigidbody>();
        while (rb != null && rb.isKinematic)
            yield return null;

        // Wait 1s
        yield return new WaitForSeconds(lingerDuration);

        Destroy(gameObject);
    }

    private void OnExpired()
    {
        if (isPickedUp || isExpired) return;
        isExpired = true;

        StopAllCoroutines();
        ClearReactionFlag();


        // Penalty point only to the player on whose side the cube was
        bool wasOnGhostSide = spawnedOnGhostSide;
        if (wasOnGhostSide)
        {
            // Failure: -bonusFail 
            CompetitionGameManager.ghostBonusPoints -= bonusFail;
            Debug.Log($"[ReactionCube] Ghost too slow! BonusPoints Ghost = {CompetitionGameManager.ghostBonusPoints}");
        }
        else
        {
            CompetitionGameManager.playerBonusPoints -= bonusFail;
            Debug.Log($"[ReactionCube] Player too slow! BonusPoints Player = {CompetitionGameManager.playerBonusPoints}");
        }

        if (cubeRenderer != null)
            cubeRenderer.material.color = Color.red;

        Destroy(gameObject, 0.5f);
    }
}