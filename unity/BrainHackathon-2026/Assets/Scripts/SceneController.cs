using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public static SceneController Instance;

    private readonly object sceneLock = new object();
    private int sceneToLoad = -1;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("SceneController ready");
    }

    public void RequestSceneChange(int sceneIndex)
    {
        lock (sceneLock)
        {
            sceneToLoad = sceneIndex;
        }

        Debug.Log("Website requested scene index: " + sceneIndex);
    }

    public void LoadSceneFromButton(int sceneIndex)
    {
        Debug.Log("Button clicked. Loading scene index: " + sceneIndex);
        SceneManager.LoadScene(sceneIndex);
    }

    void Update()
    {
        int index = -1;

        lock (sceneLock)
        {
            if (sceneToLoad != -1)
            {
                index = sceneToLoad;
                sceneToLoad = -1;
            }
        }

        if (index != -1)
        {
            Debug.Log("Loading scene index from website: " + index);
            SceneManager.LoadScene(index);
        }
    }

    public int GetCurrentSceneIndex()
    {
        return SceneManager.GetActiveScene().buildIndex;
    }
}