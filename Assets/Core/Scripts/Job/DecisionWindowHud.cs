using UnityEngine;
using UnityEngine.UI;

// A ring beside the HUD's status text that shows the fullest decision window leaning toward a call: how
// close someone is to calling the police. Fills while they can see the player, drains while not, and turns
// red and pulses once a call is under way. Hidden while no such window is open. Reads JobAlert, so it works
// with the NPC off screen. On JobHud.prefab's root
public class DecisionWindowHud : MonoBehaviour
{
    [SerializeField, Tooltip("Radial filled image: the window's progress")]
    private Image ring;

    [SerializeField, Tooltip("Faint full ring behind it, so the empty part reads as a track")]
    private Image track;

    [SerializeField, Tooltip("Ring color while someone is deciding whether to call")]
    private Color decidingColor = new Color(1f, 0.5f, 0.2f, 1f);

    [SerializeField, Tooltip("Ring color once a call is under way")]
    private Color callingColor = new Color(1f, 0.3f, 0.27f, 1f);

    [SerializeField, Tooltip("Pulses per second while calling")]
    private float callingPulseRate = 4f;

    private bool shown = true;

    private void Awake()
    {
        if (ring == null || track == null)
        {
            Debug.LogWarning($"{name} needs its ring and track images; assign them in JobHud.prefab", this);
            enabled = false;
            return;
        }
        SetShown(false);
    }

    // Continuous while a window is open: it moves every frame. Otherwise one check and out
    private void LateUpdate()
    {
        bool open = JobAlert.AnyWindowOpen;
        if (open != shown) SetShown(open);
        if (!open) return;

        JobAlert.Window(out float progress, out bool calling);
        ring.fillAmount = progress;
        if (calling)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * callingPulseRate * Mathf.PI * 2f);
            ring.color = Color.Lerp(callingColor, Color.white, 0.35f * pulse);
        }
        else ring.color = decidingColor;
    }

    private void SetShown(bool show)
    {
        shown = show;
        ring.enabled = show;
        track.enabled = show;
    }
}
