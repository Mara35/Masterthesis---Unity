using System.Collections;
using UnityEngine;

public class SequenceCube : MonoBehaviour
{
    [Tooltip("Reihenfolge dieses Würfels (1, 2 oder 3)")]
    public int sequenceNumber = 1;

    [Tooltip("Wie lange die Zahl sichtbar ist (Sekunden)")]
    public float numberDisplayTime = 2f;

    public bool IsTransferred { get; set; } = false;

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private TextMesh numberText;
    private Camera mainCam;
    private float partitionX;
    private bool spawnedOnGhostSide;

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Start()
    {
        mainCam = Camera.main;

        GameObject cp = GameObject.Find("CenterPartition");
        partitionX = cp != null ? cp.transform.position.x : 0f;
        spawnedOnGhostSide = transform.position.x < partitionX;

        CreateNumberText();
        StartCoroutine(HideNumberAfterDelay());
    }

    private void LateUpdate()
    {
        // Zahl immer zur Kamera drehen
        if (numberText != null && numberText.gameObject.activeSelf && mainCam != null)
            numberText.transform.rotation = mainCam.transform.rotation;
    }

    // -----------------------------------------------------------------------
    // Nummer anzeigen
    // -----------------------------------------------------------------------

    private void CreateNumberText()
    {
        GameObject textGo = new GameObject("SequenceNumber");
        textGo.transform.SetParent(transform);
        textGo.transform.localPosition = new Vector3(0, 0.12f, 0);

        numberText = textGo.AddComponent<TextMesh>();
        numberText.text = sequenceNumber.ToString();
        numberText.fontSize = 100;
        numberText.characterSize = 0.15f;
        numberText.color = Color.white;
        numberText.anchor = TextAnchor.MiddleCenter;
        numberText.alignment = TextAlignment.Center;
        numberText.fontStyle = FontStyle.Bold;
    }

    private IEnumerator HideNumberAfterDelay()
    {
        yield return new WaitForSeconds(numberDisplayTime);
        if (numberText != null)
            numberText.gameObject.SetActive(false);
    }

    // -----------------------------------------------------------------------
    // Public
    // -----------------------------------------------------------------------

    public bool IsOnGhostSide() => transform.position.x < partitionX;
    public bool SpawnedOnGhostSide() => spawnedOnGhostSide;
}