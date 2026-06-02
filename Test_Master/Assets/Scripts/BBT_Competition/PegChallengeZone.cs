/*
 * Attach to:  PegZone Prefab
 * Accepts only the cylinder with the matching colorId.
 */

using UnityEngine;

public class PegChallengeZone : MonoBehaviour
{
    [Tooltip("Color ID – must match the cylinder (0=Red, 1=Blue, 2=Yellow)")]
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
            // Display the zone in the appropriate color, slightly transparent
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

        // Accept only matching colors
        if (peg.colorId != colorId)
        {
            Debug.Log($"[PegChallengeZone] Wrong color! Expected {colorId}, got {peg.colorId}");
            return;
        }

        IsOccupied = true;
        placedPeg = peg;
        peg.IsPlaced = true;

        // Position the cylinder vertically and centrally in the zone
        Rigidbody rb = other.GetComponent<Rigidbody>();
        other.transform.position = transform.position + Vector3.up * 0.03f;
        other.transform.rotation = Quaternion.identity;
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; rb.velocity = Vector3.zero; }

        // Zone lights up
        if (zoneRenderer != null)
            zoneRenderer.material.color = PegChallengeCube.PegColors[colorId];

        Debug.Log($"[PegChallengeZone]  Correct! Color {colorId} is in the right place.");
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

            // Reset zone color
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