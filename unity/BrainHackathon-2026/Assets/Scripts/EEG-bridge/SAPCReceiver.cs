using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class SAPCReceiver : MonoBehaviour
{
    [Header("Network")]
    public int port = 1000;

    [Header("Debug")]
    public bool verboseDebug = true;
    public float debugLogRateHz = 10.0f;
    public bool logInvalidPackets = true;

    [Header("Packet parsing")]
    public int floatValueIndex = -1;
    public float autoEdgeEpsilon = 0.01f;

    [Header("Signal Status")]
    public float signalTimeoutSeconds = 3f;

    public float CurrentValue { get; private set; } = 0.5f;
    public bool HasSignal { get; private set; } = false;

    private Thread receiveThread;
    private UdpClient client;

    private float sapcValue = 0.5f;
    private readonly object lockObject = new object();
    private volatile bool isRunning = false;

    private float latestReceivedValue = 0.5f;
    private bool hasNewValue = false;
    private bool hasInvalidPacket = false;
    private string latestInvalidPacket = "";

    private float nextLogTime = 0f;
    private float sessionLogTimer = 0f;
    private float lastSignalTime = -999f;
    private bool receivedSignalThisFrame = false;

    void Start()
    {
        isRunning = true;

        try
        {
            client = new UdpClient(port);
            client.Client.ReceiveTimeout = 500;
        }
        catch (System.Exception e)
        {
            Debug.LogError("SAPCReceiver: could not open UDP port " + port + " — " + e.Message);
            return;
        }

        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log("SAPCReceiver: listening on UDP :" + port);
    }

    private void ReceiveData()
    {
        while (isRunning)
        {
            try
            {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = client.Receive(ref anyIP);

                string packetForDebug;
                float parsed;
                bool ok = TryParseIncomingPacket(data, out parsed, out packetForDebug);

                if (ok && !float.IsNaN(parsed))
                {
                    parsed = Mathf.Clamp01(parsed);

                    lock (lockObject)
                    {
                        sapcValue = parsed;
                        latestReceivedValue = parsed;
                        hasNewValue = true;
                        receivedSignalThisFrame = true;
                    }
                }
                else
                {
                    lock (lockObject)
                    {
                        latestInvalidPacket = packetForDebug;
                        hasInvalidPacket = true;
                    }
                }
            }
            catch (SocketException)
            {
                // Receive timeout — normal
            }
            catch (System.ObjectDisposedException)
            {
                break;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("SAPCReceiver: " + e.Message);
            }
        }
    }

    void Update()
    {
        float target;
        float receivedForLog = 0f;
        bool shouldLogValue = false;
        bool shouldLogInvalid = false;
        string invalidText = "";

        lock (lockObject)
        {
            target = sapcValue;

            if (receivedSignalThisFrame)
            {
                lastSignalTime = Time.time;
                receivedSignalThisFrame = false;
            }

            if (hasNewValue)
            {
                receivedForLog = latestReceivedValue;
                shouldLogValue = true;
                hasNewValue = false;
            }

            if (hasInvalidPacket)
            {
                invalidText = latestInvalidPacket;
                shouldLogInvalid = true;
                hasInvalidPacket = false;
            }
        }

        CurrentValue = target;
        HasSignal = Time.time - lastSignalTime <= signalTimeoutSeconds;

        if (verboseDebug && shouldLogValue)
        {
            if (debugLogRateHz <= 0f || Time.unscaledTime >= nextLogTime)
            {
                Debug.Log($"EEG: {receivedForLog:F4}");
                nextLogTime = Time.unscaledTime + (debugLogRateHz > 0f ? 1f / debugLogRateHz : 0f);
            }
        }

        if (logInvalidPackets && shouldLogInvalid)
        {
            Debug.LogWarning($"SAPCReceiver: could not parse packet '{invalidText}'");
        }

        sessionLogTimer += Time.deltaTime;

        if (sessionLogTimer >= 1f)
        {
            sessionLogTimer = 0f;

            if (SessionLogger.Instance != null)
            {
                SessionLogger.Instance.LogValue(target);
            }
        }
    }

    private bool TryParseIncomingPacket(byte[] data, out float parsed, out string packetForDebug)
    {
        parsed = 0f;
        packetForDebug = "<empty>";

        if (data == null || data.Length == 0)
            return false;

        string text = Encoding.UTF8.GetString(data).Trim();
        packetForDebug = text;

        if (TryParseFloatText(text, out parsed))
            return true;

        if (data.Length % 4 == 0)
        {
            float parsedArrayValue;
            string parsedArrayDebug;

            if (TryParseFloatArrayPacket(data, out parsedArrayValue, out parsedArrayDebug))
            {
                parsed = parsedArrayValue;
                packetForDebug = parsedArrayDebug;
                return true;
            }
        }

        if (data.Length == 4)
        {
            float littleEndianValue = System.BitConverter.ToSingle(data, 0);
            byte[] reversed = new byte[] { data[3], data[2], data[1], data[0] };
            float bigEndianValue = System.BitConverter.ToSingle(reversed, 0);

            bool littleFinite = IsFinite(littleEndianValue);
            bool bigFinite = IsFinite(bigEndianValue);

            if (littleFinite && littleEndianValue >= 0f && littleEndianValue <= 1f)
            {
                parsed = littleEndianValue;
                packetForDebug = "<binary-f32-le>";
                return true;
            }

            if (bigFinite && bigEndianValue >= 0f && bigEndianValue <= 1f)
            {
                parsed = bigEndianValue;
                packetForDebug = "<binary-f32-be>";
                return true;
            }

            if (littleFinite)
            {
                parsed = littleEndianValue;
                packetForDebug = "<binary-f32-le>";
                return true;
            }

            if (bigFinite)
            {
                parsed = bigEndianValue;
                packetForDebug = "<binary-f32-be>";
                return true;
            }
        }

        packetForDebug = "<hex:" + System.BitConverter.ToString(data) + ">";
        return false;
    }

    private bool TryParseFloatArrayPacket(byte[] data, out float parsed, out string packetForDebug)
    {
        parsed = 0f;
        packetForDebug = "<binary-f32-array-invalid>";

        int count = data.Length / 4;
        if (count <= 0)
            return false;

        float[] values = new float[count];

        for (int i = 0; i < count; i++)
        {
            values[i] = System.BitConverter.ToSingle(data, i * 4);
        }

        int index = SelectControlValueIndex(values);

        if (index < 0)
            return false;

        parsed = values[index];
        packetForDebug = "<binary-f32-array-le count=" + count + " idx=" + index + " val="
            + parsed.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + ">";

        return true;
    }

    private int SelectControlValueIndex(float[] values)
    {
        if (values == null || values.Length == 0)
            return -1;

        if (floatValueIndex >= 0 && floatValueIndex < values.Length)
        {
            if (IsFinite(values[floatValueIndex]))
                return floatValueIndex;
        }

        float edge = Mathf.Clamp01(autoEdgeEpsilon);
        float lower = edge;
        float upper = 1f - edge;

        for (int i = values.Length - 1; i >= 0; i--)
        {
            float value = values[i];

            if (IsFinite(value) && value > lower && value < upper)
                return i;
        }

        for (int i = values.Length - 1; i >= 0; i--)
        {
            float value = values[i];

            if (IsFinite(value) && value >= 0f && value <= 1f)
                return i;
        }

        for (int i = values.Length - 1; i >= 0; i--)
        {
            if (IsFinite(values[i]))
                return i;
        }

        return -1;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool TryParseFloatText(string text, out float parsed)
    {
        bool ok = float.TryParse(
            text,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out parsed
        );

        if (ok)
            return true;

        return float.TryParse(
            text.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out parsed
        );
    }

    void OnApplicationQuit()
    {
        Cleanup();
    }

    void OnDisable()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        isRunning = false;

        if (client != null)
        {
            try { client.Close(); } catch { }
            client = null;
        }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(1000);
            receiveThread = null;
        }
    }
}