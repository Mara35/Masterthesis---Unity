using System.Collections;
using UnityEngine;

public class SequenceCube : MonoBehaviour
{
    public float numberDisplayTime = 3f;

    public int sequenceNumber { get; private set; } = 1;
    public bool IsTransferred { get; set; } = false;

    private TextMesh numberText;
    private Camera mainCam;
    private float partitionX;
    private bool spawnedOnGhostSide;
    private bool initialized = false;
    private Color originalColor;

    public void Init(int number)
    {
        sequenceNumber = number;
        initialized = true;
        mainCam = Camera.main;

        GameObject cp = GameObject.Find("CenterPartition");
        partitionX = cp != null ? cp.transform.position.x : 0f;
        spawnedOnGhostSide = transform.position.x < partitionX;

        
        Renderer r = GetComponent<Renderer>();
        if (r != null) originalColor = r.material.color;

        CreateNumberText();
        StartCoroutine(HideNumberAfterDelay());
    }

    private void Start()
    {
        if (!initialized) Init(sequenceNumber);
    }

    private void LateUpdate()
    {
        if (numberText != null && numberText.gameObject.activeSelf && mainCam != null)
            numberText.transform.rotation = mainCam.transform.rotation;
    }

    private void CreateNumberText()
    {
        Transform existing = transform.Find("SequenceNumber");
        if (existing != null) Destroy(existing.gameObject);

        GameObject textGo = new GameObject("SequenceNumber");
        textGo.transform.SetParent(transform);
        textGo.transform.localPosition = new Vector3(0, 0.15f, 0);
        textGo.transform.localScale = Vector3.one;

        numberText = textGo.AddComponent<TextMesh>();
        numberText.text = sequenceNumber.ToString();
        numberText.fontSize = 150;
        numberText.characterSize = 0.12f;
        numberText.color = Color.white;
        numberText.anchor = TextAnchor.MiddleCenter;
        numberText.alignment = TextAlignment.Center;
        numberText.fontStyle = FontStyle.Bold;

        Debug.Log($"[SequenceCube] Number {sequenceNumber} displayed.");
    }

    private IEnumerator HideNumberAfterDelay()
    {
        yield return new WaitForSeconds(numberDisplayTime);
        if (numberText != null)
        {
            numberText.gameObject.SetActive(false);
            Debug.Log($"[SequenceCube] Number {sequenceNumber} hidden.");
        }
    }

    /// After transfer: wait 1s, then disappear
    public IEnumerator LingerAndDestroy()
    {
        yield return new WaitForSeconds(1f);
        Destroy(gameObject);
    }

    public bool SpawnedOnGhostSide() => spawnedOnGhostSide;
}