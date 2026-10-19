using UnityEngine;

// What the tree branches on, gathered once when the window opens. 
// Severity is the archetype's abnormality, so it means the same as 
// in suspicion (decision 6).
public struct ReactionContext
{
    public float severity; // 0 to 1
    public float playerDistance; // metres

    public readonly float Get(ReactionInput input) => input switch
    {
        ReactionInput.Severity => severity,
        ReactionInput.PlayerDistance => playerDistance,
        _ => 0f,
    };

    public static ReactionContext Build(Vector3 position, NpcPerception perception, NpcSuspicion suspicion)
    {
        ReactionContext context = default;
        if (perception.TryGetLatest(out Observation o))
        {
            context.severity = o.abnormality;
            context.playerDistance = Vector3.Distance(position, o.playerPosition);
        }
        // Rare: only a sighting can alert (sound and anomaly floors stay below alertedAt)
        else if (suspicion.HasStimulus)
            context.playerDistance = Vector3.Distance(position, suspicion.LastStimulus);

        return context;
    }

    public override readonly string ToString() => $"severity {severity:F2}, player {playerDistance:F1} m";
}
