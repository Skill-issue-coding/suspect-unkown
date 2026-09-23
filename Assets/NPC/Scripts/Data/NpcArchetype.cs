using UnityEngine;

// Tuning numbers for one kind of observer: guard, civilian, clerk, or camera. One block per
// milestone (Vision now; Suspicion, Hearing, Witness later). Shared by every NPC using the asset,
// so never write runtime state into it, and never branch on which archetype it is.
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

    [SerializeField, Tooltip("How much each body region counts toward visibility. 0 ignores the region: no rays cast")]
    private RegionWeights regionWeights = new RegionWeights(1f, 1f, 1f, 1f, 1f);

    [SerializeField, Tooltip("Perception ticks per second"), Range(1f, 60f)]
    private float tickRate = 10f;

    public float ViewDistance => viewDistance;
    public float FovDegrees => fovDegrees;
    public float EyeHeight => eyeHeight;
    public float RegionWeight(BodyRegion region) => regionWeights.Get(region);
    public float EvaluateDistance(float normalised) => distanceFalloff.Evaluate(normalised);
    public float EvaluateAngle(float normalised) => angleFalloff.Evaluate(normalised);
    public float CrouchVisibilityFactor => crouchVisibilityFactor;
    public float TickRate => tickRate;

    private void OnValidate()
    {
        if (distanceFalloff == null || distanceFalloff.length == 0)
            distanceFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        if (angleFalloff == null || angleFalloff.length == 0)
            angleFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        if (regionWeights.AllZero)
            Debug.LogWarning($"{name} ignores every body region and can never see the player.", this);
    }
}
