// One candidate the planner switched on: which one, what it added to coverage, how fast it drains
// (1 to 2), and the change-log line explaining why
public readonly struct SecurityPick
{
    public readonly int candidate;
    public readonly float gain;
    public readonly float decayMultiplier;
    public readonly string explanation;

    public SecurityPick(int candidate, float gain, float decayMultiplier, string explanation)
    {
        this.candidate = candidate;
        this.gain = gain;
        this.decayMultiplier = decayMultiplier;
        this.explanation = explanation;
    }
}
