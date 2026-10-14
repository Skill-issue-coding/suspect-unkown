using TMPro;
using UnityEngine;

// The job's elapsed time on the job card, as mm:ss from the level's start. Stops when the job ends, so the
// final time stays on screen behind the newspaper. Rewrites its text only when the shown second changes
public class JobClock : MonoBehaviour
{
    [SerializeField, Tooltip("Shows the elapsed time as mm:ss")]
    private TMP_Text label;

    private int shownSeconds = -1;

    private void Awake()
    {
        if (label == null)
        {
            Debug.LogWarning($"{name} needs a label; assign it in JobHud.prefab", this);
            enabled = false;
        }
    }

    // Continuous: time passes every frame, but the text changes once a second at most
    private void Update()
    {
        if (JobOutcome.HasEnded)
        {
            enabled = false;
            return;
        }

        int seconds = (int)Time.timeSinceLevelLoad;
        if (seconds == shownSeconds) return;

        shownSeconds = seconds;
        // SetText with arguments formats without allocating a string. Floats, because int arguments are
        // ambiguous with a span overload
        label.SetText("{0:00}:{1:00}", seconds / 60, (float)(seconds % 60));
    }
}
