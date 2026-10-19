using UnityEngine;

// Reports an NPC's decision window to JobAlert while it can end in a call, so the HUD's ring shows how
// close someone is to calling the police, even with the NPC off screen: it fills with the window while
// they lean toward a call, and sits full once the call is under way. A window leaning toward another
// reaction is shown by the NPC's own indicator only. Closes the report when the window closes or the NPC
// goes away. Reads NpcDecisionWindow and decides nothing
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
        // The pending outcome is fixed when the window opens, so a window counts for the ring for its whole
        // life or not at all. The resident has no tree, so its windows always lean toward a call
        bool nowOpen = window.IsCalling
            || (window.IsDeliberating && window.PendingOutcome == CivilianActivity.Calling);
        if (!nowOpen)
        {
            if (open) Close();
            return;
        }

        open = true;
        JobAlert.ReportWindow(this, window.IsCalling ? 1f : window.Progress, window.IsCalling);
    }

    private void Close()
    {
        open = false;
        JobAlert.CloseWindow(this);
    }
}
