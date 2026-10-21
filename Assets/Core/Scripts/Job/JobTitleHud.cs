using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// The job card's heading: "JOB 03 · THRIFT STORE" above the level's address. The number is this run at this
// level, counted from the save's completed jobs. The name and address differ per level, so they are set on
// each scene's JobHud instance. Written once in Start: nothing here changes during a job
public class JobTitleHud : MonoBehaviour
{
    [SerializeField, Tooltip("The small line above the address")]
    private TMP_Text jobLabel;

    [SerializeField, Tooltip("The address line")]
    private TMP_Text addressLabel;

    [SerializeField, Tooltip("This level's name after the job number. Override it on each scene's JobHud")]
    private string jobName = "Tutorial";

    [SerializeField, Tooltip("This level's address. Override it on each scene's JobHud")]
    private string address = "14 Elm St, Apartment";

    private void Awake()
    {
        if (jobLabel == null)
        {
            Debug.LogWarning($"{name} jobLabel not set, job hud will not display correct job title", this);
            enabled = false;
        }

        if (addressLabel == null)
        {
            Debug.LogWarning($"{name} adressLabel not set, job hud will not display correct job adress", this);
            enabled = false;
        }
    }

    private void Start()
    {
        int run = ProgressStore.Load().TimesCompleted(SceneManager.GetActiveScene().name) + 1;
        jobLabel.text = $"JOB {run:00}  \u00B7  {jobName.ToUpperInvariant()}";
        addressLabel.text = address;
    }
}
