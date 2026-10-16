using UnityEngine;

// Trigger volume tagged public or staff. Passive: the player side tracks which zones it is in.
// Colliders on this object only; several boxes make complex shapes. In a level with a JobLayout
// each zone is also one room of the job log, numbered by its place in the layout's room list.
[RequireComponent(typeof(Collider))]
public class Zone : MonoBehaviour
{
    [SerializeField, Tooltip("What type of Zone it is")]
    private ZoneType type;

    [SerializeField, Tooltip("Draw the zone's volume and name in the Scene view")]
    private bool drawGizmo = true;

    [SerializeField, Tooltip("Gizmo color for a public zone")]
    private Color publicColor = new Color(0.2f, 1f, 0.2f, 0.15f);

    [SerializeField, Tooltip("Gizmo color for a staff zone")]
    private Color staffColor = new Color(1f, 0.2f, 0.2f, 0.15f);

    public ZoneType Type => type;

    // Set by the level's JobLayout in Awake from its position in the room list; 0 (the street)
    // when the level has no layout or the zone is not listed
    public int Room { get; private set; }

    public void AssignRoom(int room) => Room = room;

    private void Reset()
    {
        foreach (Collider collider in GetComponents<Collider>())
            collider.isTrigger = true;

        int layer = LayerMask.NameToLayer("Volume");
        if (layer < 0)
            Debug.LogWarning("Layer 'Volume' does not exist.", this);
        else
            gameObject.layer = layer;
    }

    private void OnValidate()
    {
        foreach (Collider collider in GetComponents<Collider>())
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
        UnityEditor.Handles.Label(transform.position, name);
#endif
    }
}
