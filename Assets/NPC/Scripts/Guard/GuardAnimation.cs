using UnityEngine;

[RequireComponent(typeof(GuardBehaviour))]
public class GuardAnimation : MonoBehaviour
{
    private GuardBehaviour guard;
    private Animator animator;

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

    private void OnEnable() { if (animator != null) guard.ActivityChanged += OnActivityChanged; }
    private void OnDisable() => guard.ActivityChanged -= OnActivityChanged;

    private void Update()
    {
        animator.SetBool(GuardAnimatorParams.WatchingId, guard.Activity == GuardActivity.Watching);
        animator.SetBool(GuardAnimatorParams.SearchingId, guard.IsSearching);
    }

    private void OnActivityChanged(GuardActivity from, GuardActivity to)
    {
        bool gaveUp = to == GuardActivity.Patrolling && (from == GuardActivity.Investigating || from == GuardActivity.Pursuing);
        if (gaveUp) animator.SetTrigger(GuardAnimatorParams.GiveUpId);
    }
}
