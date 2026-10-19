using System;

// What a civilian is doing, for the readers that do not care which kind of civilian it is: the suspicion
// indicator's icon and the HUD's reaction reporter. CivilianBehaviour (customers and staff) and
// ResidentBehaviour (the apartment clerk) implement it. Found with GetComponentInParent, which works on
// interfaces; the reference is a plain C# one, so it misses Unity's destroyed-object check, which is safe
// only because the readers live on the same NPC and go with it
public interface ICivilianActivity
{
    CivilianActivity Activity { get; }

    // From and to, once per change, including the ones the decision window makes (Deliberating, Calling)
    event Action<CivilianActivity, CivilianActivity> ActivityChanged;
}
