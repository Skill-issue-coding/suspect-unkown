using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The job HUD's status tag (top right): UNDETECTED, SUSPICIOUS, SEARCHING or ALARM, the highest alert any
// NPC reports through JobAlert, in a colour per level. Changes only on JobAlert.Changed, with a small punch
// so a rise catches the eye. On JobHud.prefab's root
public class AlertStatusHud : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField, Tooltip("The tag itself; punched when the level changes")]
    private RectTransform tag;

    [SerializeField, Tooltip("Shows the level's name")]
    private TMP_Text label;

    [SerializeField, Tooltip("The eye icon")]
    private Image icon;

    [SerializeField, Tooltip("The tag's outline")]
    private Image border;

    [Header("Look, per level: Undetected, Suspicious, Searching, Alarm")]
    [SerializeField, Tooltip("Text and icon colour per level, in JobAlertLevel order")]
    private Color[] colors =
    {
        new Color(0.93f, 0.91f, 0.86f, 0.5f),
        new Color(1f, 0.71f, 0.28f, 1f),
        new Color(1f, 0.5f, 0.2f, 1f),
        new Color(1f, 0.3f, 0.27f, 1f),
    };

    [SerializeField, Tooltip("Text per level, in JobAlertLevel order")]
    private string[] texts = { "UNDETECTED", "SUSPICIOUS", "SEARCHING", "ALARM" };

    [SerializeField, Range(0f, 1f), Tooltip("Outline alpha, as a fraction of the level's colour")]
    private float borderAlpha = 0.55f;

    [SerializeField, Tooltip("Scale added at the peak of the punch when the level rises")]
    private float punchStrength = 0.12f;

    private Tween punch;

    private void Awake()
    {
        if (tag == null || label == null || icon == null || border == null || colors.Length < 4 || texts.Length < 4)
        {
            Debug.LogWarning($"{name} needs its tag, label, icon, border and four colours and texts; assign them in JobHud.prefab", this);
            enabled = false;
        }
    }

    private void OnEnable() => JobAlert.Changed += OnChanged;

    private void OnDisable() => JobAlert.Changed -= OnChanged;

    private void Start() => Show(JobAlert.Level);

    private void OnChanged(JobAlertLevel from, JobAlertLevel to)
    {
        Show(to);
        if (to <= from) return;
        punch.Complete();
        punch = Tween.PunchScale(tag, Vector3.one * punchStrength, duration: 0.3f, frequency: 6);
    }

    private void Show(JobAlertLevel level)
    {
        int i = (int)level;
        Color color = colors[i];
        label.text = texts[i];
        label.color = color;
        icon.color = color;
        border.color = new Color(color.r, color.g, color.b, color.a * borderAlpha);
    }
}
