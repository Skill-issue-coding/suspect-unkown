using System.Collections.Generic;
using UnityEngine;

public class CivillianSpawner : MonoBehaviour
{
    [Header("Customers")]
    [SerializeField, Tooltip("Civilian prefab spawned for each customer. Its role must be Customer")]
    private CivilianBehaviour customerPrefab;

    [SerializeField, Tooltip("Fewest and most customers per run, both included; rolled at load")]
    private Vector2Int customerCount = new Vector2Int(3, 5);

    [SerializeField, Tooltip("Where customers may start, on the NavMesh inside the shop. Each point is used once")]
    private Transform[] spawnPoints;

    [Header("Debug")]
    [SerializeField, Tooltip("Repeats the roll when not 0")]
    private int seed;

    private void Awake()
    {
        if (customerPrefab == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning($"{name} needs a customer prefab and spawn points", this);
            enabled = false;
            return;
        }

        System.Random random = seed != 0 ? new System.Random(seed) : new System.Random();

        List<Transform> free = new List<Transform>();
        foreach (Transform point in spawnPoints)
            if (point != null) free.Add(point);

        int low = Mathf.Max(0, customerCount.x);
        int high = Mathf.Max(low, customerCount.y);
        int count = Mathf.Min(random.Next(low, high + 1), free.Count);

        for (int i = 0; i < count; i++)
        {
            int pick = random.Next(free.Count);
            Transform point = free[pick];
            free.RemoveAt(pick);

            CivilianBehaviour customer = Instantiate(customerPrefab, point.position, point.rotation);
            customer.name = $"Customer {i}";
        }

        Debug.Log($"Civilians: {count} customers spawned (range {low} to {high})");
    }
}
