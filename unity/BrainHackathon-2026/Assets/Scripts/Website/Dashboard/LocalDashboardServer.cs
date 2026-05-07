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

    private readonly object musicLock = new object();
    private string pendingMusicAction = "";

    private string cachedMusicStatus = "Not found";
    private string cachedMusicVolume = "0";

    private readonly object appLock = new object();
    private bool quitRequested = false;

    private const string PASSWORD = "TheDoctorIsIn";

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

        string musicAction = "";

        lock (musicLock)
        {
            if (!string.IsNullOrEmpty(pendingMusicAction))
            {
                musicAction = pendingMusicAction;
                pendingMusicAction = "";
            }
        }

        if (!string.IsNullOrEmpty(musicAction))
        {
            if (AmbientAudioManager.Instance != null)
            {
                Debug.Log("Music action from website: " + musicAction);

                if (musicAction == "on")
                    AmbientAudioManager.Instance.SetMusicEnabled(true);

                if (musicAction == "off")
                    AmbientAudioManager.Instance.SetMusicEnabled(false);

                if (musicAction == "up")
                    AmbientAudioManager.Instance.IncreaseVolume();

                if (musicAction == "down")
                    AmbientAudioManager.Instance.DecreaseVolume();
            }
            else
            {
                Debug.LogWarning("AmbientAudioManager.Instance is null");
            }
        }

        if (AmbientAudioManager.Instance != null)
        {
            cachedMusicStatus = AmbientAudioManager.Instance.IsMusicEnabled() ? "On" : "Off";
            cachedMusicVolume = Mathf.RoundToInt(AmbientAudioManager.Instance.GetVolume() * 100f).ToString();
        }
        else
        {
            cachedMusicStatus = "Not found";
            cachedMusicVolume = "0";
        }

        bool shouldQuit = false;

        lock (appLock)
        {
            if (quitRequested)
            {
                shouldQuit = true;
                quitRequested = false;
            }
        }

        if (shouldQuit)
        {
            Debug.Log("Quit requested from website.");
            Application.Quit();
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

                if (path == "/login")
                {
                    string password = context.Request.QueryString["password"];

                    if (password == PASSWORD)
                    {
                        context.Response.SetCookie(new Cookie("dashboard_auth", "true"));
                        RedirectHome(context.Response);
                    }
                    else
                    {
                        SendHtml(context.Response, BuildLoginHtml());
                    }

                    continue;
                }

                bool isLoggedIn = false;
                Cookie authCookie = context.Request.Cookies["dashboard_auth"];

                if (authCookie != null && authCookie.Value == "true")
                    isLoggedIn = true;

                if (!isLoggedIn)
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

                if (path == "/music")
                {
                    string action = context.Request.QueryString["action"];

                    lock (musicLock)
                    {
                        pendingMusicAction = action;
                    }

                    RedirectHome(context.Response);
                    continue;
                }

                if (path == "/stop")
                {
                    if (SessionLogger.Instance != null)
                        SessionLogger.Instance.StopCollection();

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

                if (path == "/quit")
                {
                    lock (appLock)
                    {
                        quitRequested = true;
                    }

                    SendHtml(context.Response, "<html><body><h1>Application closing...</h1></body></html>");
                    continue;
                }

                if (path == "/data")
                {
                    SendHtml(context.Response, BuildLiveDataHtml());
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
                <form action='/login' method='get'>
                    <input type='password' name='password' placeholder='Password' />
                    <button type='submit'>Enter Dashboard</button>
                </form>
            </div>
        </body>
        </html>";
    }

    string BuildLiveDataHtml()
    {
        string stats = SessionLogger.Instance != null ? SessionLogger.Instance.GetStatsHtml() : "No logger found.";
        string log = SessionLogger.Instance != null ? SessionLogger.Instance.GetLogHtml() : "No logger found.";
        string graph = SessionLogger.Instance != null ? SessionLogger.Instance.GetGraphSvg() : "";
        string quitButton = BuildQuitButtonHtml();

        return $@"
            <div id='scene-status'>{currentSceneIndex}</div>
            <div id='music-status'>{cachedMusicStatus}</div>
            <div id='music-volume'>{cachedMusicVolume}%</div>
            <div id='stats-content'>{stats}</div>
            <div id='graph-content'>{graph}</div>
            <div id='log-content'>{log}</div>
            <div id='quit-content'>{quitButton}</div>
        ";
    }

    string BuildQuitButtonHtml()
    {
        bool recordingStopped = SessionLogger.Instance != null && !SessionLogger.Instance.isCollecting;

        if (!recordingStopped)
            return "";

        return @"
            <form action='/quit' method='get'>
                <button class='action-button quit-button'>Quit App</button>
            </form>";
    }

    string BuildDashboardHtml()
    {
        string stats = SessionLogger.Instance != null ? SessionLogger.Instance.GetStatsHtml() : "No logger found.";
        string log = SessionLogger.Instance != null ? SessionLogger.Instance.GetLogHtml() : "No logger found.";
        string graph = SessionLogger.Instance != null ? SessionLogger.Instance.GetGraphSvg() : "";
        string quitButtonHtml = BuildQuitButtonHtml();

        return $@"
        <html>
        <head>
            <title>Stroke Rehab Dashboard</title>
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

                .music-info {{
                    display: flex;
                    gap: 18px;
                    margin-bottom: 14px;
                    color: #607789;
                    font-size: 16px;
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

                .quit-button {{
                    background: #333333;
                }}

                .graph-box {{
                    background: white;
                    border-radius: 12px;
                    overflow: hidden;
                    border: 1px solid #e4eaee;
                }}

                .log-panel {{
                    height: 420px;
                    overflow: hidden;
                }}

                .log-scroll {{
                    height: 330px;
                    overflow-y: auto;
                    position: relative;
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
                    z-index: 10;
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
                <div class='status'>Current scene: <span id='scene-status-value'>{currentSceneIndex}</span></div>
            </div>

            <div class='container'>
                <div>
                    <div class='panel'>
                        <h2>Scene Control</h2>
                        <p class='small-note'>Therapist-controlled navigation for the current session.</p>

                        <form class='scene-form' action='/scene' method='get'>
                            <button class='scene-button' name='index' value='0'>Main Menu</button>
                            <button class='scene-button' name='index' value='1'>Start Room</button>
                            <button class='scene-button' name='index' value='2'>Cleaning Room</button>
                            <button class='scene-button' name='index' value='3'>Balloon Room</button>
                        </form>
                    </div>

                    <div class='panel'>
                        <h2>Music Control</h2>
                        <p class='small-note'>Ambient music control for the patient screen.</p>

                        <div class='music-info'>
                            <div><b>Status:</b> <span id='music-status-value'>{cachedMusicStatus}</span></div>
                            <div><b>Volume:</b> <span id='music-volume-value'>{cachedMusicVolume}%</span></div>
                        </div>

                        <form class='scene-form' action='/music' method='get'>
                            <button class='scene-button' name='action' value='on'>Music On</button>
                            <button class='scene-button' name='action' value='off'>Music Off</button>
                            <button class='scene-button' name='action' value='down'>Volume -</button>
                            <button class='scene-button' name='action' value='up'>Volume +</button>
                        </form>
                    </div>

                    <div class='panel'>
                        <h2>Patient Data</h2>
                        <div class='stats-grid' id='stats-grid'>
                            {stats}
                        </div>
                    </div>
                </div>

                <div>
                    <div class='action-row'>
                        <form action='/stop' method='get'>
                            <button class='action-button stop-button'>Stop</button>
                        </form>

                        <form action='/save' method='get'>
                            <button class='action-button save-button'>Save CSV</button>
                        </form>

                        <div id='quit-button-holder'>
                            {quitButtonHtml}
                        </div>
                    </div>

                    <div class='panel'>
                        <h2>Control Performance Graph</h2>
                        <p class='small-note'>Last 40 recorded samples, shown as percentage control over time.</p>
                        <div class='graph-box' id='graph-box'>
                            {graph}
                        </div>
                    </div>

                    <div class='panel log-panel'>
                        <h2>Session Log</h2>
                        <div class='log-scroll' id='log-scroll'>
                            {log}
                        </div>
                    </div>
                </div>
            </div>

            <script>
                async function updateDashboard() {{
                    try {{
                        const response = await fetch('/data');
                        const html = await response.text();

                        const parser = new DOMParser();
                        const doc = parser.parseFromString(html, 'text/html');

                        document.getElementById('scene-status-value').innerHTML =
                            doc.querySelector('#scene-status').innerHTML;

                        document.getElementById('music-status-value').innerHTML =
                            doc.querySelector('#music-status').innerHTML;

                        document.getElementById('music-volume-value').innerHTML =
                            doc.querySelector('#music-volume').innerHTML;

                        document.getElementById('stats-grid').innerHTML =
                            doc.querySelector('#stats-content').innerHTML;

                        document.getElementById('graph-box').innerHTML =
                            doc.querySelector('#graph-content').innerHTML;

                        document.getElementById('log-scroll').innerHTML =
                            doc.querySelector('#log-content').innerHTML;

                        document.getElementById('quit-button-holder').innerHTML =
                            doc.querySelector('#quit-content').innerHTML;
                    }} catch (e) {{
                        console.log('Dashboard update failed', e);
                    }}
                }}

                setInterval(updateDashboard, 2000);
            </script>
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
        response.Redirect("/");
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