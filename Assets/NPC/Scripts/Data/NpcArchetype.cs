using System;
using UnityEngine;

// Tuning numbers for one kind of observer: guard, civilian, clerk, or camera. One block per
// milestone: vision and light (M1, M4), suspicion (M2), hearing (M4), anomalies (M5); the witness
// block arrives in M7. Every archetype shares the guard's rise rate, so a conduct weight means the
// same thing for every observer (decision 22). Shared by every NPC using the asset, so never write
// runtime state into it, and never branch on which archetype it is.
[CreateAssetMenu(fileName = "NpcArchetype", menuName = "Suspect Unknown/NPC Archetype")]
public class NpcArchetype : ScriptableObject
{
    [Header("Vision")]
    [SerializeField, Tooltip("Metres. Beyond this, nothing is seen"), Min(0f)]
    private float viewDistance = 15f;

    [SerializeField, Tooltip("The full cone angle. NpcPerception halves it"), Range(1f, 360f)]
    private float fovDegrees = 110f;

    [SerializeField, Tooltip("Metres up from the NPC's transform"), Min(0f)]
    private float eyeHeight = 1.65f;

    [SerializeField, Tooltip("x = distance / viewDistance, y = fDistance")]
    private AnimationCurve distanceFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [SerializeField, Tooltip("x = off-centre angle / half FOV, y = fAngle")]
    private AnimationCurve angleFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [SerializeField, Tooltip("fStance when the player crouches. Standing is 1"), Range(0.1f, 1f)]
    private float crouchVisibilityFactor = 0.6f;

    [SerializeField, Range(0f, 1f), Tooltip("fLight when the player stands in full darkness. Full light is 1")]
    private float darkVisibilityFactor = 0.3f;

    [SerializeField, Tooltip("How much each body region counts toward visibility. 0 ignores the region: no rays cast")]
    private RegionWeights regionWeights = new RegionWeights(1f, 1f, 1f, 1f, 1f);

    [SerializeField, Tooltip("Perception ticks per second"), Range(1f, 60f)]
    private float tickRate = 10f;

    [Header("Hearing")]
    [SerializeField, Range(0f, 20f), Tooltip("Multiplier on how far a sound carries to this NPC")]
    private float hearingSensitivity = 1.0f;

    [SerializeField, Range(0f, 1f), Tooltip("Suspicion added by a sound at full loudness and alarm 1")]
    private float hearingSuspicion = 0.5f;

    [Header("Anomalies")]
    [SerializeField, Range(0f, 1f), Tooltip("Suspicion floor set by noticing an anomaly of weight 1. Keep below alertedAt: an anomaly alone never alerts")]
    private float anomalySuspicion = 0.6f;

    [SerializeField, Tooltip("How much each kind of anomaly worries this observer")]
    private AnomalyWeights anomalyWeights = new AnomalyWeights(1f);

    [Header("Suspicion")]
    [SerializeField, Tooltip("How abnormal each act looks to this observer")]
    private ConductWeights conductWeights = new ConductWeights(0.6f, 0.8f, 1f, 1f);

    [SerializeField, Tooltip("Suspicion per second at full strength and abnormality 1. 0.5 means two seconds from empty to full"), Range(0f, 1f)]
    private float riseRate = 0.5f;

    [SerializeField, Tooltip("Suspicion lost per second while nothing abnormal is in view"), Range(0f, 1f)]
    private float decayRate = 0.1f;

    [SerializeField, Tooltip("Seconds after the last abnormal sighting before decay starts"), Range(0f, 5f)]
    private float decayDelay = 2f;

    [SerializeField, Tooltip("Suspicion needed to enter Curious"), Range(0f, 1f)]
    private float curiousAt = 0.2f;

    [SerializeField, Tooltip("Suspicion needed to enter Investigating"), Range(0f, 1f)]
    private float investigatingAt = 0.5f;

    [SerializeField, Tooltip("Suspicion needed to enter Alerted"), Range(0f, 1f)]
    private float alertedAt = 0.9f;

    [SerializeField, Tooltip("How far suspicion must drop below a threshold to leave that state"), Range(0f, 0.1f)]
    private float hysteresis = 0.05f;

    [Header("Witness")]
    [SerializeField, Range(0f, 1f), Tooltip("How good an observer this is, a factor in every piece of evidence. Tired clerk ~0.4, civilian ~0.6, guard 1")]
    private float observerSkill = 0.6f;

    [SerializeField, Tooltip("How much each part of an outfit is noticed and remembered, 0 to 1")]
    private FeatureVisibility featureVisibility = new FeatureVisibility(1f, 1f, 1f, 1f);

    [SerializeField, Min(0f), Tooltip("Seconds for remembered evidence to halve. 0 never forgets (a camera's recording). Clerk ~90, civilian ~120, guard ~300")]
    private float memoryHalfLife = 120f;

    public float ViewDistance => viewDistance;
    public float FovDegrees => fovDegrees;
    public float EyeHeight => eyeHeight;
    public float RegionWeight(BodyRegion region) => regionWeights.Get(region);
    public float EvaluateDistance(float normalised) => distanceFalloff.Evaluate(normalised);
    public float EvaluateAngle(float normalised) => angleFalloff.Evaluate(normalised);
    public float CrouchVisibilityFactor => crouchVisibilityFactor;
    public float TickRate => tickRate;

    public float Abnormality(ConductFlags flags) => conductWeights.Abnormality(flags);
    public float RiseRate => riseRate;
    public float DecayDelay => decayDelay;
    public float DecayRate => decayRate;
    public float Hysteresis => hysteresis;
    public float HearingSensitivity => hearingSensitivity;
    public float HearingSuspicion => hearingSuspicion;
    public float AnomalySuspicion => anomalySuspicion;
    public float ObserverSkill => observerSkill;
    public float MemoryHalfLife => memoryHalfLife;
    public float FeatureVisibility(FeatureSlot slot) => featureVisibility.Get(slot);
    public float AnomalyWeight(AnomalyKind kind) => anomalyWeights.Get(kind);

    // Light level 0 is darkness, 1 full light. Shadow lowers visibility but never to zero:
    // a figure in the dark is harder to see, not invisible.
    public float LightFactor(float lightLevel) => Mathf.Lerp(darkVisibilityFactor, 1f, Mathf.Clamp01(lightLevel));

    public float Threshold(NpcAlertState state) => state switch
    {
        NpcAlertState.Idle => 0,
        NpcAlertState.Curious => curiousAt,
        NpcAlertState.Investigating => investigatingAt,
        NpcAlertState.Alerted => alertedAt,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    private void OnValidate()
    {
        if (distanceFalloff == null || distanceFalloff.length == 0)
            distanceFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        if (angleFalloff == null || angleFalloff.length == 0)
            angleFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        if (regionWeights.AllZero)
            Debug.LogWarning($"{name} ignores every body region and can never see the player.", this);

        if (curiousAt >= investigatingAt)
            Debug.LogWarning($"{name} curiousAt value is greater (or equal) than investigatingAt value, will lead to state skipping", this);

        if (investigatingAt >= alertedAt)
            Debug.LogWarning($"{name} investigatingAt value is greater (or equal) than alertedAt value, will lead to state skipping", this);

        if (hysteresis >= curiousAt)
            Debug.LogWarning($"{name} hysteresis value greater (or equal) than curiousAt value, can never calm back to Idle", this);

        if (hearingSuspicion >= alertedAt)
            Debug.LogWarning($"{name} hearingSuspicion reaches alertedAt: a sound alone could alert", this);

        if (anomalySuspicion >= alertedAt)
            Debug.LogWarning($"{name} anomalySuspicion reaches alertedAt: an anomaly alone could alert", this);

        if (featureVisibility.AllZero)
            Debug.LogWarning($"{name} notices no part of an outfit: as a witness it will remember nothing", this);
    }
}
