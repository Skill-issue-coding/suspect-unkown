using UnityEngine;

// Drives the guard-only animator parameters from GuardBehaviour; NpcAnimator feeds Speed and TurnRate. 
// Watching follows ActivityChanged; Searching is polled, since arriving is not an activity change. Idle 
// and give-up variants are picked at moments where the switch cannot show: on arrival at a POI, and just 
// before the give-up trigger. Picks never repeat the previous one.
[RequireComponent(typeof(GuardBehaviour))]
public class GuardAnimation : MonoBehaviour
{
    private bool wasDwelling;
    private int lastIdleVariant = -1;
    private int lastGiveUpVariant = -1;

    private GuardBehaviour guard;
    private Animator animator;

    // Picks a whole-number variant in [0, count), never the same as last time when there is a choice.
    // Adding 1..count-1 to the last pick and wrapping skips exactly that one value
    private static int PickVariant(int count, ref int last)
    {
        int pick = count <= 1 || last < 0
            ? Random.Range(0, count)
            : (last + Random.Range(1, count)) % count;

        last = pick;
        return pick;
    }

    private void Awake()
    {
        guard = GetComponent<GuardBehaviour>();
        animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning($"{name} has no Animator; GuardAnimation disabled.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        animator.SetBool(GuardAnimatorParams.WatchingId, guard.Activity == GuardActivity.Watching);
        animator.SetFloat(GuardAnimatorParams.IdleVariantId,
            PickVariant(GuardAnimatorParams.IdleVariantCount, ref lastIdleVariant));
    }

    private void OnEnable() { if (animator != null) guard.ActivityChanged += OnActivityChanged; }
    private void OnDisable() => guard.ActivityChanged -= OnActivityChanged;

    private void Update()
    {
        animator.SetBool(GuardAnimatorParams.SearchingId, guard.IsSearching);

        // Pick on the arrival frame: Speed is still easing down from the walk, so the stand pose is barely
        // weighted yet and the switch never shows.
        bool dwelling = guard.IsDwelling;
        if (dwelling && !wasDwelling)
            animator.SetFloat(GuardAnimatorParams.IdleVariantId,
                PickVariant(GuardAnimatorParams.IdleVariantCount, ref lastIdleVariant));
        wasDwelling = dwelling;
    }

    private void OnActivityChanged(GuardActivity from, GuardActivity to)
    {
        animator.SetBool(GuardAnimatorParams.WatchingId, to == GuardActivity.Watching);

        bool gaveUp = to == GuardActivity.Patrolling
            && (from == GuardActivity.Investigating || from == GuardActivity.Pursuing);
        if (!gaveUp) return;

        animator.SetFloat(GuardAnimatorParams.GiveUpVariantId,
            PickVariant(GuardAnimatorParams.GiveUpVariantCount, ref lastGiveUpVariant));
        animator.SetTrigger(GuardAnimatorParams.GiveUpId);
    }
}
