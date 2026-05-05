using UnityEngine;

public class DontDestroyDashboard : MonoBehaviour
{
    [Header("Unique ID for this persistent object")]
    public string uniqueID = "DashboardManager";

    private static readonly System.Collections.Generic.HashSet<string> existingIDs
        = new System.Collections.Generic.HashSet<string>();

    void Awake()
    {
        if (existingIDs.Contains(uniqueID))
        {
            Destroy(gameObject);
            return;
        }

        existingIDs.Add(uniqueID);
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (!Application.isPlaying)
        {
            existingIDs.Remove(uniqueID);
        }
    }
}