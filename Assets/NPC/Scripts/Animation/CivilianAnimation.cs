using UnityEngine;

// Shows CivilianBehaviour on the civilian animator: standing at a spot plays that spot's station clip on
// the base layer, and deliberating or calling brings the phone out on the upper-body layer. Speed comes
// from NpcAnimator. Decides nothing. Not on the apartment clerk, whose ResidentAnimation drives its own
// controller
[RequireComponent(typeof(CivilianBehaviour), typeof(NpcDecisionWindow))]
public class CivilianAnimation : MonoBehaviour
{
    [SerializeField, Tooltip("Animator on the model. Found in children when left empty")]
    private Animator animator;

    [SerializeField, Min(0.01f), Tooltip("Seconds to fade the phone layer in or out")]
    private float phoneFadeSeconds = 0.3f;

    private CivilianBehaviour civilian;
    private NpcDecisionWindow window;

    // The upper-body layer: built at weight 0, faded in only while the phone is out. At full weight with no
    // clip playing, a Humanoid layer would hold its masked arms and head in the neutral muscle pose
    private int phoneLayer;
    private float phoneWeight;

    private void Awake()
    {
        civilian = GetComponent<CivilianBehaviour>();
        window = GetComponent<NpcDecisionWindow>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogWarning($"{name} has no Animator in its children; CivilianAnimation disabled", this);
            enabled = false;
            return;
        }

        phoneLayer = animator.GetLayerIndex("Phone");
        if (phoneLayer < 0)
            Debug.LogWarning($"{name}'s animator has no Phone layer; rebuild it with Civilian > Rebuild Locomotion", this);
    }

    private void Update()
    {
        // Station only changes while walking between spots, so the Dwell tree never visibly blends two clips
        bool dwelling = civilian.IsDwelling;
        animator.SetBool(CivilianAnimatorParams.DwellingId, dwelling);
        if (dwelling) animator.SetFloat(CivilianAnimatorParams.StationId, (float)civilian.Station);

        animator.SetBool(NpcAnimatorParams.DeliberatingId, window.IsDeliberating);
        animator.SetBool(NpcAnimatorParams.CallingId, window.IsCalling);

        float target = window.IsDeliberating || window.IsCalling ? 1f : 0f;
        if (phoneLayer >= 0 && phoneWeight != target)
        {
            phoneWeight = Mathf.MoveTowards(phoneWeight, target, Time.deltaTime / phoneFadeSeconds);
            animator.SetLayerWeight(phoneLayer, phoneWeight);
        }
    }
}
