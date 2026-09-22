using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PerceptionPoint : MonoBehaviour
{
    // MapPlayerPerceptionPoints sets these three fields by name. Renaming one means renaming
    // the matching constant there.
    [SerializeField] private BodyRegion region;
    [SerializeField, Range(0.1f, 1.0f)] private float weight = 1f;
    [Tooltip("One point per region is cast. Highest priority wins.")]
    [SerializeField] private int priority;

    private Collider bodyCollider;
    public BodyRegion Region => region;
    public float Weight => weight;
    public int Priority => priority;
    public Vector3 WorldPoint => bodyCollider.bounds.center;

    private void Awake() => bodyCollider = GetComponent<Collider>();
}
