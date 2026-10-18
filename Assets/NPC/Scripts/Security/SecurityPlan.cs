using System.Collections.Generic;

// What the planner hands the level at load (roadmap.md, System boundaries): how many guards to spawn and
// which candidate POIs to switch on, in pick order
public class SecurityPlan
{
    public int Guards { get; }
    public IReadOnlyList<SecurityPick> Picks { get; }
    public IReadOnlyList<float> StandaloneGains { get; }

    public SecurityPlan(int guards, IReadOnlyList<SecurityPick> picks, IReadOnlyList<float> standaloneGains)
    {
        Guards = guards;
        Picks = picks;
        StandaloneGains = standaloneGains;
    }
}
