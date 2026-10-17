using UnityEngine;

// One doorway between the street and the level, for the job log (thriftshop-level.md 4.1).
// A trigger straddling the doorway whose local +Z points into the room: a crossing counts as
// inbound when the player enters from the street side and leaves on the room side, so stepping
// in and backing out, or walking out, counts nothing. Every inbound crossing counts, so round
// trips through the same door reinforce it. On the Volume layer, which collides with the
// player's CharacterController only. Numbered by its place in the level's JobLayout.
[RequireComponent(typeof(BoxCollider))]
public class Entrance : MonoBehaviour
{
    [SerializeField, Tooltip("The room this entrance opens into")]
    private Zone opensInto;

    [SerializeField, Tooltip("Log every inbound crossing to the console")]
    private bool logCrossings = true;

    // Set by the level's JobLayout in Awake from its position in the entrance list; -1 when the
    // entrance is not listed, so an unlisted doorway can never pass for entrance 0
    public int Index { get; private set; } = -1;
    public Zone OpensInto => opensInto;

    public void AssignIndex(int index) => Index = index;

    private bool enteredFromStreet;

    private void Awake()
    {
        if (opensInto == null)
        {
            Debug.LogWarning($"{name} has no room to open into; crossings are not counted", this);
            enabled = false;
        }
    }

    private void OnTriggerEnter(Collider other) => enteredFromStreet = OnStreetSide(other.transform.position);

    private void OnTriggerExit(Collider other)
    {
        // Trigger messages still reach a disabled component, so the guard has to be explicit
        if (!enabled || !enteredFromStreet || OnStreetSide(other.transform.position)) return;

        enteredFromStreet = false;
        JobLog.RecordEntry(Index);
        if (logCrossings) Debug.Log($"{name} inbound crossing (entrance {Index} -> room {opensInto.Room})", this);
    }

    private bool OnStreetSide(Vector3 worldPosition) =>
        transform.InverseTransformPoint(worldPosition).z < 0f;

    // As Zone: a trigger on the Volume layer, so only the player's CharacterController reaches it
    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;

        int layer = LayerMask.NameToLayer("Volume");
        if (layer < 0)
            Debug.LogWarning("Layer 'Volume' does not exist.", this);
        else
            gameObject.layer = layer;
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(1f, 0.6f, 0f, 1f);
        Gizmos.DrawLine(box.center, box.center + Vector3.forward * 1.5f);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
