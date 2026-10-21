using UnityEngine;

public class LootSpawner : MonoBehaviour
{
    [Header("Slots")]
    [SerializeField, Tooltip("Every loot slot in the level, in any order")]
    private LootSlot[] slots;

    [Header("Roll")]
    [SerializeField, Tooltip("Off: every slot stays as authored. On: only Min to Max random slots are live")]
    private bool randomized = true;

    [SerializeField, Min(0), Tooltip("Fewest live slots per run")]
    private int minCount = 3;

    [SerializeField, Min(0), Tooltip("Most live slots per run")]
    private int maxCount = 4;

    [SerializeField, Tooltip("Repeats a roll while testing. -1 = a fresh roll every run")]
    private int debugSeed = -1;

    private void Awake()
    {
        if (randomized != true) return;

        System.Random rng = debugSeed >= 0 ? new System.Random(debugSeed) : new System.Random();
        int count = Mathf.Min(rng.Next(minCount, maxCount + 1), slots.Length);

        int[] order = new int[slots.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        for (int i = 0; i < count; i++)
        {
            int j = rng.Next(i, order.Length);
            (order[i], order[j]) = (order[j], order[i]);
        }

        for (int i = 0; i < order.Length; i++)
            if (slots[order[i]] != null) slots[order[i]].SetLive(i < count);
    }

    private void OnValidate()
    {
        if (maxCount <= minCount)
            Debug.LogWarning($"{name} maxCount is less than minCount", this);

        if (maxCount > slots.Length)
            Debug.LogWarning($"{name} maxCount is {maxCount} which is greater than slots.Length {slots.Length}", this);

        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null)
                Debug.LogWarning($"{name} slot index {i} is null", this);
    }
}
