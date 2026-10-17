using UnityEngine;
using UnityEngine.SceneManagement;

// Teaches security from completed runs only (decision 24): on Completed it decays this location's stored
// counts, adds the run from JobLog and saves. A failed run never reaches it, so failing never makes the next
// try harder. A record saved against another layout is replaced, with a warning
[RequireComponent(typeof(JobLayout))]
public class SecurityRecorder : MonoBehaviour
{
    [SerializeField, Range(0f, 1f), Tooltip("Weight kept by the stored counts per completed run; 0.8 means a run five jobs ago counts a third")]
    private float decay = 0.8f;

    private JobLayout layout;

    private void Awake() => layout = GetComponent<JobLayout>();

    private void OnEnable() => JobOutcome.Completed += OnCompleted;

    private void OnDisable() => JobOutcome.Completed -= OnCompleted;

    private void OnCompleted()
    {
        if (!JobLog.IsActive) return;

        SecuritySave save = SecurityStore.Load();
        string location = SceneManager.GetActiveScene().name;
        SecurityRecord record = save.Find(location);

        if (record != null && !record.Fits(layout))
        {
            Debug.LogWarning($"Security: the layout of {location} changed since its record was saved; starting over", this);
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
}
