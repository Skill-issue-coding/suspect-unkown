// How alarmed the job is, as the HUD's status tag shows it. JobAlert compares levels by declaration
// order and keeps the highest, so keep them in rising order. Mirrors NpcAlertState one to one
public enum JobAlertLevel
{
    Undetected,
    Suspicious,
    Searching,
    Alarm,
}
