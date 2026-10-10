using UnityEngine;
using UnityEngine.UI;
using TMPro;

// One "< value >" settings row on the concept settings board. Steppers instead of sliders
// because they read well on a world-space panel and work with a gamepad later.
// Applies the real setting so the board is not a dead mock-up.
public class SettingsStepper : MonoBehaviour
{
    public enum Setting { MasterVolume, Fullscreen, Quality }

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
            Setting.MasterVolume => Mathf.RoundToInt(AudioListener.volume * 10f),
            Setting.Fullscreen => Screen.fullScreen ? 1 : 0,
            _ => QualitySettings.GetQualityLevel()
        };
        Refresh();
    }

    private void Step(int direction)
    {
        switch (setting)
        {
            case Setting.MasterVolume:
                value = Mathf.Clamp(value + direction, 0, 10);
                AudioListener.volume = value / 10f;
                break;
            case Setting.Fullscreen:
                value = 1 - value;
                Screen.fullScreen = value == 1;
                break;
            case Setting.Quality:
                value = (int)Mathf.Repeat(value + direction, QualitySettings.names.Length);
                QualitySettings.SetQualityLevel(value, true);
                break;
        }
        Refresh();
    }

    private void Refresh()
    {
        valueLabel.text = setting switch
        {
            Setting.MasterVolume => $"{value * 10}%",
            Setting.Fullscreen => value == 1 ? "ON" : "OFF",
            _ => QualitySettings.names[value].ToUpperInvariant()
        };
    }
}
