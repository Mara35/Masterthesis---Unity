using System.Collections;
using UnityEngine;

/// <summary>
/// Trigger zone that freezes its target (an orb controller) for freezeDuration when a matching freeze
/// cube is dropped in, with a color tint and a floating countdown as feedback.
/// </summary>
public class FreezeZone : MonoBehaviour
{
    [Header("Target")]
    public MonoBehaviour targetToFreeze;

    [Header("Settings")]
    public float freezeDuration = 5f;

    [Header("Visual feedback")]
    public Renderer zoneRenderer;
    public Color activeColor = new Color(0.2f, 0.5f, 1f, 0.5f);
    public Color inactiveColor = new Color(0.2f, 0.5f, 1f, 0.15f);

    [Header("Countdown Text")]
    public float textHeight = 0.3f;
    public float textSize = 0.2f;
    public Color textColor = new Color(0f, 0.1f, 0.5f);

    private bool isFrozen = false;
    private TextMesh countdownMesh;
    private Camera mainCam;

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
        if (countdownMesh != null && mainCam != null && countdownMesh.gameObject.activeSelf)
            countdownMesh.transform.rotation = mainCam.transform.rotation;
    }

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

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Freeze")) return;
        if (isFrozen) return;

        Debug.Log($"[FreezeZone] FreezeCube detected - freeze {targetToFreeze?.name}.");

        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        other.transform.position = transform.position + Vector3.up * 0.02f;

        OrbSharedState.Lock(other.gameObject.GetInstanceID());
        StartCoroutine(FreezeTarget(other.gameObject));
    }

    private IEnumerator FreezeTarget(GameObject freezeCube)
    {
        if (targetToFreeze == null)
        {
            Debug.LogWarning("[FreezeZone] targetToFreeze is not assigned!");
            if (freezeCube != null) Destroy(freezeCube);
            yield break;
        }

        isFrozen = true;
        UpdateVisual(true);

        // targetToFreeze is a MonoBehaviour because it can be either the GhostOrbController
        // or the human's GloveGrabber - we cast to whichever it is and freeze that one.

        GhostOrbController ghost = targetToFreeze as GhostOrbController;
        GloveGrabber player = targetToFreeze as GloveGrabber;

        if (ghost != null) ghost.Freeze(freezeDuration);
        if (player != null) player.Freeze(freezeDuration);

        if (ghost == null && player == null)
            Debug.LogWarning("[FreezeZone] targetToFreeze is neither Ghost nor GloveGrabber!");

        if (countdownMesh != null) countdownMesh.gameObject.SetActive(true);

        float remaining = freezeDuration;
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            if (countdownMesh != null)
                countdownMesh.text = Mathf.CeilToInt(Mathf.Max(0, remaining)).ToString();
            yield return null;
        }

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
        Debug.Log("[FreezeZone] Freeze ended.");
    }

    private void UpdateVisual(bool frozen)
    {
        if (zoneRenderer == null) return;
        zoneRenderer.material.color = frozen ? activeColor : inactiveColor;
    }
}