using System;
using UnityEngine;

// A stealable item in the level: a trigger on the Volume layer the player stands in, a prompt and
// an outline the player side shows, and Take(), which announces the item and hides it. Taken is
// static so a level's objectives hear every pickup without keeping a list of them
[RequireComponent(typeof(Collider))]
public class LootPickup : MonoBehaviour
{
    public static event Action<LootPickup> Taken;

    [SerializeField, Tooltip("Which item this is. Shared with every pickup of the same item")]
    private LootItem item;

    [SerializeField, Tooltip("The prompt shown while the player is in reach. Shown by the player side, never by this pickup")]
    private WorldPrompt prompt;

    [SerializeField, Tooltip("Outlined while the player is in reach")]
    private InteractOutline outline;

    [SerializeField, Tooltip("Where the prompt's line points, in local space")]
    private Vector3 promptOffset = new Vector3(0f, 0.2f, 0f);

    public LootItem Item => item;
    public WorldPrompt Prompt => prompt;
    public InteractOutline Outline => outline;
    public Vector3 PromptPoint => transform.TransformPoint(promptOffset);

    private void Awake()
    {
        if (item == null)
        {
            Debug.LogWarning($"{name} has no LootItem; it cannot be taken", this);
            enabled = false;
        }

        if (!GetComponent<Collider>().isTrigger)
            Debug.LogWarning($"{name}: the collider must be a trigger, or it will block the player", this);
    }

    // Announce first, then hide: listeners can still read the item. Switching off sends no
    // OnTriggerExit, so the player side must drop this pickup when it hears Taken
    [ContextMenu("Take")]
    public void Take()
    {
        if (!enabled || !gameObject.activeSelf) return;
        Taken?.Invoke(this);
        gameObject.SetActive(false);
    }
}
