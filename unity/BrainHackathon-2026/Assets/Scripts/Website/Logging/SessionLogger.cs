using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class SessionSample
{
    public string time;
    public float value;
    public float score;

    public bool lookingAtTarget;
    public bool combinedActivation;
}

public class SessionLogger : MonoBehaviour
{
    public static SessionLogger Instance;

    private readonly object dataLock = new object();

    public List<SessionSample> samples = new List<SessionSample>();

    public bool isCollecting = true;

    private DateTime sessionStartTime;
    private float latestValue = 0f;

    private bool latestLookingAtTarget = false;
    private bool latestCombinedActivation = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        sessionStartTime = DateTime.Now;
        Debug.Log("SessionLogger ready");
    }

    public void LogValue(float value)
    {
        if (!isCollecting) return;

        value = Mathf.Clamp01(value);
        float score = value * 100f;

        lock (dataLock)
        {
            latestValue = value;

            samples.Add(new SessionSample
            {
                time = DateTime.Now.ToString("HH:mm:ss"),
                value = value,
                score = score,
                lookingAtTarget = latestLookingAtTarget,
                combinedActivation = latestCombinedActivation
            });
        }
    }

    public void LogEyeEegSync(bool lookingAtTarget, bool combinedActivation)
    {
        lock (dataLock)
        {
            latestLookingAtTarget = lookingAtTarget;
            latestCombinedActivation = combinedActivation;
        }
    }

    public void StopCollection()
    {
        isCollecting = false;
        Debug.Log("Data collection stopped");
    }

    public string GetStatsHtml()
    {
        lock (dataLock)
        {
            int count = samples.Count;
            float current = latestValue * 100f;
            float average = 0f;
            float best = 0f;
            float lowest = 100f;

            int lookingCount = 0;
            int combinedCount = 0;

            foreach (var sample in samples)
            {
                average += sample.score;

                if (sample.score > best) best = sample.score;
                if (sample.score < lowest) lowest = sample.score;

                if (sample.lookingAtTarget) lookingCount++;
                if (sample.combinedActivation) combinedCount++;
            }

            float lookingPercent = 0f;
            float syncPercent = 0f;

            if (count > 0)
            {
                average /= count;
                lookingPercent = (lookingCount / (float)count) * 100f;
                syncPercent = (combinedCount / (float)count) * 100f;
            }
            else
            {
                lowest = 0f;
            }

            TimeSpan duration = DateTime.Now - sessionStartTime;

            return $@"
                <div class='stat-card'>
                    <span>Current Control</span>
                    <strong>{current:F0}%</strong>
                </div>
                <div class='stat-card'>
                    <span>Performance Score</span>
                    <strong>{current:F0}/100</strong>
                </div>
                <div class='stat-card'>
                    <span>Average Control</span>
                    <strong>{average:F0}%</strong>
                </div>
                <div class='stat-card'>
                    <span>Best Control</span>
                    <strong>{best:F0}%</strong>
                </div>
                <div class='stat-card'>
                    <span>Lowest Control</span>
                    <strong>{lowest:F0}%</strong>
                </div>
                <div class='stat-card'>
                    <span>Eye Focus</span>
                    <strong>{lookingPercent:F0}%</strong>
                </div>
                <div class='stat-card'>
                    <span>Eye + EEG Sync</span>
                    <strong>{syncPercent:F0}%</strong>
                </div>
                <div class='stat-card'>
                    <span>Samples</span>
                    <strong>{count}</strong>
                </div>
                <div class='stat-card'>
                    <span>Session Time</span>
                    <strong>{duration:mm\:ss}</strong>
                </div>
                <div class='stat-card'>
                    <span>Status</span>
                    <strong>{(isCollecting ? "Recording" : "Stopped")}</strong>
                </div>
            ";
        }
    }

    public string GetGraphSvg()
    {
        lock (dataLock)
        {
            int width = 900;
            int height = 260;
            int padding = 35;

            if (samples.Count < 2)
            {
                return $@"
                <svg width='100%' height='{height}' viewBox='0 0 {width} {height}'>
                    <rect width='{width}' height='{height}' fill='#ffffff'/>
                    <text x='{width / 2}' y='{height / 2}' text-anchor='middle' fill='#888' font-size='22'>
                        Waiting for data...
                    </text>
                </svg>";
            }

            int maxPoints = 40;
            int start = Mathf.Max(0, samples.Count - maxPoints);
            int visibleCount = samples.Count - start;

            StringBuilder points = new StringBuilder();

            for (int i = 0; i < visibleCount; i++)
            {
                float score = samples[start + i].score;

                float x = padding + ((float)i / (visibleCount - 1)) * (width - padding * 2);
                float y = height - padding - (score / 100f) * (height - padding * 2);

                points.Append(
                    x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                    + "," +
                    y.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                    + " "
                );
            }

            return $@"
            <svg width='100%' height='{height}' viewBox='0 0 {width} {height}'>
                <rect width='{width}' height='{height}' fill='#ffffff'/>

                <defs>
                    <clipPath id='chartClip'>
                        <rect x='{padding}' y='{padding}' width='{width - padding * 2}' height='{height - padding * 2}' />
                    </clipPath>
                </defs>

                <line x1='{padding}' y1='{height - padding}' x2='{width - padding}' y2='{height - padding}' stroke='#999' stroke-width='2'/>
                <line x1='{padding}' y1='{padding}' x2='{padding}' y2='{height - padding}' stroke='#999' stroke-width='2'/>

                <line x1='{padding}' y1='80' x2='{width - padding}' y2='80' stroke='#ddd' stroke-width='1'/>
                <line x1='{padding}' y1='130' x2='{width - padding}' y2='130' stroke='#ddd' stroke-width='1'/>
                <line x1='{padding}' y1='180' x2='{width - padding}' y2='180' stroke='#ddd' stroke-width='1'/>

                <text x='5' y='{padding + 5}' fill='#555' font-size='14'>100%</text>
                <text x='10' y='{height / 2}' fill='#555' font-size='14'>50%</text>
                <text x='18' y='{height - padding + 5}' fill='#555' font-size='14'>0%</text>

                <polyline points='{points}' fill='none' stroke='#1f77b4' stroke-width='4' stroke-linecap='round' stroke-linejoin='round' clip-path='url(#chartClip)'/>
            </svg>";
        }
    }

    public string GetLogHtml()
    {
        lock (dataLock)
        {
            if (samples.Count == 0)
                return "<p>No data recorded yet.</p>";

            StringBuilder html = new StringBuilder();
            html.Append("<table>");
            html.Append("<tr><th>Time</th><th>Signal</th><th>Score</th><th>Looking</th><th>Sync</th></tr>");

            for (int i = samples.Count - 1; i >= 0; i--)
            {
                html.Append(
                    $"<tr>" +
                    $"<td>{samples[i].time}</td>" +
                    $"<td>{samples[i].value:F2}</td>" +
                    $"<td>{samples[i].score:F0}</td>" +
                    $"<td>{(samples[i].lookingAtTarget ? "Yes" : "No")}</td>" +
                    $"<td>{(samples[i].combinedActivation ? "Yes" : "No")}</td>" +
                    $"</tr>"
                );
            }

            html.Append("</table>");
            return html.ToString();
        }
    }

    public string GetCsv()
    {
        lock (dataLock)
        {
            int count = samples.Count;
            float current = latestValue * 100f;
            float average = 0f;
            float best = 0f;
            float lowest = 100f;

            int lookingCount = 0;
            int combinedCount = 0;

            foreach (var sample in samples)
            {
                average += sample.score;

                if (sample.score > best) best = sample.score;
                if (sample.score < lowest) lowest = sample.score;

                if (sample.lookingAtTarget) lookingCount++;
                if (sample.combinedActivation) combinedCount++;
            }

            float lookingPercent = 0f;
            float syncPercent = 0f;

            if (count > 0)
            {
                average /= count;
                lookingPercent = (lookingCount / (float)count) * 100f;
                syncPercent = (combinedCount / (float)count) * 100f;
            }
            else
            {
                lowest = 0f;
            }

            TimeSpan duration = DateTime.Now - sessionStartTime;

            StringBuilder csv = new StringBuilder();

            csv.AppendLine("Patient Data");
            csv.AppendLine($"Current Control,{current:F0}%");
            csv.AppendLine($"Performance Score,{current:F0}/100");
            csv.AppendLine($"Average Control,{average:F0}%");
            csv.AppendLine($"Best Control,{best:F0}%");
            csv.AppendLine($"Lowest Control,{lowest:F0}%");
            csv.AppendLine($"Eye Focus,{lookingPercent:F0}%");
            csv.AppendLine($"Eye EEG Synchronization,{syncPercent:F0}%");
            csv.AppendLine($"Samples Recorded,{count}");
            csv.AppendLine($"Session Duration,{duration:mm\\:ss}");
            csv.AppendLine($"Collection Status,{(isCollecting ? "Recording" : "Stopped")}");
            csv.AppendLine();

            csv.AppendLine("Log Data");
            csv.AppendLine("Time,Signal,Score,LookingAtTarget,CombinedActivation");

            foreach (var sample in samples)
            {
                csv.AppendLine(
                    $"{sample.time}," +
                    $"{sample.value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{sample.score.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{sample.lookingAtTarget}," +
                    $"{sample.combinedActivation}"
                );
            }

            return csv.ToString();
        }
    }
}