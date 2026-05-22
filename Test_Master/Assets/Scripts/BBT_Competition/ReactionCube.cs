/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       ReactionCube.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Attach to:  ReactionCube Prefab
 *
 * Erfolg = Aufheben innerhalb von timeLimit
 * Würfel bleibt 1s nach Ablegen liegen, dann Destroy
 * Bonuspunkte werden dem richtigen Spieler zugeordnet
 */

using System.Collections;
using UnityEngine;

public class ReactionCube : MonoBehaviour
{
    [Header("Einstellungen")]
    public float timeLimit = 3f;
    public float lingerDuration = 1f;  // wie lange nach Ablegen liegen bleibt
    public int bonusSuccess = 2;   // Score sinkt um diesen Wert (gut)
    public int bonusFail = 2;   // Score steigt um diesen Wert (schlecht)

    [Header("Visuell")]
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

    // Wer hat aufgenommen? (wird in PickUp gesetzt)
    private bool pickedUpByGhost = false;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        timeRemaining = timeLimit;
        cubeRenderer = GetComponent<Renderer>();

        GameObject cp = GameObject.Find("CenterPartition");
        partitionX = cp != null ? cp.transform.position.x : 0f;

        if (progressBarGO != null)
            barOriginalScale = progressBarGO.transform.localScale;

        StartCoroutine(CountdownRoutine());
        StartCoroutine(BlinkRoutine());
    }

    private void Update()
    {
        if (isPickedUp || isExpired) return;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && rb.isKinematic && carrierRegistered)
            OnPickedUp();
        else if (rb != null && rb.isKinematic && !carrierRegistered)
        {
            // Sicherheits-Fallback: Carrier per Position bestimmen
            GhostOrbController ghost = FindObjectOfType<GhostOrbController>();
            if (ghost != null && Vector3.Distance(ghost.transform.position, transform.position) < 0.3f)
                RegisterCarrier(true);
            else
                RegisterCarrier(false);
        }
    }

    // Wird vom Orb direkt aufgerufen wenn er den Würfel aufnimmt
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

    private void OnPickedUp()
    {
        if (isPickedUp || isExpired) return;
        isPickedUp = true;

        // Countdown und Blinken stoppen
        StopAllCoroutines();
        if (progressBarGO != null) progressBarGO.SetActive(false);
        if (cubeRenderer != null) cubeRenderer.enabled = true;

        // Bonuspunkte dem richtigen Spieler zuordnen
        // bonusPoints wird am Ende VOM Score abgezogen ? positiv = Score sinkt (gut)
        if (pickedUpByGhost)
        {
            CompetitionGameManager.ghostBonusPoints += bonusSuccess;
            Debug.Log($"[ReactionCube] Ghost erfolgreich! Ghost +{bonusSuccess} Bonus.");
        }
        else
        {
            CompetitionGameManager.playerBonusPoints += bonusSuccess;
            Debug.Log($"[ReactionCube] Player erfolgreich! Player +{bonusSuccess} Bonus.");
        }

        // 1s nach Ablegen warten dann zerstören
        StartCoroutine(LingerAndDestroy());
    }

    private IEnumerator LingerAndDestroy()
    {
        // Warten bis der Würfel abgelegt wird (isKinematic wird false)
        Rigidbody rb = GetComponent<Rigidbody>();
        while (rb != null && rb.isKinematic)
            yield return null;

        // 1s liegen lassen
        yield return new WaitForSeconds(lingerDuration);

        Destroy(gameObject);
    }

    private void OnExpired()
    {
        if (isPickedUp || isExpired) return;
        isExpired = true;

        StopAllCoroutines();

        // Strafpunkte für beide Seiten (keiner hat rechtzeitig reagiert)
        // Nur dem Spieler auf dessen Seite er lag
        bool wasOnGhostSide = transform.position.x < partitionX;
        if (wasOnGhostSide)
        {
            // Misserfolg: Score steigt ? bonusPoints sinkt (wird weniger abgezogen)
            CompetitionGameManager.ghostBonusPoints -= bonusFail;
            Debug.Log($"[ReactionCube] Ghost zu langsam! Ghost +{bonusFail} Strafpunkte.");
        }
        else
        {
            CompetitionGameManager.playerBonusPoints -= bonusFail;
            Debug.Log($"[ReactionCube] Player zu langsam! Player +{bonusFail} Strafpunkte.");
        }

        if (cubeRenderer != null)
            cubeRenderer.material.color = Color.red;

        Destroy(gameObject, 0.5f);
    }
}