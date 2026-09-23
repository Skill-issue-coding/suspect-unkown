using UnityEngine;

// MapPlayerPerceptionPoints sets region by name. Renaming it means renaming RegionField there.
[RequireComponent(typeof(Collider))]
public class PerceptionPoint : MonoBehaviour
{
    [SerializeField] private BodyRegion region;

    private Collider bodyCollider;
    public BodyRegion Region => region;
    public Vector3 WorldPoint => bodyCollider.bounds.center;

    private void Awake() => bodyCollider = GetComponent<Collider>();
}
