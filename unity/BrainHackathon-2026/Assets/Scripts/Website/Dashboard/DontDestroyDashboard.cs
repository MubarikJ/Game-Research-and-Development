using UnityEngine;

public class DontDestroyDashboard : MonoBehaviour
{
    private static DontDestroyDashboard instance;

    void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}