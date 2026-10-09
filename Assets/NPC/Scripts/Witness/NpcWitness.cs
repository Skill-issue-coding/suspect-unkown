using UnityEngine;

// How much of the player this NPC has seen: the first slice of the witness model (decision 7, M6 step 2).
// Each perception tick that saw the player stands for one tick interval and adds that time, weighted by
// the observation's quality, to Exposure: seconds of a perfect view. Separate from suspicion, which is
// alarm: a calm civilian who watched for ten seconds is the dangerous witness (decision 22). Feature
// slots, observer skill and memory decay arrive in M7
[RequireComponent(typeof(NpcPerception))]
public class NpcWitness : MonoBehaviour
{
    private NpcPerception perception;

    public float Exposure { get; private set; }
    public float SeenTime { get; private set; }
    public float BestQuality { get; private set; }

    private void Awake() => perception = GetComponent<NpcPerception>();

    private void OnEnable() { if (perception != null) perception.Perceived += OnPerceived; }

    private void OnDisable() { if (perception != null) perception.Perceived -= OnPerceived; }

    private void OnPerceived(Observation observation)
    {
        float interval = 1f / perception.Archetype.TickRate;
        float quality = observation.Strength;

        Exposure += quality * interval;
        SeenTime += interval;
        if (quality > BestQuality) BestQuality = quality;
    }
}
