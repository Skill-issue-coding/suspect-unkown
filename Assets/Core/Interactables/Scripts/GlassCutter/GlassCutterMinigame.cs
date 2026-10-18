using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.Rendering;

// The glass-cutting minigame screen: suspends PlayerMotor movement, reads aim and tension from
// PlayerInputs, draws the dotted guide ring at the cutter's radius, turns the cutter while aligned,
// and rattles the cone when the player tries to cut off-line
[RequireComponent(typeof(LineRenderer))]
public class GlassCutterMinigame : MonoBehaviour
{
    [Header("Player")]
    [SerializeField, Tooltip("Reads aim, tension, and cancel values. Auto-resolved from PlayerMotor if left empty")]
    private PlayerInputs playerInputs;

    [SerializeField, Tooltip("Suspended while the minigame is active so the player cannot walk away")]
    private PlayerMotor playerMotor;

    [SerializeField, Tooltip("Lock state restored to PlayerInputs when the minigame ends")]
    private bool lockCursorOnEnd = false;

    [Header("Hierarchy")]
    [SerializeField, Tooltip("The parent object holding the minigame meshes, used for the completion scale punch")]
    private Transform cutterRoot;

    [SerializeField, Tooltip("Rotated by the cut angle. Pivot must sit on the center suction cup")]
    private Transform cutterMesh;

    [SerializeField, Tooltip("Empty child on the cutting wheel at the end of the arm")]
    private Transform cutterTip;

    [SerializeField, Tooltip("Follows the cutter tip and rotates to aim at the pointer")]
    private Transform conePivot;

    [SerializeField, Tooltip("Child visual inside ConePivot that shakes and changes color when misaligned")]
    private Transform coneVisual;

    [SerializeField, Tooltip("Renderer on the cone visual, tinted by alignment state")]
    private Renderer coneRenderer;

    [Header("Cinemachine")]
    [SerializeField, Tooltip("The Cinemachine camera GameObject for this minigame; enabled only while cutting")]
    private GameObject cinemachineCamera;

    [Header("Settings")]
    [SerializeField, Tooltip("Change to 1,0,0 or 0,1,0 if your Blender FBX rotates on a different axis")]
    private Vector3 cutterRotationAxis = Vector3.forward;

    [SerializeField, Tooltip("Degrees per second the cutter turns while aligned and holding tension")]
    private float cutSpeed = 75f;

    [SerializeField, Tooltip("Half-angle of the cone in degrees: how close the aim must be to the dotted line tangent")]
    [Range(5f, 60f)]
    private float alignmentTolerance = 22f;

    [SerializeField, Tooltip("Minimum distance from the cutter tip to the pointer on the glass to register valid aiming")]
    private float minAimDistance = 0.05f;

    [SerializeField, Tooltip("True to cut clockwise, false for counter-clockwise")]
    private bool cutClockwise = true;

    [SerializeField, Tooltip("Degrees the cone shakes at most while holding tension out of alignment")]
    private float maxShakeAngle = 6f;

    [SerializeField, Tooltip("Position rattle distance on the cone when holding tension out of alignment")]
    private float shakeOffset = 0.02f;

    [Header("Look")]
    [SerializeField, Tooltip("Cone color while waiting for input")]
    private Color idleColor = new Color(0.62f, 0.68f, 0.8f, 0.6f);

    [SerializeField, Tooltip("Cone color while aligned with the dotted line")]
    private Color alignedColor = new Color(0.45f, 1f, 0.6f, 0.6f);

    [SerializeField, Tooltip("Cone color while holding tension out of alignment")]
    private Color warningColor = new Color(1f, 0.48f, 0.32f, 0.75f);

    [Header("Dotted Circle")]
    [SerializeField, Tooltip("Number of vertices in the dotted circle LineRenderer")]
    private int circleSegments = 128;

    [SerializeField, Tooltip("Thickness of the dotted guide line")]
    private float lineWidth = 0.02f;

    [SerializeField, Tooltip("How many dots repeat around the circle")]
    private float dotTiling = 30f;

    [SerializeField, Tooltip("Slight local Z offset so the dotted line sits cleanly on the glass without z-fighting")]
    private float lineZOffset = -0.005f;

    [Header("Background Blur")]
    [SerializeField, Tooltip("Global Volume with Depth of Field, weight 0. Fades in while cutting")]
    private Volume blurVolume;

    [SerializeField, Tooltip("Seconds to blur in or out")]
    private float blurDuration = 0.25f;

    [Header("Presentation")]
    [SerializeField, Tooltip("Camera that films the glass cutter; its field of view narrows as the cut progresses")]
    private Camera cutterCamera;

    [SerializeField, Tooltip("Degrees the field of view narrows at a full 360 cut")]
    private float pushInFov = 6f;

    [SerializeField, Tooltip("How quickly the camera follows the cut progress. Higher is snappier")]
    private float pushInSharpness = 8f;

    [SerializeField, Tooltip("Scale punch on the cutter root when the circle finishes")]
    private float completePunch = 0.08f;

    // Read by any HUD or controller: 0 = start, 1 = full circle cut
    public float Progress => Mathf.Clamp01(cutAngle / 360f);
    public bool IsComplete => cutAngle >= 360f;
    public bool IsAligned { get; private set; }
    public bool IsStraining { get; private set; }

    public event Action Completed;
    public event Action Canceled;
    public event Action Misaligned;

    private LineRenderer lineRenderer;
    private Quaternion initialCutterRotation;
    private Quaternion initialConeRotation;
    private Vector3 initialConePosition;
    private float cutterRadius;
    private float cutAngle;
    private float strainTimer;
    private float baseFov;
    private bool isActive;
    private bool celebrated;
    private Tween blurFade;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();

        if (cutterCamera == null)
            cutterCamera = Camera.main;

        if (cinemachineCamera == null)
        {
            Transform cm = transform.Find("GlassCutterCamera");
            if (cm != null) cinemachineCamera = cm.gameObject;
        }

        if (cinemachineCamera != null)
            cinemachineCamera.SetActive(false);

        if (playerMotor == null)
            playerMotor = FindAnyObjectByType<PlayerMotor>();

        if (playerInputs == null && playerMotor != null)
            playerInputs = playerMotor.GetComponent<PlayerInputs>();

        if (cutterMesh == null || cutterTip == null || conePivot == null || coneVisual == null)
        {
            Debug.LogWarning($"{name} is missing hierarchy references; the minigame is disabled", this);
            enabled = false;
            return;
        }

        // Store the initial transforms so we don't ruin your Blender setup
        initialCutterRotation = cutterMesh.localRotation;
        initialConeRotation = coneVisual.localRotation;
        initialConePosition = coneVisual.localPosition;

        if (cutterCamera != null)
            baseFov = cutterCamera.fieldOfView;

        cutterRadius = Vector3.Distance(cutterMesh.position, cutterTip.position);
        SetupDottedCircle();
    }

    // Can be called directly if references are assigned in the Inspector, or via Begin(inputs, motor)
    public void Begin(PlayerInputs inputs = null, PlayerMotor motor = null)
    {
        // Activate GameObject first so Awake runs synchronously if starting inactive
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (!enabled) return;

        if (inputs != null) playerInputs = inputs;
        if (motor != null) playerMotor = motor;

        if (playerInputs == null)
        {
            Debug.LogWarning($"{name} has no PlayerInputs reference; cannot start minigame", this);
            return;
        }

        isActive = true;
        cutAngle = 0f;
        strainTimer = 0f;
        celebrated = false;

        // Freeze player movement and free the cursor for aiming around the glass
        if (playerMotor != null)
            playerMotor.IsMovementSuspended = true;

        playerInputs.tension = false;
        playerInputs.cancel = false;
        playerInputs.SetCursorLocked(false);

        cutterMesh.localRotation = initialCutterRotation;
        coneVisual.localRotation = initialConeRotation;
        coneVisual.localPosition = initialConePosition;

        if (cutterCamera != null)
            cutterCamera.fieldOfView = baseFov;

        if (cinemachineCamera != null)
            cinemachineCamera.SetActive(true);

        FadeBlur(1f);
    }

    public void End()
    {
        if (!isActive) return;

        isActive = false;
        IsStraining = false;

        // Restore player movement and cursor state
        if (playerMotor != null)
            playerMotor.IsMovementSuspended = false;

        if (playerInputs != null)
            playerInputs.SetCursorLocked(lockCursorOnEnd);

        if (cinemachineCamera != null)
            cinemachineCamera.SetActive(false);

        FadeBlur(0f);
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        End();
    }

    private void LateUpdate()
    {
        if (!isActive || playerInputs == null || IsComplete) return;

        if (playerInputs.cancel)
        {
            playerInputs.cancel = false;
            End();
            Canceled?.Invoke();
            return;
        }

        // 1. Keep the cone pivot anchored to the cutting wheel at the end of the arm
        conePivot.position = cutterTip.position;

        // 2. Raycast the pointer aim position from PlayerInputs onto the glass plane
        Plane glassPlane = new Plane(transform.forward, cutterMesh.position);
        Ray mouseRay = cutterCamera.ScreenPointToRay(playerInputs.aim);

        Vector3 aimDirection = conePivot.up;
        bool hasValidMouse = false;

        if (glassPlane.Raycast(mouseRay, out float enterDist))
        {
            Vector3 mouseWorldPos = mouseRay.GetPoint(enterDist);
            Vector3 tipToMouse = mouseWorldPos - cutterTip.position;
            hasValidMouse = tipToMouse.magnitude >= minAimDistance;

            if (tipToMouse.sqrMagnitude > 0.0001f)
            {
                aimDirection = Vector3.ProjectOnPlane(tipToMouse, transform.forward).normalized;
                conePivot.rotation = Quaternion.LookRotation(transform.forward, aimDirection);
            }
        }

        // 3. Compare the cone's aim direction with the dotted circle's tangent at the cutter tip
        Vector3 radialDir = (cutterTip.position - cutterMesh.position).normalized;
        Vector3 tangentDir = cutClockwise
            ? Vector3.Cross(radialDir, transform.forward).normalized
            : Vector3.Cross(transform.forward, radialDir).normalized;

        float angleToTangent = Vector3.Angle(aimDirection, tangentDir);
        IsAligned = hasValidMouse && angleToTangent <= alignmentTolerance;

        // 4. Advance the cut or rattle the cone while tension (left mouse button) is held
        bool holdingTension = playerInputs.tension;

        if (holdingTension && IsAligned)
        {
            IsStraining = false;
            strainTimer = 0f;

            cutAngle = Mathf.Min(360f, cutAngle + cutSpeed * Time.deltaTime);
            float signedAngle = cutClockwise ? -cutAngle : cutAngle;
            cutterMesh.localRotation = initialCutterRotation * Quaternion.AngleAxis(signedAngle, cutterRotationAxis);
        }
        else if (holdingTension && !IsAligned)
        {
            // Trigger a punch on the frame the player starts pressing out of alignment
            if (!IsStraining)
            {
                IsStraining = true;
                Tween.PunchLocalPosition(coneVisual, new Vector3(shakeOffset, 0f, 0f), duration: 0.25f, frequency: 12);
                Misaligned?.Invoke();
            }

            strainTimer += Time.deltaTime;
        }
        else
        {
            IsStraining = false;
            strainTimer = 0f;
        }

        // 5. Apply continuous rotational and positional shake while straining out of alignment
        if (IsStraining)
        {
            float shakeMagnitude = Mathf.Lerp(maxShakeAngle * 0.5f, maxShakeAngle, Mathf.Clamp01(strainTimer * 4f));
            float coneShake = Mathf.Sin(Time.time * 60f) * shakeMagnitude;
            float offsetJitter = Mathf.Cos(Time.time * 55f) * (shakeOffset * 0.5f);

            coneVisual.localRotation = initialConeRotation * Quaternion.AngleAxis(coneShake, Vector3.forward);
            coneVisual.localPosition = initialConePosition + new Vector3(offsetJitter, 0f, 0f);
            SetConeColor(warningColor);
        }
        else
        {
            coneVisual.localRotation = initialConeRotation;
            coneVisual.localPosition = initialConePosition;
            SetConeColor(IsAligned ? alignedColor : idleColor);
        }

        // 6. Exponential camera FOV smoothing, matching LockpickView
        if (cutterCamera != null)
        {
            float targetFov = baseFov - pushInFov * Progress;
            float blend = 1f - Mathf.Exp(-pushInSharpness * Time.deltaTime);
            cutterCamera.fieldOfView = Mathf.Lerp(cutterCamera.fieldOfView, targetFov, blend);
        }

        // 7. Completion celebration
        if (!celebrated && IsComplete)
        {
            celebrated = true;
            SetConeColor(alignedColor);
            if (cutterRoot != null)
                Tween.PunchScale(cutterRoot, Vector3.one * completePunch, duration: 0.35f, frequency: 6);

            End();
            Completed?.Invoke();
        }
    }

    private void SetConeColor(Color color)
    {
        if (coneRenderer != null && coneRenderer.material != null)
            coneRenderer.material.color = color;
    }

    private void FadeBlur(float to)
    {
        if (blurVolume == null) return;

        blurFade.Stop();
        blurFade = Tween.Custom(blurVolume, blurVolume.weight, to, blurDuration,
            (volume, weight) => volume.weight = weight, Ease.OutQuad);
    }

    private void SetupDottedCircle()
    {
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = circleSegments;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.textureMode = LineTextureMode.Tile;

        if (lineRenderer.sharedMaterial == null || lineRenderer.sharedMaterial.name == "Default-Line")
        {
            Texture2D dotTex = new Texture2D(16, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point
            };

            for (int x = 0; x < 16; x++)
                dotTex.SetPixel(x, 0, x < 8 ? Color.white : Color.clear);

            dotTex.Apply();

            Material dottedMat = new Material(Shader.Find("Sprites/Default"))
            {
                mainTexture = dotTex,
                mainTextureScale = new Vector2(dotTiling, 1f)
            };
            lineRenderer.material = dottedMat;
        }

        float localRadius = cutterRadius / transform.lossyScale.x;
        Vector3 localCenter = transform.InverseTransformPoint(cutterMesh.position);

        for (int i = 0; i < circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * localRadius;
            float y = Mathf.Sin(angle) * localRadius;
            lineRenderer.SetPosition(i, localCenter + new Vector3(x, y, lineZOffset));
        }
    }
}