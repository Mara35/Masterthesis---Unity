using UnityEngine;

public class DropTargetMarker : MonoBehaviour
{
    [SerializeField] public Color markerColor = Color.yellow;
    [SerializeField] private float size = 0.12f;
    [SerializeField] private float thickness = 0.006f;

    private GameObject frameRoot;
    private Material frameMat;

    // Awake: Frame under construction, not yet colored
    private void Awake()
    {
        // Material is white for now—color will be added in Start()
        frameMat = new Material(Shader.Find("Unlit/Color"));

        frameRoot = new GameObject("_FrameRoot");
        frameRoot.transform.SetParent(transform, false);
        frameRoot.transform.localPosition = Vector3.zero;
        frameRoot.transform.localRotation = Quaternion.identity;

        float h = size / 2f;
        float t = thickness;
        float y = 0.003f;

        CreateBar(new Vector3(0, y, h), new Vector3(size + t, t, t));
        CreateBar(new Vector3(0, y, -h), new Vector3(size + t, t, t));
        CreateBar(new Vector3(-h, y, 0), new Vector3(t, t, size - t));
        CreateBar(new Vector3(h, y, 0), new Vector3(t, t, size - t));

        frameRoot.SetActive(false);
    }

    // Start: Inspector values are now loaded -> Set color
    private void Start()
    {
        if (frameMat != null)
            frameMat.color = markerColor;
    }

    private void CreateBar(Vector3 localPos, Vector3 localScale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "bar";
        go.transform.SetParent(frameRoot.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;

        var col = go.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        go.GetComponent<Renderer>().sharedMaterial = frameMat;
    }

    public void SetColor(Color c)
    {
        markerColor = c;
        if (frameMat != null) frameMat.color = c;
    }

    public void SetVisible(bool visible)
    {
        if (frameRoot != null) frameRoot.SetActive(visible);
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            if (frameMat != null) Destroy(frameMat);
            if (frameRoot != null) Destroy(frameRoot);
        }
        else
        {
            if (frameMat != null) DestroyImmediate(frameMat);
            if (frameRoot != null) DestroyImmediate(frameRoot);
        }
    }
}