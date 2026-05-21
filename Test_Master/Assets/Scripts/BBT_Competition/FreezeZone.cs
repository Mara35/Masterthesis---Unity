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
        if (isFrozen) return;

        Debug.Log($"[FreezeZone] FreezeCube erkannt – friere {targetToFreeze?.name} ein.");

        // Würfel in Zone fixieren
        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        other.transform.position = transform.position + Vector3.up * 0.05f;

        // Gegner einfrieren und Würfel nach Freeze-Duration zerstören
        StartCoroutine(FreezeTarget(other.gameObject));
    }

    // -----------------------------------------------------------------------
    // Freeze Coroutine
    // -----------------------------------------------------------------------

    private IEnumerator FreezeTarget(GameObject freezeCube)
    {
        if (targetToFreeze == null)
        {
            Debug.LogWarning("[FreezeZone] targetToFreeze ist nicht zugewiesen! Bitte im Inspector setzen.");
            if (freezeCube != null) Destroy(freezeCube);
            yield break;
        }

        isFrozen = true;
        UpdateVisual(true);
        Debug.Log($"[FreezeZone] Friere {targetToFreeze.name} für {freezeDuration}s ein.");

        GhostOrbController ghost = targetToFreeze as GhostOrbController;
        PlayerOrbController player = targetToFreeze as PlayerOrbController;

        if (ghost != null) ghost.Freeze(freezeDuration);
        if (player != null) player.Freeze(freezeDuration);

        if (ghost == null && player == null)
            Debug.LogWarning("[FreezeZone] targetToFreeze ist weder GhostOrbController noch PlayerOrbController!");

        // Würfel bleibt für die gesamte Freeze-Duration sichtbar in der Zone
        yield return new WaitForSeconds(freezeDuration);

        // Würfel entsperren und zerstören
        if (freezeCube != null)
        {
            OrbSharedState.Unlock(freezeCube.GetInstanceID());
            Destroy(freezeCube);
        }
        isFrozen = false;
        UpdateVisual(false);

        Debug.Log("[FreezeZone] Freeze beendet – FreezeCube entfernt.");
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