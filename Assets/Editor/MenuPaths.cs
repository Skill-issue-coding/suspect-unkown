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

    public const string MainMenu = Root + "Main Menu/";
    public const string MainMenuBuildConcept = MainMenu + "Build Concept Scene";

    public const string Interactables = Root + "Interactables/";
    public const string InteractablesBuildLockpickHud = Interactables + "Build Lockpick HUD";

    // Priorities: blocks of 100 so there is always a separator between groups
    public const int WindowsPriority = 0;
    public const int PlayerPriority = 100;
    public const int NpcPriority = 200;
    public const int MainMenuPriority = 300;
    public const int InteractablesPriority = 400;
}
