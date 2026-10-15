using UnityEngine;

// What this NPC remembers of the player: exposure (seconds of a perfect view, the M6 slice) and, per outfit
// slot, log-odds beliefs about style and color built from every sighting, weighted by its quality
// (concept 6.2) and by how long the sighting has lasted (f_duration). Separate from suspicion, which is
// alarm: a calm civilian who watched for ten seconds is the dangerous witness (decision 22). A poor look can
// be mistaken (W4: colors through a ConfusionMatrix, capped so the truth stays most likely), and every
// belief fades toward the even start at the archetype's memory half-life (W5). The beliefs live in a
// WitnessReport; a copy is handed in at job end (W6)
[RequireComponent(typeof(NpcPerception))]
public class NpcWitness : MonoBehaviour
{
    private static readonly FeatureSlot[] Slots = (FeatureSlot[])System.Enum.GetValues(typeof(FeatureSlot));

    [Header("Evidence")]
    [SerializeField, Tooltip("Log-odds evidence per second of a perfect look (quality 1). Higher learns faster")]
    private float evidencePerSecond = 3f;

    [SerializeField, Tooltip("Seconds of unbroken looking for f_duration to reach about 63%. A glance stays near 0")]
    private float durationTime = 1.5f;

    [SerializeField, Tooltip("A gap longer than this, in seconds, ends a sighting and restarts f_duration")]
    private float sightingGap = 0.5f;

    [SerializeField, Tooltip("Which colors a poor look mistakes for which. Empty: poor looks are never wrong")]
    private ConfusionMatrix colorConfusion;

    [SerializeField, Range(0.01f, 1f), Tooltip("A look at least this good is always right; below it, the worse the look, the more often it is wrong")]
    private float clearLook = 0.35f;

    [SerializeField, Range(0f, 0.6f), Tooltip("Chance a look of quality 0 is wrong. Below about 0.6 the truth stays the most likely recall: a poor witness is vague, not consistently wrong")]
    private float worstWrongChance = 0.5f;

    private float sightingStart = -1f;
    private float lastSeen = -1f;
    private float lastFade;
    private NpcPerception perception;
    private readonly WitnessReport memory = new();

    public float Exposure { get; private set; }
    public float SeenTime { get; private set; }
    public float BestQuality { get; private set; }

    private void Awake()
    {
        perception = GetComponent<NpcPerception>();
    }

    private void OnEnable()
    {
        if (perception != null) perception.Perceived += OnPerceived;
        JobWitnesses.Register(this);
    }

    private void OnDisable()
    {
        if (perception != null) perception.Perceived -= OnPerceived;
        JobWitnesses.Unregister(this);
    }

    private void OnPerceived(Observation observation)
    {
        float interval = 1f / perception.Archetype.TickRate;
        float quality = observation.Strength;

        Exposure += quality * interval;
        SeenTime += interval;
        if (quality > BestQuality) BestQuality = quality;

        if (lastSeen < 0f || observation.timestamp - lastSeen > sightingGap) sightingStart = observation.timestamp;
        lastSeen = observation.timestamp;
        float fDuration = 1f - Mathf.Exp(-(observation.timestamp - sightingStart + interval) / durationTime);

        Forget(observation.timestamp);

        float lookQuality = observation.Strength * fDuration * perception.Archetype.ObserverSkill;   // 0 to 1
        for (int i = 0; i < Slots.Length; i++)
        {
            FeatureSlot slot = Slots[i];
            if (!SlotSeen(slot, observation)) continue;

            float slotQuality = lookQuality * perception.Archetype.FeatureVisibility(slot);
            if (slotQuality <= 0f) continue;

            OutfitFeature seen = Recall(observation.appearance[slot], slotQuality);
            float weight = slotQuality * interval * evidencePerSecond;
            memory.AddEvidence(slot, seen, weight);
        }
    }

    // What a look of this quality takes in: the truth, or (below clearLook, more often the worse the look)
    // a plausible mistake. Style and color are judged separately, so one can be right and the other wrong
    private OutfitFeature Recall(OutfitFeature truth, float quality)
    {
        if (quality >= clearLook) return truth;
        float wrongChance = worstWrongChance * (1f - quality / clearLook);

        OutfitFeature recalled = truth;
        if (colorConfusion != null && Random.value < wrongChance)
            recalled.color = colorConfusion.MistakeFor(truth.color);
        if (Random.value < wrongChance)
            recalled.style = WrongStyle(truth.style);
        return recalled;
    }

    private static int WrongStyle(int style)
    {
        int other = Random.Range(1, WitnessReport.StyleCount);
        return other >= style ? other + 1 : other;          // skips the true style
    }

    // A slot is seen when any of its body regions had a clear line: feet hidden means nothing about shoes.
    // The same for every observer, so a rule here rather than archetype tuning
    private static bool SlotSeen(FeatureSlot slot, Observation observation) => slot switch
    {
        FeatureSlot.Head => observation.IsRegionVisible(BodyRegion.Head),
        FeatureSlot.Chest => observation.IsRegionVisible(BodyRegion.UpperBody) || observation.IsRegionVisible(BodyRegion.Hands),
        FeatureSlot.Legs => observation.IsRegionVisible(BodyRegion.LowerBody),
        _ => observation.IsRegionVisible(BodyRegion.Feet),
    };

    // What this witness believes about one slot right now, after forgetting up to this moment. False while
    // it has learned nothing
    public bool TryDescribe(FeatureSlot slot, out OutfitFeature feature, out float styleConfidence, out float colorConfidence)
    {
        Forget(Time.time);
        return memory.TryDescribe(slot, out feature, out styleConfidence, out colorConfidence);
    }

    // Fades every belief by the time since the last fade, at the archetype's half-life. Called when evidence
    // arrives and when the beliefs are read, so nothing runs while nobody looks: the result is the same as
    // fading every frame
    private void Forget(float now)
    {
        float halfLife = perception.Archetype.MemoryHalfLife;
        float elapsed = now - lastFade;
        lastFade = now;
        if (halfLife <= 0f || elapsed <= 0f) return;

        float factor = Mathf.Pow(0.5f, elapsed / halfLife);
        memory.Fade(factor);
    }

    // A copy of what this witness remembers right now, after forgetting up to this moment
    public WitnessReport Report()
    {
        Forget(Time.time);
        return memory.Copy();
    }
}
