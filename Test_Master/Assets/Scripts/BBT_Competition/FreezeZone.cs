/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       FreezeZone.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Attach to:  FreezeZone_Left  ? friert GhostOrb ein
 *             FreezeZone_Right ? friert PlayerOrb ein
 *
 * Setup im Inspector:
 *   - targetToFreeze ? GhostOrbController ODER PlayerOrbController
 *   - freezeDuration ? Sekunden (default 5)
 */

using System.Collections;
using UnityEngine;

public class FreezeZone : MonoBehaviour
{
    [Header("Ziel")]
    [Tooltip("Der Orb der eingefroren wird wenn ein FreezeCube die Zone betritt")]
    public MonoBehaviour targetToFreeze; // GhostOrbController oder PlayerOrbController

    [Header("Einstellungen")]
    public float freezeDuration = 5f;

    [Tooltip("Visuelles Feedback – z.B. blaues Leuchten auf der Zone (optional)")]
    public Renderer zoneRenderer;
    public Color activeColor = new Color(0.2f, 0.5f, 1f, 0.5f);
    public Color inactiveColor = new Color(0.2f, 0.5f, 1f, 0.15f);

    private bool isFrozen = false;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        var col = GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = true;

        UpdateVisual(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Freeze")) return;

        Debug.Log($"[FreezeZone] FreezeCube erkannt – friere {targetToFreeze?.name} ein.");

        // Würfel sofort zerstören
        Destroy(other.gameObject);

        // Gegner einfrieren
        StartCoroutine(FreezeTarget());
    }

    // -----------------------------------------------------------------------
    // Freeze Coroutine
    // -----------------------------------------------------------------------

    private IEnumerator FreezeTarget()
    {
        if (targetToFreeze == null) yield break;
        if (isFrozen) yield break; // nicht stapeln

        isFrozen = true;
        UpdateVisual(true);

        // Freeze aufrufen – funktioniert für beide Controller
        GhostOrbController ghost = targetToFreeze as GhostOrbController;
        PlayerOrbController player = targetToFreeze as PlayerOrbController;

        if (ghost != null) ghost.Freeze(freezeDuration);
        if (player != null) player.Freeze(freezeDuration);

        yield return new WaitForSeconds(freezeDuration);

        isFrozen = false;
        UpdateVisual(false);
    }

    // -----------------------------------------------------------------------
    // Visuelles Feedback
    // -----------------------------------------------------------------------

    private void UpdateVisual(bool frozen)
    {
        if (zoneRenderer == null) return;
        zoneRenderer.material.color = frozen ? activeColor : inactiveColor;
    }
}