using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// In-world suspicion telegraph (concept 6.5). Hidden at zero, so a calm 
// room shows nothing. Icon and colour follow AlertStateChanged; the fill 
// follows Suspicion every frame. Billboards by copying the fixed camera's 
// rotation.
[RequireComponent(typeof(CanvasGroup))]
public class NpcSuspicionIndicator : MonoBehaviour
{
    [SerializeField] private Image fill;
    [SerializeField] private TMP_Text icon;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform arrowPivot;
    [SerializeField] private float turnSpeed = 720f;
    private NpcPerception perception;
    private Vector3 lastKnownPosition;
    private bool hasTarget;

    [Header("State colors")]
    [SerializeField] private Color idleColor = Color.gray;
    [SerializeField] private Color curiousColor = Color.yellow;
    [SerializeField] private Color investigatingColor = new Color(1f, 0.55f, 0f);
    [SerializeField] private Color alertedColor = Color.red;

    [Header("Smoothing")]
    [SerializeField] private float fillSmoothTime = 0.1f;
    [SerializeField] private float turnSmoothTime = 0.12f;
    [SerializeField] private float fadeSpeed = 4f;

    private float displayedFill;
    private float fillVelocity;
    private float currentAngle;
    private float angleVelocity;

    private NpcSuspicion suspicion;
    private Transform cameraTransform;

    private void Awake()
    {
        suspicion = GetComponentInParent<NpcSuspicion>();
        if (suspicion == null)
        {
            Debug.LogWarning($"{name} has no NpcSuspicion; NpcSuspicionIndicator disabled.", this);
            enabled = false;
        }

        perception = GetComponentInParent<NpcPerception>();
        if (perception == null)
        {
            Debug.LogWarning($"{name} has no NpcPerception; indicator rotation disabled.", this);
            hasTarget = false;
        }
        else if (arrowPivot == null)
        {
            Debug.LogWarning($"{name} has no ArrowPivot; indicator rotation disabled.", this);
            hasTarget = false;
        }
        else
            hasTarget = true;

        group = GetComponent<CanvasGroup>();
    }

    private void OnEnable() => suspicion.AlertStateChanged += OnAlertStateChanged;
    private void OnDisable()
    {
        if (suspicion != null)
            suspicion.AlertStateChanged -= OnAlertStateChanged;
    }

    private void Start()
    {
        if (Camera.main == null)
        {
            Debug.LogWarning($"{name} found no main camera; indicator disabled.", this);
            enabled = false;
            return;
        }

        cameraTransform = Camera.main.transform;
        ApplyState(suspicion.State);
    }

    private void LateUpdate()
    {
        displayedFill = Mathf.SmoothDamp(displayedFill, suspicion.Suspicion, ref fillVelocity, fillSmoothTime);
        fill.fillAmount = displayedFill;

        group.alpha = suspicion.Suspicion > 0f || suspicion.State != NpcAlertState.Idle ? 1f : 0f;

        if (perception.TryGetLatest(out Observation observation))
        {
            lastKnownPosition = observation.playerPosition;
            hasTarget = true;
        }

        if (hasTarget)
        {
            Vector3 toTarget = lastKnownPosition - transform.position;

            float x = Vector3.Dot(toTarget, cameraTransform.right);
            float y = Vector3.Dot(toTarget, cameraTransform.up);
            float targetAngle = -Mathf.Atan2(x, y) * Mathf.Rad2Deg;
            currentAngle = Mathf.SmoothDampAngle(currentAngle, targetAngle, ref angleVelocity, turnSmoothTime);
            arrowPivot.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
        }

        bool visible = displayedFill > 0.001f || suspicion.State != NpcAlertState.Idle;
        group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, fadeSpeed * Time.deltaTime);
    }

    private void OnAlertStateChanged(NpcAlertState from, NpcAlertState to) => ApplyState(to);

    private void ApplyState(NpcAlertState state)
    {
        icon.text = state switch
        {
            NpcAlertState.Idle => "",
            NpcAlertState.Curious => "?",
            NpcAlertState.Investigating => "?!",
            NpcAlertState.Alerted => "!",
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };

        Color color = state switch
        {
            NpcAlertState.Idle => idleColor,
            NpcAlertState.Curious => curiousColor,
            NpcAlertState.Investigating => investigatingColor,
            NpcAlertState.Alerted => alertedColor,
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };

        icon.color = color;
        fill.color = color;
    }
}
