using UnityEngine;
using PrimeTween; 

public class LockpickView : MonoBehaviour
{
    [Header("Hierarchy")]
    [Tooltip("The parent object holding both meshes, used for the slip rattle shake")]
    [SerializeField] private Transform lockRoot;
    [SerializeField] private Transform pickMesh;
    [SerializeField] private Transform cylinderMesh;

    [Header("Settings")]
    [Tooltip("Change to 1,0,0 or 0,1,0 if your Blender FBX rotates on a different axis")]
    [SerializeField] private Vector3 pickRotationAxis = Vector3.forward;
    [SerializeField] private Vector3 cylinderRotationAxis = Vector3.forward;
    [Tooltip("Change to -90 if the cylinder rotates counter-clockwise")]
    [SerializeField] private float cylinderMaxTurnAngle = 90f;
    [SerializeField] private float maxShakeAngle = 4f;

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

    public void Begin(LockpickModel model)
    {
        if (currentModel != null)
        {
            currentModel.Slipped -= OnSlipped;
        }

        currentModel = model;
        bindingTimer = 0f;
        
        if (currentModel != null)
        {
            currentModel.Slipped += OnSlipped;
        }
    }

    public void End()
    {
        if (currentModel != null)
        {
            currentModel.Slipped -= OnSlipped;
            currentModel = null;
        }
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