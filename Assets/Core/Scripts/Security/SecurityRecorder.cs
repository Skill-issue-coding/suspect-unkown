using UnityEngine;
using UnityEngine.SceneManagement;

// Teaches security from completed runs only (decision 24): on Completed it decays this location's stored
// counts, adds the run from JobLog and saves. A failed run never reaches it, so failing never makes the next
// try harder. A record saved against another layout is replaced, with a warning. The editor's Fake Runs
// goes through the same RecordRun, so a faked run decays exactly as a real one
[RequireComponent(typeof(JobLayout))]
public class SecurityRecorder : MonoBehaviour
{
    [SerializeField, Range(0f, 1f), Tooltip("Weight kept by the stored counts per completed run; 0.8 means a run five jobs ago counts a third")]
    private float decay = 0.8f;

    private JobLayout layout;

    public float Decay => decay;

    private void Awake() => layout = GetComponent<JobLayout>();

    private void OnEnable() => JobOutcome.Completed += OnCompleted;

    private void OnDisable() => JobOutcome.Completed -= OnCompleted;

    // Shared by the recorder and the editor's Fake Runs: decays the location's record, adds the run
    // now in JobLog and saves. A record that no longer fits the layout is replaced
    public static void RecordRun(string location, JobLayout layout, float decay, Object context = null)
    {
        if (!JobLog.IsActive) return;

        SecuritySave save = SecurityStore.Load();
        SecurityRecord record = save.Find(location);

        if (record != null && !record.Fits(layout))
        {
            Debug.LogWarning($"Security: the layout of {location} changed since its record was saved; starting over", context);
            save.locations.Remove(record);
            record = null;
        }

        if (record == null)
        {
            record = SecurityRecord.For(location, layout);
            save.locations.Add(record);
        }

        record.AddRun(decay);
        SecurityStore.Save(save);
    }

    private void OnCompleted() => RecordRun(SceneManager.GetActiveScene().name, layout, decay, this);
}
