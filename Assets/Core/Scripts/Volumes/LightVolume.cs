using UnityEngine;

// Trigger volume tagged with a light level, read as fLight by NPC perception. Passive: the player
// side tracks which volumes it is in. A stand-in for real lighting: cheap, and predictable enough
// for the player to learn. Where volumes overlap the darkest wins; outside every volume is full light.
[RequireComponent(typeof(Collider))]
public class LightVolume : MonoBehaviour
{
    [Header("Light")]
    [SerializeField, Range(0f, 1f), Tooltip("Light level inside this volume. 0 darkness, 1 full light. Where volumes overlap, the darkest wins")]
    private float level = 0.2f;

    [Header("Gizmo")]
    [SerializeField, Tooltip("Draw the volume in the Scene view, tinted by its level")]
    private bool drawGizmo = true;

    [SerializeField, Tooltip("Gizmo tint at level 0. Keep alpha low so the level stays readable")]
    private Color darkColor = new Color(0.1f, 0.1f, 0.4f, 0.15f);

    [SerializeField, Tooltip("Gizmo tint at level 1")]
    private Color brightColor = new Color(1f, 0.9f, 0.3f, 0.15f);

    public float Level => level;

    private void Reset()
    {
        foreach (Collider volumeCollider in GetComponents<Collider>())
            volumeCollider.isTrigger = true;

        int layer = LayerMask.NameToLayer("Volume");
        if (layer < 0)
            Debug.LogWarning("Layer 'Volume' does not exist.", this);
        else
            gameObject.layer = layer;
    }

    private void OnValidate()
    {
        foreach (Collider volumeCollider in GetComponents<Collider>())
            if (!volumeCollider.isTrigger)
                Debug.LogWarning($"{name} collider isn't a trigger", this);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmo) return;
        Gizmos.color = Color.Lerp(darkColor, brightColor, level);

        foreach (Collider volumeCollider in GetComponents<Collider>())
        {
            Gizmos.matrix = volumeCollider.transform.localToWorldMatrix;

            switch (volumeCollider)
            {
                case BoxCollider box: Gizmos.DrawCube(box.center, box.size); break;
                case SphereCollider sphere: Gizmos.DrawSphere(sphere.center, sphere.radius); break;
                case MeshCollider mesh when mesh.sharedMesh != null: Gizmos.DrawMesh(mesh.sharedMesh); break;
                default:
                    Gizmos.matrix = Matrix4x4.identity;
                    Gizmos.DrawCube(volumeCollider.bounds.center, volumeCollider.bounds.size);
                    break;
            }
        }

        Gizmos.matrix = Matrix4x4.identity;
    }
}
