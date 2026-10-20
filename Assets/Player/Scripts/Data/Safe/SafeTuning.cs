using UnityEngine;

// Difficulty per safe level and the feel values every safe shares. Read by PlayerSafeCracking when it
// builds a SafeModel. Tuning only; the combination and the numbers already set live on the Safe
[CreateAssetMenu(menuName = "Suspect Unknown/Interactables/Safe Tuning")]
public class SafeTuning : ScriptableObject
{
    [Header("Levels")]
    [SerializeField, Tooltip("One entry per safe level, level 1 first")]
    private SafeLevelTuning[] levels =
    {
        new SafeLevelTuning { numbers = 3, sweetSpot = 9f, margin = 36f, testSpeed = 1.6f, slipTime = 0.9f },
        new SafeLevelTuning { numbers = 3, sweetSpot = 6f, margin = 27f, testSpeed = 1.3f, slipTime = 0.7f },
        new SafeLevelTuning { numbers = 3, sweetSpot = 4f, margin = 18f, testSpeed = 1.1f, slipTime = 0.5f }
    };

    [Header("Feel")]
    [SerializeField, Tooltip("Degrees the dial turns per unit of horizontal mouse movement")]
    private float dialSpeed = 0.3f;

    [SerializeField, Tooltip("Tests per second the test meter falls back while tension is released")]
    private float returnSpeed = 3f;

    [SerializeField, Tooltip("Seconds tension does nothing after a slip")]
    private float slipLockout = 0.4f;

    public int LevelCount => levels.Length;
    public float DialSpeed => dialSpeed;
    public float ReturnSpeed => returnSpeed;
    public float SlipLockout => slipLockout;

    // Level 1 is the first entry. Out-of-range levels are clamped, so a safe set above the table still opens
    public SafeLevelTuning Level(int lockLevel) => levels[Mathf.Clamp(lockLevel - 1, 0, levels.Length - 1)];

    private void OnValidate()
    {
        if (levels == null || levels.Length == 0)
        {
            Debug.LogWarning($"{name} has no levels", this);
            return;
        }

        for (int i = 0; i < levels.Length; i++)
        {
            SafeLevelTuning level = levels[i];
            if (level.numbers < 1 || level.sweetSpot <= 0f || level.margin <= 0f
                || level.testSpeed <= 0f || level.slipTime <= 0f)
                Debug.LogWarning($"{name}: safe level {i + 1} has a value at or below zero", this);
        }
    }
}
