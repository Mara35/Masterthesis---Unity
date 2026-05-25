/*
 * Project:    SensinGlove – Box & Block Rehab Game
 * File:       PegChallengeZone.cs
 * Author:     Mari und Kiki (MCI – University of Applied Sciences)
 * Supervisor: Simon Winkler, BSc MSc
 * Year:       2025
 *
 * Attach to:  PegZone Prefab
 * Akzeptiert nur den Zylinder mit passender colorId.
 */

using UnityEngine;

public class PegChallengeZone : MonoBehaviour
{
    [Tooltip("Farb-ID – muss mit dem Zylinder übereinstimmen (0=Rot, 1=Blau, 2=Gelb)")]
    public int colorId = 0;

    public bool IsOccupied { get; private set; } = false;

    private PegChallengeCube placedPeg = null;
    private Renderer zoneRenderer;
    private Color baseColor;

    private void Start()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        zoneRenderer = GetComponent<Renderer>();
        if (zoneRenderer != null && colorId < PegChallengeCube.PegColors.Length)
        {
            // Zone in der passenden Farbe anzeigen, leicht transparent
            baseColor = PegChallengeCube.PegColors[colorId];
            baseColor.a = 0.5f;
            zoneRenderer.material.color = baseColor;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsOccupied) return;

        PegChallengeCube peg = other.GetComponent<PegChallengeCube>();
        if (peg == null) return;

        // Nur passende Farbe akzeptieren
        if (peg.colorId != colorId)
        {
            Debug.Log($"[PegChallengeZone] Falsche Farbe! Erwartet {colorId}, bekommen {peg.colorId}");
            return;
        }

        IsOccupied = true;
        placedPeg = peg;
        peg.IsPlaced = true;

        // Zylinder senkrecht und zentriert in Zone fixieren
        Rigidbody rb = other.GetComponent<Rigidbody>();
        other.transform.position = transform.position + Vector3.up * 0.03f;
        other.transform.rotation = Quaternion.identity;
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; rb.velocity = Vector3.zero; }

        // Zone leuchtet auf
        if (zoneRenderer != null)
            zoneRenderer.material.color = PegChallengeCube.PegColors[colorId];

        Debug.Log($"[PegChallengeZone] Richtig! Farbe {colorId} korrekt platziert.");
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;

        PegChallengeCube peg = other.GetComponent<PegChallengeCube>();
        if (peg == null || placedPeg == null) return;

        if (peg == placedPeg)
        {
            IsOccupied = false;
            placedPeg = null;
            peg.IsPlaced = false;

            // Farbe zurücksetzen
            if (zoneRenderer != null && colorId < PegChallengeCube.PegColors.Length)
            {
                Color c = PegChallengeCube.PegColors[colorId];
                c.a = 0.5f;
                zoneRenderer.material.color = c;
            }
        }
    }

    public void Reset()
    {
        IsOccupied = false;
        placedPeg = null;
    }
}