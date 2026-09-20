using UnityEngine;

// Spends the gap between cursor and body on a spine twist, so the character can track the
// cursor without taking a step for every small correction.
//
// Lives on the rig for the same reason PlayerFootIk does: Unity sends OnAnimatorIK only to
// components on the Animator's own GameObject. The execution order puts it after PlayerFootIk,
// so the twist goes on top of a settled hip position rather than one still moving underneath it.
//
// Needs "IK Pass" enabled on the animator layer; PlayerLocomotionBuilder sets that.
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Animator))]
public class PlayerAimTwist : MonoBehaviour
{
    [SerializeField] private bool _enableTwist = true;
    [Tooltip("Largest twist in degrees. Match PlayerAim's Twist Limit, or the spine runs out of " +
             "travel before the feet are asked to move.")]
    [SerializeField] private float _limit = 45.0f;
    [Tooltip("Seconds for the twist to catch up to the cursor. Stops fast mouse flicks snapping the torso.")]
    [SerializeField] private float _smoothing = 0.08f;

    [Header("Share Per Bone")]
    [Tooltip("Fractions of the total twist. Spreading it down the chain is what keeps the waist " +
             "from folding. These should add up to 1.")]
    [SerializeField] private float _spineShare = 0.25f;
    [SerializeField] private float _chestShare = 0.3f;
    [SerializeField] private float _upperChestShare = 0.2f;
    [SerializeField] private float _neckShare = 0.1f;
    [SerializeField] private float _headShare = 0.15f;

    [Header("Head Look")]
    [Tooltip("Point the head along the aim instead of leaving it where the clip put it. The crouch " +
             "clips look straight at the floor, which this fixes.")]
    [SerializeField] private bool _enableLook = true;
    [Tooltip("How much of the head pose the look takes over while standing.")]
    [SerializeField] private float _standingLookWeight = 0.5f;
    [Tooltip("How much while crouching. The crouch clips need more, because their head is further off.")]
    [SerializeField] private float _crouchingLookWeight = 1.0f;
    [Tooltip("Share of the look given to the eyes. 0 on a rig with no eye bones.")]
    [SerializeField] private float _eyesWeight = 0.0f;
    [Tooltip("Limits how far off centre the head may point. 0 lets it turn all the way, 1 pins it forward.")]
    [SerializeField] private float _lookClamp = 0.6f;
    [Tooltip("Seconds for the look weight to fade as the stance changes.")]
    [SerializeField] private float _lookSmoothing = 0.15f;

    private Animator _animator;
    private PlayerAim _aim;
    private PlayerStance _stance;

    private float _twist;
    private float _twistVelocity;

    private float _lookWeight;
    private float _lookVelocity;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _aim = GetComponentInParent<PlayerAim>();
        _stance = GetComponentInParent<PlayerStance>();

        if (_aim == null)
        {
            Debug.LogError($"{name}: no PlayerAim on this object or any parent.", this);
            enabled = false;
            return;
        }

        // SetBoneLocalRotation addresses humanoid bones, which a generic rig does not have.
        if (!_animator.isHuman)
        {
            Debug.LogError($"{name}: aim twist needs a Humanoid avatar; this rig is generic.", _animator);
            enabled = false;
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0) return;

        ApplyLook();

        if (!_enableTwist) return;

        // Past the limit the body itself turns, so the spine holds at full twist and no further.
        float target = Mathf.Clamp(_aim.TurnError, -_limit, _limit);
        _twist = Mathf.SmoothDamp(_twist, target, ref _twistVelocity, _smoothing);

        if (Mathf.Abs(_twist) < 0.01f) return;

        ApplyTwist(HumanBodyBones.Spine, _spineShare);
        ApplyTwist(HumanBodyBones.Chest, _chestShare);
        ApplyTwist(HumanBodyBones.UpperChest, _upperChestShare);

        // The look already owns the neck and head when it is on, and twisting them as well would
        // turn the head twice as far as the cursor asks for.
        if (_enableLook && _lookWeight > 0.001f) return;

        ApplyTwist(HumanBodyBones.Neck, _neckShare);
        ApplyTwist(HumanBodyBones.Head, _headShare);
    }

    // Unity's look-at solver, aimed level with the head rather than at the cursor's point on the
    // floor: aiming at the floor point is what the crouch clips already do, and it reads as the
    // character studying their own feet.
    private void ApplyLook()
    {
        float target = _enableLook
            ? (_stance != null && _stance.Current == Stance.Crouching ? _crouchingLookWeight : _standingLookWeight)
            : 0.0f;

        _lookWeight = Mathf.SmoothDamp(_lookWeight, target, ref _lookVelocity, _lookSmoothing);

        if (_lookWeight < 0.001f)
        {
            _animator.SetLookAtWeight(0.0f);
            return;
        }

        Transform head = _animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null) return;

        Vector3 lookAt = _aim.AimPoint;
        lookAt.y = head.position.y;

        // Body weight stays at 0: the spine twist above owns the torso, and letting the solver
        // turn it too would fight that.
        _animator.SetLookAtWeight(_lookWeight, 0.0f, 1.0f, _eyesWeight, _lookClamp);
        _animator.SetLookAtPosition(lookAt);
    }

    // Each bone turns about world up rather than its own axis, so a bone that is pitched forward
    // still yaws instead of tipping the character sideways.
    private void ApplyTwist(HumanBodyBones bone, float share)
    {
        if (share <= 0.0f) return;

        Transform boneTransform = _animator.GetBoneTransform(bone);
        if (boneTransform == null) return;

        Vector3 axis = Quaternion.Inverse(boneTransform.rotation) * Vector3.up;
        Quaternion rotation = boneTransform.localRotation * Quaternion.AngleAxis(_twist * share, axis);
        _animator.SetBoneLocalRotation(bone, rotation);
    }
}
