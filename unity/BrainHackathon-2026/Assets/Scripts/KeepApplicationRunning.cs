using UnityEngine;

public class KeepApplicationRunning : MonoBehaviour
{
    void Awake()
    {
        Application.runInBackground = true;
    }
}