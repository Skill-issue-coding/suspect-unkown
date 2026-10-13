using UnityEngine;
using PrimeTween;
using UnityEngine.Rendering;

// The lockpicking minigame's screen: the 3D lock and pick, the camera that films them, the HUD, and the
// background blur. Reads a LockpickModel and decides nothing. PlayerLockpicking calls Begin and End
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

    [Header("Background Blur")]
    [SerializeField, Tooltip("Global Volume with Depth of Field, weight 0. Kept outside this object so it can fade out after the view hides")]
    private Volume blurVolume;

    [SerializeField, Tooltip("Seconds to blur in or out")]
    private float blurDuration = 0.25f;

    [Header("Presentation")]
    [SerializeField, Tooltip("HUD prefab with hints, status and meter. Created once, as its own overlay canvas")]
    private LockpickHud hudPrefab;

    [SerializeField, Tooltip("Camera that films the lock; its field of view narrows as the lock turns")]
    private Camera lockCamera;

    [SerializeField, Tooltip("Degrees the field of view narrows at a full turn")]
    private float pushInFov = 8f;

    [SerializeField, Tooltip("How quickly the camera follows the turn. Higher is snappier")]
    private float pushInSharpness = 8f;

    [SerializeField, Tooltip("Scale punch on the lock when it opens")]
    private float openPunch = 0.08f;

    private Tween blurFade;
    private LockpickHud hud;
    private float baseFov;
    private bool celebrated;

    private LockpickModel currentModel;
    private float bindingTimer;

    // Store the initial rotations so we don't ruin your Blender setup
    private Quaternion initialPickRotation;
    private Quaternion initialCylinderRotation;

    private void Awake()
    {
        if (pickMesh != null) initialPickRotation = pickMesh.localRotation;
        if (cylinderMesh != null) initialCylinderRotation = cylinderMesh.localRotation;
        if (lockCamera != null) baseFov = lockCamera.fieldOfView;

        // Unparented on purpose: a root overlay canvas sizes itself to the screen wherever this view sits
        if (hudPrefab != null) hud = Instantiate(hudPrefab);
    }

    private void OnDestroy()
    {
        if (hud != null) Destroy(hud.gameObject);
    }

    public void Begin(LockpickModel model, int lockLevel)
    {
        if (currentModel != null)
        {
            currentModel.Slipped -= OnSlipped;
        }

        currentModel = model;
        FadeBlur(1f);
        bindingTimer = 0f;
        celebrated = false;
        if (lockCamera != null) lockCamera.fieldOfView = baseFov;
        if (hud != null) hud.Show(model, lockLevel);

        if (currentModel != null)
        {
            currentModel.Slipped += OnSlipped;
        }
    }

    public void End()
    {
        if (currentModel == null) return;

        FadeBlur(0f);
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

        // Smooths only what is displayed: the camera eases toward the turn, the model stays exact.
        // Exponential smoothing, so the feel does not depend on frame rate
        if (lockCamera != null)
        {
            float targetFov = baseFov - pushInFov * currentModel.LockTurn;
            float blend = 1f - Mathf.Exp(-pushInSharpness * Time.deltaTime);
            lockCamera.fieldOfView = Mathf.Lerp(lockCamera.fieldOfView, targetFov, blend);
        }

        if (!celebrated && currentModel.IsOpen)
        {
            celebrated = true;
            if (lockRoot != null) Tween.PunchScale(lockRoot, Vector3.one * openPunch, duration: 0.35f, frequency: 6);
        }
    }

    private void FadeBlur(float to)
    {
        if (blurVolume == null) return;

        blurFade.Stop();
        blurFade = Tween.Custom(blurVolume, blurVolume.weight, to, blurDuration,
            (volume, weight) => volume.weight = weight, Ease.OutQuad);
    }

    private void OnSlipped()
    {
        Tween.PunchLocalPosition(lockRoot, new Vector3(0.05f, 0f, 0f), duration: 0.3f, frequency: 12);
    }
}
