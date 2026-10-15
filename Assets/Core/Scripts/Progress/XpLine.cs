// One source of XP on the debrief's reputation view: what it was for, a detail line, and the XP it gave.
// Built by XpRules from a JobResult each time it is needed, never saved
public struct XpLine
{
    public string label;
    public string detail;
    public int xp;
}