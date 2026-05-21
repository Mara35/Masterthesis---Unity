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
 * Erstellt automatisch einen 3D-Countdown-Text über der Zone.
 * Kein manuelles TextMeshPro Setup nötig.
 */

using System.Collections;
using UnityEngine;

public class FreezeZone : MonoBehaviour
{
    [Header("Ziel")]
    public MonoBehaviour targetToFreeze;

    [Header("Einstellungen")]
    public float freezeDuration = 5f;

    [Header("Visuelles Feedback")]
    public Renderer zoneRenderer;
    public Color activeColor = new Color(0.2f, 0.5f, 1f, 0.5f);
    public Color inactiveColor = new Color(0.2f, 0.5f, 1f, 0.15f);

    [Header("Countdown Text")]
    public float textHeight = 0.3f;  // Höhe über der Zone
    public float textSize = 0.2f;  // Schriftgröße
    public Color textColor = new Color(0f, 0.1f, 0.5f); // Dunkelblau

    private bool isFrozen = false;
    private TextMesh countdownMesh;
    private Camera mainCam;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        mainCam = Camera.main;

        var col = GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = true;

        UpdateVisual(false);
        CreateCountdownText();
    }

    private void LateUpdate()
    {
        // Text immer zur Kamera drehen – Kamera-Rotation direkt übernehmen
        if (countdownMesh != null && mainCam != null && countdownMesh.gameObject.activeSelf)
        {
            countdownMesh.transform.rotation = mainCam.transform.rotation;
        }
    }

    // -----------------------------------------------------------------------
    // Text erstellen
    // -----------------------------------------------------------------------

    private void CreateCountdownText()
    {
        GameObject textGo = new GameObject("CountdownText");
        textGo.transform.SetParent(transform);
        textGo.transform.localPosition = new Vector3(0, textHeight, 0);
        textGo.transform.localScale = Vector3.one;

        countdownMesh = textGo.AddComponent<TextMesh>();
        countdownMesh.text = "";
        countdownMesh.fontSize = 100;
        countdownMesh.characterSize = textSize;
        countdownMesh.color = textColor;
        countdownMesh.anchor = TextAnchor.MiddleCenter;
        countdownMesh.alignment = TextAlignment.Center;
        countdownMesh.fontStyle = FontStyle.Bold;

        textGo.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // Trigger
    // -----------------------------------------------------------------------

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Freeze")) return;
        if (isFrozen) return;

        Debug.Log($"[FreezeZone] FreezeCube erkannt – friere {targetToFreeze?.name} ein.");

        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        other.transform.position = transform.position + Vector3.up * 0.02f;

        OrbSharedState.Lock(other.gameObject.GetInstanceID());
        StartCoroutine(FreezeTarget(other.gameObject));
    }

    // -----------------------------------------------------------------------
    // Freeze Coroutine
    // -----------------------------------------------------------------------

    private IEnumerator FreezeTarget(GameObject freezeCube)
    {
        if (targetToFreeze == null)
        {
            Debug.LogWarning("[FreezeZone] targetToFreeze nicht zugewiesen!");
            if (freezeCube != null) Destroy(freezeCube);
            yield break;
        }

        isFrozen = true;
        UpdateVisual(true);

        GhostOrbController ghost = targetToFreeze as GhostOrbController;
        PlayerOrbController player = targetToFreeze as PlayerOrbController;

        if (ghost != null) ghost.Freeze(freezeDuration);
        if (player != null) player.Freeze(freezeDuration);

        if (ghost == null && player == null)
            Debug.LogWarning("[FreezeZone] targetToFreeze ist weder Ghost noch Player!");

        // Countdown anzeigen
        if (countdownMesh != null) countdownMesh.gameObject.SetActive(true);

        float remaining = freezeDuration;
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            if (countdownMesh != null)
                countdownMesh.text = Mathf.CeilToInt(Mathf.Max(0, remaining)).ToString();
            yield return null;
        }

        // Aufräumen
        if (countdownMesh != null)
        {
            countdownMesh.text = "";
            countdownMesh.gameObject.SetActive(false);
        }

        if (freezeCube != null)
        {
            OrbSharedState.Unlock(freezeCube.GetInstanceID());
            Destroy(freezeCube);
        }

        isFrozen = false;
        UpdateVisual(false);
        Debug.Log("[FreezeZone] Freeze beendet.");
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