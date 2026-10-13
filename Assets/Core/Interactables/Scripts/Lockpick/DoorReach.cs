using UnityEngine;

// Marks where the player can use a door: a trigger on the Volume layer, which collides with the
// player only. Put one on each side of the doorway, stopping short of the leaf so the player never
// stands where it swings. The door switches its reaches off while open, so a prompt only appears
// at a closed door
[RequireComponent(typeof(Collider))]
public class DoorReach : MonoBehaviour
{
    [SerializeField, Tooltip("The prompt on this side of the door. Shown by the player side, never by this reach")]
    private WorldPrompt prompt;

    public WorldPrompt Prompt => prompt;
    public Door Door { get; private set; }

    private Collider trigger;

    private void Awake()
    {
        Door = GetComponentInParent<Door>();
        trigger = GetComponent<Collider>();

        if (Door == null)
            Debug.LogWarning($"{name} has no Door above it; the player cannot use it", this);
        if (!trigger.isTrigger)
            Debug.LogWarning($"{name}: the collider must be a trigger, or it will block the player", this);
    }

    public void SetReachable(bool reachable) => trigger.enabled = reachable;
}
