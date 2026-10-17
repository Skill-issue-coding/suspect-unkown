/// <summary>
/// Central place for every "Tools/Suspect Unknown" menu path and priority.
/// Use a submenu ("Player/...") to group related tools. A gap of 11 or more in
/// priority between neighbouring items makes Unity draw a separator line there.
/// </summary>
public static class MenuPaths
{
    public const string Root = "Tools/Suspect Unknown/";

    // Top level: windows
    public const string NightLab = Root + "Night Lab";

    // Submenus
    public const string Player = Root + "Player/";
    public const string PlayerRebuildLocomotion = Player + "Rebuild Locomotion";
    public const string PlayerMapPerceptionPoints = Player + "Map Perception Points";

    public const string Npc = Root + "NPC/";
    public const string NpcRebuildLocomotion = Npc + "Rebuild Locomotion";

    public const string GuardRebuildLocomotion = Npc + "Guard/" + "Rebuild Locomotion";
    public const string ClerkRebuildLocomotion = Npc + "Clerk/" + "Rebuild Locomotion";

    public const string Progress = Root + "Progress/";
    public const string ProgressLog = Progress + "Log Save";
    public const string ProgressReveal = Progress + "Reveal Save Folder";
    public const string ProgressDelete = Progress + "Delete Save";
    public const string ProgressUnlockAll = Progress + "Unlock All";
    public const string ProgressFakeDebrief = Progress + "Fake Debrief";

    public const string Police = Root + "Police Record/";
    public const string PoliceLog = Police + "Log Record";
    public const string PoliceDelete = Police + "Delete Record";

    public const string ActionRecord = Root + "Action Record/";
    public const string ActionRecordLog = ActionRecord + "Log Record";
    public const string ActionRecordReveal = ActionRecord + "Reveal Save Folder";
    public const string ActionRecordDelete = ActionRecord + "Delete Record";
    public const string ActionRecordFakeRuns = ActionRecord + "Fake Runs Window";

    // Priorities: blocks of 100 so there is always a separator between groups
    public const int WindowsPriority = 0;
    public const int PlayerPriority = 100;
    public const int NpcPriority = 200;
    public const int ProgressPriority = 300;
    public const int PolicePriority = 400;
    public const int ActionRecordPriority = 500;
}
