using UnityEngine;

// Reports the clerk's decision window to JobAlert while it is open, so the HUD can show how close they are
// to calling even when the clerk is off screen. Closes the report when the window closes or the clerk goes
// away. Reads ResidentBehaviour and decides nothing
[RequireComponent(typeof(ResidentBehaviour))]
public class ResidentWindowReporter : MonoBehaviour
{
    private ResidentBehaviour resident;
    private bool open;

    private void Awake() => resident = GetComponent<ResidentBehaviour>();

    private void OnDisable() => Close();

    // Continuous while the window is open: progress moves every frame. Otherwise one check and out
    private void LateUpdate()
    {
        bool nowOpen = resident.IsDeliberating || resident.IsCalling;
        if (!nowOpen)
        {
            if (open) Close();
            return;
        }

        open = true;
        JobAlert.ReportWindow(this, resident.WindowProgress, resident.IsCalling);
    }

    private void Close()
    {
        open = false;
        JobAlert.CloseWindow(this);
    }
}
