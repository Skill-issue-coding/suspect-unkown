using UnityEngine;

// Reports an NPC's decision window to JobAlert while it is open, so the HUD can show how close they are
// to calling even when they are off screen. Closes the report when the window closes or the NPC goes
// away. Reads NpcDecisionWindow and decides nothing
[RequireComponent(typeof(NpcDecisionWindow))]
public class NpcWindowReporter : MonoBehaviour
{
    private NpcDecisionWindow window;
    private bool open;

    private void Awake() => window = GetComponent<NpcDecisionWindow>();

    private void OnDisable() => Close();

    // Continuous while the window is open: progress moves every frame. Otherwise one check and out
    private void LateUpdate()
    {
        bool nowOpen = window.IsDeliberating || window.IsCalling;
        if (!nowOpen)
        {
            if (open) Close();
            return;
        }

        open = true;
        JobAlert.ReportWindow(this, window.Progress, window.IsCalling);
    }

    private void Close()
    {
        open = false;
        JobAlert.CloseWindow(this);
    }
}
