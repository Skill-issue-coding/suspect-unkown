using System.Text;
using UnityEngine;

// On-screen readout of this run's job log: every transition and entrance used so far, by name. The text is
// rebuilt only when the log records something, never per frame; OnGUI just draws the cached string
[RequireComponent(typeof(JobLayout))]
public class JobLogReadout : MonoBehaviour
{
    [SerializeField, Tooltip("Show the readout")]
    private bool show = true;

    [SerializeField, Tooltip("Top-left corner of the readout, in pixels")]
    private Vector2 position = new(10f, 300f);

    private JobLayout layout;
    private string text = "";

    private readonly StringBuilder builder = new();

    private void Awake() => layout = GetComponent<JobLayout>();

    private void OnEnable() => JobLog.Recorded += Rebuild;

    private void OnDisable() => JobLog.Recorded -= Rebuild;

    private void Start() => Rebuild();

    private void Rebuild()
    {
        builder.Clear();

        if (!JobLog.IsActive)
        {
            text = "Job log: inactive";
            return;
        }

        builder.AppendLine("JobLog: Transitions");
        for (int from = 0; from < JobLog.RoomCount; from++)
        {
            for (int to = 0; to < JobLog.RoomCount; to++)
            {
                int count = JobLog.Transitions(from, to);
                if (count == 0) continue;
                builder.Append("  ").Append(layout.RoomName(from)).Append(" → ")
                       .Append(layout.RoomName(to)).Append(" ×").Append(count).AppendLine();
            }
        }

        builder.AppendLine("JobLog: Entrances");
        for (int e = 0; e < JobLog.EntranceCount; e++)
        {
            int count = JobLog.Entries(e);
            if (count == 0) continue;
            builder.Append("  ").Append(layout.EntranceName(e)).Append(" ×").Append(count).AppendLine();
        }

        text = builder.ToString();
    }

    private void OnGUI()
    {
        if (show) GUI.Label(new Rect(position, new Vector2(400f, 400f)), text);
    }
}
