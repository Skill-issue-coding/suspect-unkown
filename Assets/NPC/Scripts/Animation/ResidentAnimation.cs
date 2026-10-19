using UnityEngine;

// Shows ResidentBehaviour on the clerk's animator: the phase drives the base layer (armchair, snack run),
// the scare stops the snacking, and deliberating or calling brings the phone out on the upper-body layer.
// Speed comes from NpcAnimator. Decides nothing
[RequireComponent(typeof(ResidentBehaviour), typeof(NpcDecisionWindow))]
public class ResidentAnimation : MonoBehaviour
{
    [SerializeField, Tooltip("Animator on the model. Found in children when left empty")]
    private Animator animator;

    [SerializeField, Min(0.01f), Tooltip("Seconds to fade the phone layer in or out")]
    private float phoneFadeSeconds = 0.3f;

    private ResidentBehaviour resident;
    private NpcDecisionWindow window;

    // The upper-body layer: built at weight 0, faded in only while the phone is out. At full weight with no
    // clip playing, a Humanoid layer would hold its masked arms and head in the neutral muscle pose
    private int phoneLayer;
    private float phoneWeight;

    private void Awake()
    {
        window = GetComponent<NpcDecisionWindow>();
        resident = GetComponent<ResidentBehaviour>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogWarning($"{name} has no Animator in its children; ResidentAnimation disabled", this);
            enabled = false;
            return;
        }

        phoneLayer = animator.GetLayerIndex("Phone");
    }

    private void Update()
    {
        animator.SetInteger(ResidentAnimatorParams.PhaseId, (int)resident.Phase);
        animator.SetBool(ResidentAnimatorParams.ScaredId, resident.IsScared);
        animator.SetBool(NpcAnimatorParams.DeliberatingId, window.IsDeliberating);
        animator.SetBool(NpcAnimatorParams.CallingId, window.IsCalling);

        float target = window.IsDeliberating || window.IsCalling ? 1f : 0f;
        if (phoneWeight != target)
        {
            phoneWeight = Mathf.MoveTowards(phoneWeight, target, Time.deltaTime / phoneFadeSeconds);
            animator.SetLayerWeight(phoneLayer, phoneWeight);
        }
    }
}
