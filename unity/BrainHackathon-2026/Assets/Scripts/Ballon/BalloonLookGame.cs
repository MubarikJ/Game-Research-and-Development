using UnityEngine;
using Tobii.Gaming;

public class BalloonLookGame : MonoBehaviour
{
    [Header("References")]
    public RectTransform balloon;
    public RectTransform spawnArea;
    public RectTransform popZone;
    public SAPCReceiver sapcReceiver;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip focusSound;
    public AudioClip popSound;

    [Header("Movement")]
    public float riseSpeed = 120f;
    public float fallSpeed = 90f;
    public float swayAmount = 80f;
    public float swaySpeed = 2f;

    [Header("Focus Side Movement")]
    public float focusSideSpeed = 80f;
    public float changeSideEverySeconds = 1.5f;

    [Header("Look + EEG")]
    public float lookRadius = 120f;
    public float eegThreshold = 0.6f;

    private float swayTimer;
    private int score = 0;

    private bool wasFocused = false;
    private float sideTimer = 0f;
    private float sideDirection = 1f;

    void Start()
    {
        if (balloon == null)
            balloon = GetComponent<RectTransform>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        PickRandomSideDirection();
        ResetBalloon();
    }

    void Update()
    {
        bool looking = IsLookingAtBalloon();
        bool eegActive = sapcReceiver != null && sapcReceiver.CurrentValue >= eegThreshold;
        bool focused = looking && eegActive;

        if (SessionLogger.Instance != null)
        {
            SessionLogger.Instance.LogEyeEegSync(looking, focused);
        }

        Vector2 pos = balloon.anchoredPosition;

        if (focused)
        {
            pos.y += riseSpeed * Time.deltaTime;

            sideTimer += Time.deltaTime;
            if (sideTimer >= changeSideEverySeconds)
            {
                PickRandomSideDirection();
            }

            pos.x += sideDirection * focusSideSpeed * Time.deltaTime;

            if (!wasFocused)
            {
                PlaySound(focusSound);
            }
        }
        else
        {
            pos.y -= fallSpeed * Time.deltaTime;

            swayTimer += Time.deltaTime * swaySpeed;
            pos.x += Mathf.Sin(swayTimer) * swayAmount * Time.deltaTime;
        }

        wasFocused = focused;

        balloon.anchoredPosition = pos;

        if (IsTouchingPopZone())
        {
            Pop();
            return;
        }

        balloon.anchoredPosition = KeepInsideSpawnAreaXOnly(pos);
    }

    bool IsLookingAtBalloon()
    {
        GazePoint gazePoint = TobiiAPI.GetGazePoint();

        if (!gazePoint.IsValid)
            return false;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            spawnArea,
            gazePoint.Screen,
            null,
            out Vector2 localGaze
        );

        return Vector2.Distance(localGaze, balloon.anchoredPosition) <= lookRadius;
    }

    bool IsTouchingPopZone()
    {
        if (popZone == null)
            return false;

        Vector3[] balloonCorners = new Vector3[4];
        Vector3[] zoneCorners = new Vector3[4];

        balloon.GetWorldCorners(balloonCorners);
        popZone.GetWorldCorners(zoneCorners);

        float balloonBottom = balloonCorners[0].y;
        float popZoneTop = zoneCorners[1].y;

        return balloonBottom <= popZoneTop;
    }

    Vector2 KeepInsideSpawnAreaXOnly(Vector2 pos)
    {
        float halfAreaWidth = spawnArea.rect.width / 2f;
        float halfBalloonWidth = balloon.rect.width / 2f;

        pos.x = Mathf.Clamp(
            pos.x,
            -halfAreaWidth + halfBalloonWidth,
            halfAreaWidth - halfBalloonWidth
        );

        return pos;
    }

    void PickRandomSideDirection()
    {
        sideTimer = 0f;
        sideDirection = Random.value > 0.5f ? 1f : -1f;
    }

    void Pop()
    {
        score++;
        Debug.Log("POP! Score: " + score);

        PlaySound(popSound);
        ResetBalloon();
    }

    void ResetBalloon()
    {
        float halfAreaWidth = spawnArea.rect.width / 2f;
        float halfAreaHeight = spawnArea.rect.height / 2f;

        float halfBalloonWidth = balloon.rect.width / 2f;
        float halfBalloonHeight = balloon.rect.height / 2f;

        float randomX = Random.Range(
            -halfAreaWidth + halfBalloonWidth,
            halfAreaWidth - halfBalloonWidth
        );

        float y = halfAreaHeight - halfBalloonHeight;

        balloon.anchoredPosition = new Vector2(randomX, y);

        swayTimer = Random.Range(0f, 10f);
        wasFocused = false;
        PickRandomSideDirection();

        if (SessionLogger.Instance != null)
        {
            SessionLogger.Instance.LogEyeEegSync(false, false);
        }
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}