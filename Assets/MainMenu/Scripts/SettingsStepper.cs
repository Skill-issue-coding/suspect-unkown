using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One "< value >" settings row on the concept settings board. Steppers instead of sliders
// because they read well on a world-space panel and work with a gamepad later.
// Applies the real setting so the board is not a dead mock-up.
public class SettingsStepper : MonoBehaviour
{
    public enum Setting { MasterVolume, Fullscreen }

    [Tooltip("Which setting this row changes")]
    [SerializeField] private Setting setting;
    [Tooltip("Arrow buttons")]
    [SerializeField] private Button previous, next;
    [Tooltip("Current value label")]
    [SerializeField] private TMP_Text valueLabel;

    private int value;

    private void Awake()
    {
        if (previous == null || next == null || valueLabel == null)
        {
            Debug.LogWarning($"{name} is missing its buttons or label", this);
            enabled = false;
            return;
        }
        previous.onClick.AddListener(() => Step(-1));
        next.onClick.AddListener(() => Step(1));

        value = setting switch
        {
            Setting.MasterVolume => Mathf.RoundToInt(MasterVolume.Value * 10f),
            _ => Screen.fullScreen ? 1 : 0,
        };
        Refresh();
    }

    private void Step(int direction)
    {
        switch (setting)
        {
            case Setting.MasterVolume:
                value = Mathf.Clamp(value + direction, 0, 10);
                MasterVolume.Set(value / 10f);
                break;
            case Setting.Fullscreen:
                value = 1 - value;
                if (value == 1)
                    Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
                else
                    Screen.SetResolution(1600, 900, FullScreenMode.Windowed);
                break;
        }
        Refresh();
    }

    private void Refresh()
    {
        valueLabel.text = setting switch
        {
            Setting.MasterVolume => $"{value * 10}%",
            _ => value == 1 ? "ON" : "OFF",
        };
    }
}
