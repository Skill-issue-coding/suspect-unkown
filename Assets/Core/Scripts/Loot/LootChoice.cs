using UnityEngine;

// Picks which one of several contents a loot spot holds this run and switches the others off: the item in
// a glass display, or a smash table's thrift goods against the rare jewelry. Picks once, in Awake, so a spot
// LootSpawner switched off before it woke never picks. Uses UnityEngine.Random on purpose: System.Random
// instances created in the same tick share a seed, which would give every display the same item
public class LootChoice : MonoBehaviour
{
    [SerializeField, Tooltip("Even: every option equally likely. ByValue: the more an option is worth, the rarer it is (1 / value). Weighted: each option's own weight")]
    private LootChoiceMode mode = LootChoiceMode.Even;

    [SerializeField, Tooltip("The possible contents. Only the picked one stays active")]
    private LootChoiceOption[] options;

    private void Awake()
    {
        if (options == null || options.Length == 0) return;

        // Allocated once per spot at load, never per frame
        float[] weights = new float[options.Length];
        float total = 0f;
        for (int i = 0; i < options.Length; i++)
        {
            weights[i] = Weight(options[i]);
            total += weights[i];
        }

        int picked = options.Length - 1;
        float roll = Random.value * total;
        for (int i = 0; i < options.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0f)
            {
                picked = i;
                break;
            }
        }

        for (int i = 0; i < options.Length; i++)
            if (options[i].content != null)
                options[i].content.SetActive(i == picked);
    }

    private float Weight(LootChoiceOption option)
    {
        if (option.content == null) return 0f;

        return mode switch
        {
            LootChoiceMode.ByValue => 1f / Mathf.Max(1, ValueOf(option.content)),
            LootChoiceMode.Weighted => option.weight,
            _ => 1f,
        };
    }

    // Everything takeable in the option, so a smash table's pair of cases counts as both
    private static int ValueOf(GameObject content)
    {
        int value = 0;
        foreach (LootPickup pickup in content.GetComponentsInChildren<LootPickup>(true))
            if (pickup.Item != null)
                value += pickup.Item.Value;
        return value;
    }

    private void OnValidate()
    {
        if (options == null || options.Length == 0)
        {
            Debug.LogWarning($"{name} has no loot options", this);
            return;
        }

        float total = 0f;
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i].content == null)
                Debug.LogWarning($"{name} loot option {i} has no content", this);
            else if (mode == LootChoiceMode.ByValue && ValueOf(options[i].content) <= 0)
                Debug.LogWarning($"{name} loot option {i} is worth nothing, so ByValue makes it the most likely pick", this);
            total += options[i].weight;
        }

        if (mode == LootChoiceMode.Weighted && total <= 0f)
            Debug.LogWarning($"{name} is Weighted but every weight is 0; the last option would always be picked", this);
    }
}
