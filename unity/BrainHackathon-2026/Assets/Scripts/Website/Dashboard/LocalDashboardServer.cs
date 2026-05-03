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
            <title>Login</title>
            <style>
                body { font-family: Arial; background: #111; color: white; display: flex; align-items: center; justify-content: center; height: 100vh; }
                .box { background: #ddd; color: black; padding: 40px; border-radius: 10px; text-align: center; }
                input { font-size: 20px; padding: 10px; margin: 10px; }
                button { font-size: 20px; padding: 10px 30px; }
            </style>
        </head>
        <body>
            <div class='box'>
                <h1>Doctor Login</h1>
                <form action='/' method='get'>
                    <input type='password' name='password' placeholder='Password' />
                    <br/>
                    <button type='submit'>Enter</button>
                </form>
            </div>
        </body>
        </html>";
    }

    string BuildDashboardHtml()
    {
        string stats = SessionLogger.Instance != null ? SessionLogger.Instance.GetStatsHtml() : "No logger found.";
        string log = SessionLogger.Instance != null ? SessionLogger.Instance.GetLogHtml() : "No logger found.";

        return $@"
        <html>
        <head>
            <title>Stroke Rehab Dashboard</title>
            <meta http-equiv='refresh' content='2; url=/?password=test'>
            <style>
                body {{
                    margin: 0;
                    padding: 20px;
                    background: black;
                    font-family: Arial;
                }}

                .title {{
                    background: #d9d9d9;
                    color: white;
                    text-align: center;
                    font-size: 52px;
                    padding: 25px;
                    margin-bottom: 15px;
                }}

                .grid {{
                    display: grid;
                    grid-template-columns: 48% 52%;
                    gap: 15px;
                }}

                .panel {{
                    background: #d9d9d9;
                    padding: 25px;
                    margin-bottom: 15px;
                }}

                h2 {{
                    color: white;
                    font-size: 44px;
                    text-align: center;
                    margin: 0 0 25px 0;
                }}

                .scene-buttons {{
                    display: flex;
                    justify-content: space-around;
                }}

                .circle {{
                    width: 120px;
                    height: 120px;
                    border-radius: 50%;
                    border: 4px solid black;
                    background: #cfcfcf;
                    font-size: 24px;
                    cursor: pointer;
                }}

                .top-buttons {{
                    display: flex;
                    gap: 15px;
                    justify-content: flex-start;
                    margin-bottom: 15px;
                }}

                .rect {{
                    width: 220px;
                    height: 70px;
                    background: #bdbdbd;
                    color: white;
                    border: none;
                    font-size: 26px;
                    cursor: pointer;
                }}

                .data-panel {{
                    min-height: 520px;
                }}

                .metric {{
                    display: flex;
                    justify-content: space-between;
                    font-size: 24px;
                    color: white;
                    padding: 8px 40px;
                }}

                .metric b {{
                    color: black;
                }}

                .log-panel {{
                    background: white;
                    height: 650px;
                    padding: 25px;
                    font-size: 18px;
                    overflow-y: auto;
                }}

                table {{
                    width: 100%;
                    border-collapse: collapse;
                }}

                th, td {{
                    border-bottom: 1px solid #ccc;
                    padding: 8px;
                    text-align: left;
                }}
            </style>
        </head>

        <body>
            <div class='title'>Patient Session Dashboard</div>

            <div class='grid'>
                <div>
                    <div class='panel'>
                        <h2>Scene Control</h2>
                        <div class='scene-buttons'>
                            <form action='/scene' method='get'>
                                <input type='hidden' name='password' value='test'>
                                <button class='circle' name='index' value='0'>Main<br>Menu</button>
                                <button class='circle' name='index' value='1'>Game<br>Scene 1</button>
                                <button class='circle' name='index' value='2'>Game<br>Scene 2</button>
                            </form>
                        </div>
                    </div>

                    <div class='panel data-panel'>
                        <h2>Patient Data</h2>
                        {stats}
                    </div>
                </div>

                <div>
                    <div class='top-buttons'>
                        <form action='/stop' method='get'>
                            <input type='hidden' name='password' value='test'>
                            <button class='rect'>Stop</button>
                        </form>

                        <form action='/save' method='get'>
                            <input type='hidden' name='password' value='test'>
                            <button class='rect'>Save CSV</button>
                        </form>
                    </div>

                    <div class='log-panel'>
                        <h2 style='color:black;'>Log</h2>
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