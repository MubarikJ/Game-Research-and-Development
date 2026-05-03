using UnityEngine;

public class FakeDataSimulator : MonoBehaviour
{
    [Header("Simulation")]
    public bool simulateData = true;
    public float logEverySeconds = 3f;

    private float timer = 0f;

    void Start()
    {
        Debug.Log("FakeDataSimulator started");
    }

    void Update()
    {
        if (!simulateData) return;
        if (SessionLogger.Instance == null) return;

        timer += Time.deltaTime;

        if (timer >= logEverySeconds)
        {
            timer = 0f;

            // Simulate realistic signal (smooth + some variation)
            float baseSignal = Mathf.PingPong(Time.time * 0.2f, 1f);

            // Add small noise
            float noise = Random.Range(-0.1f, 0.1f);

            float fakeValue = Mathf.Clamp01(baseSignal + noise);

            SessionLogger.Instance.LogValue(fakeValue);

            Debug.Log("Fake data logged: " + fakeValue);
        }
    }
}