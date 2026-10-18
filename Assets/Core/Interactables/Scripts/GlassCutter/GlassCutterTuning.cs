using UnityEngine;

// Tuning for the glass cutter minigame: how fast it cuts and how forgiving the aim is. Tuning only;
// the running state lives in GlassCutterModel
[CreateAssetMenu(menuName = "Data/Glass Cutter Tuning")]
public class GlassCutterTuning : ScriptableObject
{
    [SerializeField, Tooltip("Degrees per second the cutter turns while aligned and holding tension")]
    private float cutSpeed = 75f;

    [SerializeField, Range(5f, 60f), Tooltip("Half-angle in degrees: how close the aim must be to the dotted line's tangent")]
    private float alignmentTolerance = 22f;

    [SerializeField, Tooltip("Minimum distance in metres from the cutter tip to the pointer for the aim to count")]
    private float minAimDistance = 0.05f;

    [SerializeField, Tooltip("True to cut clockwise, false for counter-clockwise")]
    private bool cutClockwise = true;

    public float CutSpeed => cutSpeed;
    public float AlignmentTolerance => alignmentTolerance;
    public float MinAimDistance => minAimDistance;
    public bool CutClockwise => cutClockwise;

    private void OnValidate()
    {
        if (cutSpeed <= 0f)
            Debug.LogWarning($"{name}: cutSpeed must be greater than zero", this);

        if (minAimDistance < 0f)
            Debug.LogWarning($"{name}: minAimDistance must not be negative", this);
    }
}
