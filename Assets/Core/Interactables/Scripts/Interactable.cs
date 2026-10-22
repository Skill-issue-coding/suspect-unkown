using System;
using UnityEngine;

// The shared part of every interaction: a trigger the player stands in, the prompt and outline
// shown for it, and a way to use it. One per reach spot (a door has two, one per side). The
// behaviour (IInteractionBehaviour) decides whether the interaction is blocked and what it does
[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
{
    [Header("Presentation")]
    [SerializeField, Tooltip("The prompt shown while the player is in reach. Shown by the player side, never by this")]
    private WorldPrompt prompt;

    [SerializeField, Tooltip("Outlined while it is the nearest in reach (optional)")]
    private InteractOutline outline;

    [SerializeField, Tooltip("Where the prompt's line points. If empty, uses this transform + promptOffset")]
    private Transform promptTarget;

    [SerializeField, Tooltip("Local-space offset for the prompt target, used when promptTarget is empty")]
    private Vector3 promptOffset = new Vector3(0f, 0.2f, 0f);

    [SerializeField, Tooltip("Text in the prompt box")]
    private string useText = "E";

    [Header("Use")]
    [SerializeField, Tooltip("Seconds to hold E. 0 = press; above 0 = hold, the prompt ring fills over this time")]
    private float holdSeconds;

    public WorldPrompt Prompt => prompt;
    public InteractOutline Outline => outline;
    public string UseText => useText;
    public float HoldSeconds => holdSeconds;
    public string BlockedText => behaviour.BlockedText;

    public Vector3 PromptPoint =>
        promptTarget != null ? promptTarget.position : transform.TransformPoint(promptOffset);

    // Raised by SetReachable(false) and by OnDisable, so the player side drops this interactable
    // from its reach dictionary even when the thing switches itself off (no OnTriggerExit)
    public event Action<Interactable> Withdrawn;

    // Raised when the player tries to use a blocked interactable. Static so the HUD toast hears every
    // refusal without a reference to the player (Core references nothing)
    public static event Action<Interactable> Refused;

    private Collider trigger;
    private IInteractionBehaviour behaviour;

    public IInteractionBehaviour Behaviour => behaviour;

    private void Awake()
    {
        trigger = GetComponent<Collider>();
        behaviour = GetComponentInParent<IInteractionBehaviour>();

        if (behaviour == null)
        {
            Debug.LogWarning($"{name} has no IInteractionBehaviour; it cannot be used", this);
            enabled = false;
            return;
        }

        if (!trigger.isTrigger)
            Debug.LogWarning($"{name}: the collider must be a trigger, or it will block the player", this);

        if (outline == null)
            outline = GetComponentInChildren<InteractOutline>();
    }

    // Lets a behaviour override the authored hold time (loot sets it from item.TakeSeconds in Awake)
    public void SetHoldSeconds(float seconds) => holdSeconds = seconds;

    // Asks the behaviour whether this interactable is blocked for the given interactor (greyed prompt)
    public bool IsBlockedFor(GameObject interactor) => behaviour.IsBlockedFor(interactor);

    // Called by the player side after a completed press or hold
    public void Use(GameObject interactor) => behaviour.Interact(interactor);

    // Called by the player side when a press lands on a blocked interactable, so the HUD can say why
    public void Refuse() => Refused?.Invoke(this);

    // Enables or disables the reach trigger. Raises Withdrawn when turned off. Safe to call before this
    // Awake has run: an owner (the safe, the glass cutter) switches its loot off from its own Awake, and
    // Awake order between objects is not defined
    public void SetReachable(bool reachable)
    {
        if (trigger == null) trigger = GetComponent<Collider>();
        trigger.enabled = reachable;
        if (!reachable) Withdrawn?.Invoke(this);
    }

    private void OnDisable()
    {
        Withdrawn?.Invoke(this);
    }
}
