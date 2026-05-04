using UnityEngine;
using UnityEngine.SceneManagement;

public class AmbientAudioManager : MonoBehaviour
{
    public static AmbientAudioManager Instance;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip ambientClip;

    [Header("Settings")]
    public float volume = 0.5f;
    public bool musicEnabled = true;

    [Header("Scenes Where Music Should Not Play")]
    public int mutedSceneIndex = 1; // BugRoom Scene 1

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = volume;

        if (ambientClip != null)
            audioSource.clip = ambientClip;

        SceneManager.sceneLoaded += OnSceneLoaded;

        UpdateMusicState();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateMusicState();
    }

    public void SetMusicEnabled(bool enabled)
    {
        musicEnabled = enabled;
        UpdateMusicState();
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);

        if (audioSource != null)
            audioSource.volume = volume;
    }

    public void IncreaseVolume()
    {
        SetVolume(volume + 0.1f);
    }

    public void DecreaseVolume()
    {
        SetVolume(volume - 0.1f);
    }

    public float GetVolume()
    {
        return volume;
    }

    public bool IsMusicEnabled()
    {
        return musicEnabled;
    }

    void UpdateMusicState()
    {
        if (audioSource == null || ambientClip == null)
            return;

        int currentScene = SceneManager.GetActiveScene().buildIndex;
        bool shouldPlay = musicEnabled && currentScene != mutedSceneIndex;

        audioSource.volume = volume;

        if (shouldPlay)
        {
            if (!audioSource.isPlaying)
                audioSource.Play();
        }
        else
        {
            if (audioSource.isPlaying)
                audioSource.Pause();
        }
    }
}