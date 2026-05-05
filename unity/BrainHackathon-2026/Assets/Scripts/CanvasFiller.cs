using UnityEngine;

[ExecuteAlways]
public class CanvasFiller : MonoBehaviour
{
    [Header("Background / zones to stretch")]
    public RectTransform background;
    public RectTransform spawnArea;
    public RectTransform popZone;

    [Header("Pop Zone Height")]
    public float popZoneHeightPercent = 0.28f;

    void Awake()
    {
        Apply();
    }

    void Start()
    {
        Apply();
    }

    void Update()
    {
        Apply();
    }

    void Apply()
    {
        RectTransform canvas = GetComponent<RectTransform>();
        if (canvas == null) return;

        if (background != null)
        {
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;
        }

        if (popZone != null)
        {
            popZone.anchorMin = new Vector2(0f, 0f);
            popZone.anchorMax = new Vector2(1f, popZoneHeightPercent);
            popZone.offsetMin = Vector2.zero;
            popZone.offsetMax = Vector2.zero;
        }

        if (spawnArea != null)
        {
            spawnArea.anchorMin = new Vector2(0f, popZoneHeightPercent);
            spawnArea.anchorMax = new Vector2(1f, 1f);
            spawnArea.offsetMin = Vector2.zero;
            spawnArea.offsetMax = Vector2.zero;
        }
    }
}