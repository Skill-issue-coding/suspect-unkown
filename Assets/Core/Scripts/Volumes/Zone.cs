using UnityEngine;

// Trigger volume tagged public or staff. Passive: the player side 
// tracks which zones it is in. Colliders on this object; 
// several boxes make complex shapes. Reused by Phase 8.
[RequireComponent(typeof(Collider))]
public class Zone : MonoBehaviour
{
    [SerializeField, Tooltip("What type of Zone it is")]
    private ZoneType type;

    [SerializeField, Min(0), Tooltip("Room index for the job log; 0 is the street, so zones start at 1. Unique per room in a level")]
    private int room = 1;

    [SerializeField]
    private bool drawGizmo = true;

    [SerializeField]
    private Color publicColor = new Color(0.2f, 1f, 0.2f, 0.15f);

    [SerializeField]
    private Color staffColor = new Color(1f, 0.2f, 0.2f, 0.15f);

    public ZoneType Type => type;
    public int Room => room;

    private Collider[] colliders;

    private void Reset()
    {
        colliders = GetComponents<Collider>();
        foreach (Collider collider in colliders)
            collider.isTrigger = true;

        int layer = LayerMask.NameToLayer("Volume");
        if (layer < 0)
            Debug.LogWarning("Layer 'Volume' does not exist.", this);
        else
            gameObject.layer = layer;
    }

    private void OnValidate()
    {
        colliders = GetComponents<Collider>();

        foreach (Collider collider in colliders)
            if (!collider.isTrigger)
                Debug.LogWarning($"{name} collider isn't a trigger", this);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmo) return;
        Gizmos.color = type == ZoneType.Staff ? staffColor : publicColor;

        foreach (Collider zoneCollider in GetComponents<Collider>())
        {
            Gizmos.matrix = zoneCollider.transform.localToWorldMatrix;

            switch (zoneCollider)
            {
                case BoxCollider box:
                    Gizmos.DrawCube(box.center, box.size);
                    break;
                case SphereCollider sphere:
                    Gizmos.DrawSphere(sphere.center, sphere.radius);
                    break;
                case MeshCollider mesh when mesh.sharedMesh != null:
                    Gizmos.DrawMesh(mesh.sharedMesh);
                    break;
                default:
                    Gizmos.matrix = Matrix4x4.identity;
                    Gizmos.DrawCube(zoneCollider.bounds.center, zoneCollider.bounds.size);
                    break;
            }
        }

        Gizmos.matrix = Matrix4x4.identity;


#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position, $"{name} [{room}]");
#endif
    }
}
