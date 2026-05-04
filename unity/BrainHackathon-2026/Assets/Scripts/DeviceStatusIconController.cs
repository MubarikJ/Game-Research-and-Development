using UnityEngine;
using UnityEngine.UI;

public class DeviceStatusIconController : MonoBehaviour
{
    [Header("Icons")]
    public Image brainIcon;
    public Image eyeIcon;

    [Header("Device References")]
    public SAPCReceiver sapcReceiver;
    public TobiiSignal tobiiSignal;

    private Color connectedColor;
    private Color disconnectedColor = Color.black;

    void Start()
    {
        ColorUtility.TryParseHtmlString("#489709", out connectedColor);
        UpdateIcons();
    }

    void Update()
    {
        UpdateIcons();
    }

    void UpdateIcons()
    {
        bool unicornConnected = sapcReceiver != null && sapcReceiver.HasSignal;
        bool tobiiConnected = tobiiSignal != null && tobiiSignal.HasTobiiSignal;

        if (brainIcon != null)
            brainIcon.color = unicornConnected ? connectedColor : disconnectedColor;

        if (eyeIcon != null)
            eyeIcon.color = tobiiConnected ? connectedColor : disconnectedColor;
    }
}