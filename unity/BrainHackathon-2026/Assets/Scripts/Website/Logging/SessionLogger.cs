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
}

public class SessionLogger : MonoBehaviour
{
    public static SessionLogger Instance;

    private readonly object dataLock = new object();

    public List<SessionSample> samples = new List<SessionSample>();

    public bool isCollecting = true;

    private DateTime sessionStartTime;
    private float latestValue = 0f;

    void Awake()
    {
        Instance = this;
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
                score = score
            });
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

            foreach (var sample in samples)
            {
                average += sample.score;
                if (sample.score > best) best = sample.score;
                if (sample.score < lowest) lowest = sample.score;
            }

            if (count > 0)
            {
                average /= count;
            }
            else
            {
                lowest = 0f;
            }

            TimeSpan duration = DateTime.Now - sessionStartTime;

            return $@"
                <div class='metric'><span>Control now</span><b>{current:F0}%</b></div>
                <div class='metric'><span>Current signal</span><b>{latestValue:F2}</b></div>
                <div class='metric'><span>Performance score</span><b>{current:F0}/100</b></div>
                <div class='metric'><span>Consistency average</span><b>{average:F0}%</b></div>
                <div class='metric'><span>Potential best</span><b>{best:F0}%</b></div>
                <div class='metric'><span>Fatigue / lowest</span><b>{lowest:F0}%</b></div>
                <div class='metric'><span>Samples recorded</span><b>{count}</b></div>
                <div class='metric'><span>Session duration</span><b>{duration:mm\:ss}</b></div>
            ";
        }
    }

    public string GetLogHtml()
    {
        lock (dataLock)
        {
            if (samples.Count == 0)
                return "<p>No data recorded yet.</p>";

            StringBuilder html = new StringBuilder();
            html.Append("<table><tr><th>Time</th><th>Signal</th><th>Score</th></tr>");

            int start = Mathf.Max(0, samples.Count - 20);

            for (int i = start; i < samples.Count; i++)
            {
                html.Append($"<tr><td>{samples[i].time}</td><td>{samples[i].value:F2}</td><td>{samples[i].score:F0}</td></tr>");
            }

            html.Append("</table>");
            return html.ToString();
        }
    }

    public string GetSummaryCsv()
    {
        lock (dataLock)
        {
            int count = samples.Count;
            float current = latestValue * 100f;
            float average = 0f;
            float best = 0f;
            float lowest = 100f;

            foreach (var sample in samples)
            {
                average += sample.score;
                if (sample.score > best) best = sample.score;
                if (sample.score < lowest) lowest = sample.score;
            }

            if (count > 0)
            {
                average /= count;
            }
            else
            {
                lowest = 0f;
            }

            TimeSpan duration = DateTime.Now - sessionStartTime;

            StringBuilder csv = new StringBuilder();
            csv.AppendLine("Patient Data");
            csv.AppendLine($"Control Now,{current:F0}%");
            csv.AppendLine($"Current Signal,{latestValue:F2}");
            csv.AppendLine($"Performance Score,{current:F0}/100");
            csv.AppendLine($"Consistency Average,{average:F0}%");
            csv.AppendLine($"Potential Best,{best:F0}%");
            csv.AppendLine($"Fatigue Lowest,{lowest:F0}%");
            csv.AppendLine($"Samples Recorded,{count}");
            csv.AppendLine($"Session Duration,{duration:mm\\:ss}");
            csv.AppendLine();

            return csv.ToString();
        }
    }

    public string GetCsv()
    {
        lock (dataLock)
        {
            StringBuilder csv = new StringBuilder();

            csv.Append(GetSummaryCsv());

            csv.AppendLine("Log Data");
            csv.AppendLine("Time,Signal,Score");

            foreach (var sample in samples)
            {
                csv.AppendLine($"{sample.time},{sample.value:F4},{sample.score:F2}");
            }

            return csv.ToString();
        }
    }
}