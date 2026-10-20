using UnityEngine;
using PrimeTween;

// The lockpicking minigame's own drawing: the 3D lock and pick, the bind shake, the slip rattle, and the
// HUD. The camera, blur, push-in and open punch are the MinigameScreen beside it. Reads a LockpickModel
// and decides nothing. PlayerLockpicking calls Begin and End
[RequireComponent(typeof(MinigameScreen))]
public class LockpickView : MonoBehaviour
{
    [Header("Hierarchy")]
    [Tooltip("The parent object holding both meshes, used for the slip rattle shake")]
    [SerializeField] private Transform lockRoot;
    [SerializeField, Tooltip("Rotated by the pick angle")] private Transform pickMesh;
    [SerializeField, Tooltip("Rotated by the lock turn")] private Transform cylinderMesh;

    [Header("Settings")]
    [Tooltip("Change to 1,0,0 or 0,1,0 if your Blender FBX rotates on a different axis")]
    [SerializeField] private Vector3 pickRotationAxis = Vector3.forward;
    [SerializeField, Tooltip("Axis the cylinder turns around, in its local space")] private Vector3 cylinderRotationAxis = Vector3.forward;
    [Tooltip("Change to -90 if the cylinder rotates counter-clockwise")]
    [SerializeField] private float cylinderMaxTurnAngle = 90f;
    [SerializeField, Tooltip("Degrees the pick shakes at most while the lock binds")] private float maxShakeAngle = 4f;

    [Header("HUD")]
    [SerializeField, Tooltip("The lockpick HUD beside this view in Minigames.prefab. Kept outside this object so it can fade out after the view hides")]
    private LockpickHud hud;

    private LockpickModel currentModel;
    private float bindingTimer;

    // Store the initial rotations so we don't ruin your Blender setup
    private Quaternion initialPickRotation;
    private Quaternion initialCylinderRotation;

    private void Awake()
    {
        if (pickMesh != null) initialPickRotation = pickMesh.localRotation;
        if (cylinderMesh != null) initialCylinderRotation = cylinderMesh.localRotation;
    }

    public void Begin(LockpickModel model, int lockLevel)
    {
        if (currentModel != null)
        {
            currentModel.Slipped -= OnSlipped;
        }

        currentModel = model;
        bindingTimer = 0f;
        if (hud != null) hud.Show(model, lockLevel);

        if (currentModel != null)
        {
            currentModel.Slipped += OnSlipped;
        }
    }

    public void End()
    {
        if (currentModel == null) return;

        if (hud != null) hud.Hide();
        currentModel.Slipped -= OnSlipped;
        currentModel = null;
    }

    private void OnDisable()
    {
        End();
    }

    private void LateUpdate()
    {
        if (currentModel == null) return;

        float pickAngle = currentModel.PickAngle;
        float cylinderAngle = currentModel.LockTurn * cylinderMaxTurnAngle;

        float pickShake = 0f;
        if (currentModel.IsBinding)
        {
            bindingTimer += Time.deltaTime;
            float shakeMagnitude = Mathf.Lerp(0f, maxShakeAngle, bindingTimer);
            pickShake = Mathf.Sin(Time.time * 60f) * shakeMagnitude;
        }
        else
        {
            bindingTimer = 0f;
        }

        pickMesh.localRotation = initialPickRotation * Quaternion.AngleAxis(pickAngle + pickShake, pickRotationAxis);
        cylinderMesh.localRotation = initialCylinderRotation * Quaternion.AngleAxis(cylinderAngle, cylinderRotationAxis);
    }

    private void OnSlipped()
    {
        Tween.PunchLocalPosition(lockRoot, new Vector3(0.05f, 0f, 0f), duration: 0.3f, frequency: 12);
    }
}
