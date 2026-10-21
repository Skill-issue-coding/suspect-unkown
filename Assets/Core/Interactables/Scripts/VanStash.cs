using UnityEngine;

// The getaway van's stash point: a trigger at its side door where the player empties the bag mid-job.
// At the side door, not the back doors, so standing here never starts the extraction countdown. It only
// marks the spot and stashes on request; PlayerVanStash finds it and shows its prompt, so the van never
// looks up the player
[RequireComponent(typeof(Collider))]
public class VanStash : MonoBehaviour
{
    [SerializeField, Tooltip("The prompt shown while the player is in reach. Shown by the player side, never by this")]
    private WorldPrompt prompt;

    [SerializeField, Tooltip("Where the prompt's line points. If empty, uses this transform + promptOffset")]
    private Transform promptTarget;

    [SerializeField, Tooltip("Text in the prompt box")]
    private string useText = "E";

    private Collider trigger;

    public WorldPrompt Prompt => prompt;
    public string UseText => useText;
    public Vector3 PromptPoint => promptTarget != null ? promptTarget.position : transform.position;

    private void Awake()
    {
        trigger = GetComponent<Collider>();

        if (!trigger.isTrigger)
            Debug.LogWarning($"{name}: the collider must be a trigger, or it will block the player", this);
    }

    public void Stash()
    {
        if (LootBag.Active == null) return;
        int count = LootBag.Active.Stash();
        if (count > 0) Debug.Log($"{name}: {count} bag item(s) stashed in the van");
    }
}
