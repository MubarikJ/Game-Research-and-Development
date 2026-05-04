using System;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LocalDashboardServer : MonoBehaviour
{
    private HttpListener listener;
    private Thread serverThread;

    private readonly object sceneLock = new object();
    private int sceneToLoad = -1;
    private int currentSceneIndex = -1;

    private const string PASSWORD = "test";

    void Start()
    {
        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/");
            listener.Start();

            serverThread = new Thread(HandleRequests);
            serverThread.IsBackground = true;
            serverThread.Start();

            Debug.Log("Dashboard running at http://localhost:8080");
        }
        catch (Exception e)
        {
            Debug.LogError("Server failed to start: " + e.Message);
        }
    }

    void Update()
    {
        currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

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
            Debug.Log("Website loading scene index: " + index);
            SceneManager.LoadScene(index);
        }
    }

    void HandleRequests()
    {
        while (listener != null && listener.IsListening)
        {
            try
            {
                var context = listener.GetContext();
                string path = context.Request.Url.AbsolutePath;
                string password = context.Request.QueryString["password"];

                if (string.IsNullOrEmpty(password) || password != PASSWORD)
                {
                    SendHtml(context.Response, BuildLoginHtml());
                    continue;
                }

                if (path == "/scene")
                {
                    string sceneIndexStr = context.Request.QueryString["index"];

                    if (int.TryParse(sceneIndexStr, out int index))
                    {
                        lock (sceneLock)
                        {
                            sceneToLoad = index;
                        }

                        Debug.Log("Website requested scene index: " + index);
                    }

                    RedirectHome(context.Response);
                    continue;
                }

                if (path == "/stop")
                {
                    if (SessionLogger.Instance != null)
                    {
                        SessionLogger.Instance.StopCollection();
                    }

                    RedirectHome(context.Response);
                    continue;
                }

                if (path == "/save")
                {
                    string csv = SessionLogger.Instance != null ? SessionLogger.Instance.GetCsv() : "";
                    byte[] buffer = Encoding.UTF8.GetBytes(csv);

                    context.Response.ContentType = "text/csv";
                    context.Response.AddHeader("Content-Disposition", "attachment; filename=session_data.csv");
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    context.Response.OutputStream.Close();
                    continue;
                }

                SendHtml(context.Response, BuildDashboardHtml());
            }
            catch (Exception e)
            {
                Debug.LogWarning("Dashboard request error: " + e.Message);
            }
        }
    }

    string BuildLoginHtml()
    {
        return @"
        <html>
        <head>
            <title>Doctor Login</title>
            <style>
                body {
                    margin: 0;
                    font-family: Arial, sans-serif;
                    background: #eef2f5;
                    height: 100vh;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                }

                .box {
                    background: white;
                    padding: 45px;
                    border-radius: 18px;
                    box-shadow: 0 12px 35px rgba(0,0,0,0.15);
                    width: 380px;
                    text-align: center;
                }

                h1 {
                    margin-top: 0;
                    color: #18324a;
                }

                input {
                    width: 100%;
                    font-size: 20px;
                    padding: 14px;
                    margin: 20px 0;
                    border: 1px solid #ccd6dd;
                    border-radius: 10px;
                }

                button {
                    width: 100%;
                    font-size: 20px;
                    padding: 14px;
                    background: #1f5f8b;
                    color: white;
                    border: none;
                    border-radius: 10px;
                    cursor: pointer;
                }
            </style>
        </head>
        <body>
            <div class='box'>
                <h1>Doctor Login</h1>
                <form action='/' method='get'>
                    <input type='password' name='password' placeholder='Password' />
                    <button type='submit'>Enter Dashboard</button>
                </form>
            </div>
        </body>
        </html>";
    }

    string BuildDashboardHtml()
    {
        string stats = SessionLogger.Instance != null ? SessionLogger.Instance.GetStatsHtml() : "No logger found.";
        string log = SessionLogger.Instance != null ? SessionLogger.Instance.GetLogHtml() : "No logger found.";
        string graph = SessionLogger.Instance != null ? SessionLogger.Instance.GetGraphSvg() : "";

        return $@"
        <html>
        <head>
            <title>Stroke Rehab Dashboard</title>
            <meta http-equiv='refresh' content='2; url=/?password=test'>
            <style>
                body {{
                    margin: 0;
                    background: #eef2f5;
                    font-family: Arial, sans-serif;
                    color: #18324a;
                }}

                .header {{
                    background: #18324a;
                    color: white;
                    padding: 24px 32px;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                }}

                .header h1 {{
                    margin: 0;
                    font-size: 34px;
                }}

                .header .status {{
                    font-size: 18px;
                    background: #ffffff22;
                    padding: 10px 16px;
                    border-radius: 999px;
                }}

                .container {{
                    padding: 24px;
                    display: grid;
                    grid-template-columns: 42% 58%;
                    gap: 24px;
                }}

                .panel {{
                    background: white;
                    border-radius: 16px;
                    padding: 22px;
                    box-shadow: 0 6px 18px rgba(0,0,0,0.08);
                    margin-bottom: 24px;
                }}

                .panel h2 {{
                    margin: 0 0 18px 0;
                    color: #18324a;
                    font-size: 26px;
                }}

                .scene-form {{
                    display: flex;
                    gap: 12px;
                    flex-wrap: wrap;
                }}

                .scene-button {{
                    width: 130px;
                    height: 72px;
                    border: none;
                    border-radius: 14px;
                    background: #dbe7ef;
                    color: #18324a;
                    font-size: 18px;
                    font-weight: bold;
                    cursor: pointer;
                }}

                .scene-button:hover {{
                    background: #c6d9e6;
                }}

                .stats-grid {{
                    display: grid;
                    grid-template-columns: 1fr 1fr;
                    gap: 14px;
                }}

                .stat-card {{
                    background: #f5f8fa;
                    border-left: 6px solid #1f5f8b;
                    padding: 16px;
                    border-radius: 12px;
                }}

                .stat-card span {{
                    display: block;
                    font-size: 15px;
                    color: #607789;
                    margin-bottom: 8px;
                }}

                .stat-card strong {{
                    font-size: 28px;
                    color: #18324a;
                }}

                .action-row {{
                    display: flex;
                    gap: 12px;
                    justify-content: flex-start;
                    margin-bottom: 24px;
                }}

                .action-button {{
                    width: 180px;
                    height: 64px;
                    border: none;
                    border-radius: 14px;
                    font-size: 20px;
                    font-weight: bold;
                    color: white;
                    cursor: pointer;
                }}

                .stop-button {{
                    background: #a63d40;
                }}

                .save-button {{
                    background: #1f7a4d;
                }}

                .graph-box {{
                    background: white;
                    border-radius: 12px;
                    overflow: hidden;
                    border: 1px solid #e4eaee;
                }}

                .log-panel {{
                    height: 420px;
                    overflow-y: auto;
                }}

                table {{
                    width: 100%;
                    border-collapse: collapse;
                    font-size: 16px;
                }}

                th {{
                    position: sticky;
                    top: 0;
                    background: #18324a;
                    color: white;
                    padding: 10px;
                    text-align: left;
                }}

                td {{
                    border-bottom: 1px solid #e6ecef;
                    padding: 10px;
                }}

                tr:nth-child(even) {{
                    background: #f7fafb;
                }}

                .small-note {{
                    color: #607789;
                    font-size: 15px;
                    margin-top: -8px;
                    margin-bottom: 14px;
                }}
            </style>
        </head>

        <body>
            <div class='header'>
                <h1>Patient Session Dashboard</h1>
                <div class='status'>Current scene: {currentSceneIndex}</div>
            </div>

            <div class='container'>
                <div>
                    <div class='panel'>
                        <h2>Scene Control</h2>
                        <p class='small-note'>Therapist-controlled navigation for the current session.</p>

                        <form class='scene-form' action='/scene' method='get'>
                            <input type='hidden' name='password' value='test'>
                            <button class='scene-button' name='index' value='0'>Main Menu</button>
                            <button class='scene-button' name='index' value='1'>Game Scene 1</button>
                            <button class='scene-button' name='index' value='2'>Game Scene 2</button>
                        </form>
                    </div>

                    <div class='panel'>
                        <h2>Patient Data</h2>
                        <div class='stats-grid'>
                            {stats}
                        </div>
                    </div>
                </div>

                <div>
                    <div class='action-row'>
                        <form action='/stop' method='get'>
                            <input type='hidden' name='password' value='test'>
                            <button class='action-button stop-button'>Stop</button>
                        </form>

                        <form action='/save' method='get'>
                            <input type='hidden' name='password' value='test'>
                            <button class='action-button save-button'>Save CSV</button>
                        </form>
                    </div>

                    <div class='panel'>
                        <h2>Control Performance Graph</h2>
                        <p class='small-note'>Last 40 recorded samples, shown as percentage control over time.</p>
                        <div class='graph-box'>
                            {graph}
                        </div>
                    </div>

                    <div class='panel log-panel'>
                        <h2>Session Log</h2>
                        {log}
                    </div>
                </div>
            </div>
        </body>
        </html>";
    }

    void SendHtml(HttpListenerResponse response, string html)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.OutputStream.Close();
    }

    void RedirectHome(HttpListenerResponse response)
    {
        response.Redirect("/?password=test");
        response.Close();
    }

    void OnApplicationQuit()
    {
        try
        {
            listener?.Stop();
            serverThread?.Join(500);
        }
        catch { }
    }
}