using PrimeTween;
using UnityEngine;

// The glass cutter minigame's own drawing: the 3D cutter and glass, the dotted guide ring, the progressive
// cut groove, and the aim cone. The camera, blur, push-in and completion punch are the MinigameScreen
// beside it. Reads a GlassCutterModel and decides nothing. PlayerGlassCutting calls Begin and End, and
// asks TryGetAim where the pointer is on the glass.
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(MinigameScreen))]
public class GlassCutterView : MonoBehaviour
{
    [Header("Hierarchy")]
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

    [Header("Settings")]
    [SerializeField, Tooltip("Change to 1,0,0 or 0,1,0 if your Blender FBX rotates on a different axis")]
    private Vector3 cutterRotationAxis = Vector3.forward;

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

    [Header("Dotted Circle & Cut Line")]
    [SerializeField, Tooltip("Number of dots / segments around the circle")]
    private int circleSegments = 72;

    [SerializeField, Tooltip("Thickness of the dotted guide line and cut line")]
    private float lineWidth = 0.02f;

    [SerializeField, Tooltip("Color and opacity of the dotted guide line")]
    private Color guideColor = new Color(1f, 1f, 1f, 0.35f);

    [SerializeField, Tooltip("Color and opacity of the solid cut line")]
    private Color cutColor = new Color(1f, 1f, 1f, 0.95f);

    [SerializeField, Tooltip("Slight local Z offset so the dotted line sits cleanly on the glass without z-fighting")]
    private float lineZOffset = -0.005f;

    [SerializeField, Tooltip("Optional separate LineRenderer for the solid cut groove. Auto-created on child 'CutLine' if empty")]
    private LineRenderer cutLineRenderer;

    [Header("Presentation")]
    [SerializeField, Tooltip("The RawImage rect that shows the camera's RenderTexture. The pointer is mapped through it, because the camera does not fill the screen")]
    private RectTransform screenRect;

    [Header("HUD")]
    [SerializeField, Tooltip("The glass cutter HUD beside this view in Minigames.prefab. Kept outside this object so it can fade out after the view hides")]
    private GlassCutterHud hud;

    private GlassCutterModel currentModel;
    private LineRenderer lineRenderer;
    private Camera cutterCamera;
    private Camera uiCamera;
    private Material coneMaterial;
    private Color coneColor;
    private SpriteRenderer coneSpriteRenderer;

    // Materials and textures created procedurally for cleanup
    private Material proceduralDottedMat;
    private Texture2D proceduralDotTex;
    private Material proceduralSolidMat;

    // Store the initial transforms
    private Quaternion initialCutterRotation;
    private Quaternion initialConeRotation;
    private Vector3 initialConePosition;

    private Vector3 aimDirection;
    private float cutterRadius;
    private float strainTimer;
    private bool cutClockwise;
    private bool celebrated;

    // Progressive cut line tracking
    private float startAngle;
    private float cutDirectionSign = 1f;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        cutterCamera = GetComponent<MinigameScreen>().Camera;

        if (cutterMesh == null || cutterTip == null || conePivot == null || coneVisual == null
            || cutterCamera == null || screenRect == null)
        {
            Debug.LogWarning($"{name} is missing hierarchy references; the minigame is disabled", this);
            enabled = false;
            return;
        }

        // A canvas in overlay mode takes no camera; the others map the pointer through their own
        Canvas canvas = screenRect.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera;

        initialCutterRotation = cutterMesh.localRotation;
        initialConeRotation = coneVisual.localRotation;
        initialConePosition = coneVisual.localPosition;
        aimDirection = conePivot.up;

        if (coneRenderer is SpriteRenderer sr)
            coneSpriteRenderer = sr;
        else if (coneRenderer != null)
            coneMaterial = coneRenderer.material;

        cutterRadius = Vector3.Distance(cutterMesh.position, cutterTip.position);
        SetupDottedCircle();
    }

    private void OnDestroy()
    {
        if (proceduralDottedMat != null) Destroy(proceduralDottedMat);
        if (proceduralDotTex != null) Destroy(proceduralDotTex);
        if (proceduralSolidMat != null) Destroy(proceduralSolidMat);
    }

    public void Begin(GlassCutterModel model, bool clockwise)
    {
        if (!enabled) return;

        if (currentModel != null)
            currentModel.Misaligned -= OnMisaligned;

        currentModel = model;
        cutClockwise = clockwise;
        strainTimer = 0f;
        celebrated = false;

        cutterMesh.localRotation = initialCutterRotation;
        coneVisual.localRotation = initialConeRotation;
        coneVisual.localPosition = initialConePosition;

        Vector3 localCenter = transform.InverseTransformPoint(cutterMesh.position);
        Vector3 startTipLocal = transform.InverseTransformPoint(cutterTip.position);
        Vector3 startDiff = startTipLocal - localCenter;
        startAngle = Mathf.Atan2(startDiff.y, startDiff.x);

        // Determine angular direction sign (+1 or -1) in 2D circle plane
        Quaternion testRot = initialCutterRotation * Quaternion.AngleAxis(cutClockwise ? 5f : -5f, cutterRotationAxis);
        cutterMesh.localRotation = testRot;
        Vector3 testTipLocal = transform.InverseTransformPoint(cutterTip.position);
        Vector3 testDiff = testTipLocal - localCenter;
        float testAngle = Mathf.Atan2(testDiff.y, testDiff.x);
        float deltaAngle = Mathf.DeltaAngle(startAngle * Mathf.Rad2Deg, testAngle * Mathf.Rad2Deg);
        cutDirectionSign = deltaAngle >= 0f ? 1f : -1f;

        // Restore initial rotation
        cutterMesh.localRotation = initialCutterRotation;

        if (cutLineRenderer != null)
        {
            cutLineRenderer.positionCount = 0;
            cutLineRenderer.enabled = false;
        }

        currentModel.Misaligned += OnMisaligned;
        if (hud != null) hud.Show(model);
    }

    public void End()
    {
        if (currentModel == null) return;

        if (hud != null) hud.Hide();
        currentModel.Misaligned -= OnMisaligned;
        currentModel = null;

        if (cutLineRenderer != null)
        {
            cutLineRenderer.positionCount = 0;
            cutLineRenderer.enabled = false;
        }
    }

    private void OnDisable()
    {
        End();
    }

    // Maps the pointer through the RawImage into the camera's view, lands it on the glass plane and
    // measures its direction from the cutter tip against the dotted line's tangent there. False when the
    // pointer is off the picture or too close to the tip to mean a direction
    public bool TryGetAim(Vector2 screenPoint, float minDistance, out float angleToTangent)
    {
        angleToTangent = 180f;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(screenRect, screenPoint, uiCamera, out Vector2 local))
            return false;

        Rect rect = screenRect.rect;
        if (!rect.Contains(local))
            return false;

        Vector2 viewport = new Vector2((local.x - rect.x) / rect.width, (local.y - rect.y) / rect.height);
        Ray ray = cutterCamera.ViewportPointToRay(viewport);

        Plane glassPlane = new Plane(-transform.forward, cutterMesh.position);
        if (!glassPlane.Raycast(ray, out float enterDistance)) return false;

        Vector3 tipToPointer = ray.GetPoint(enterDistance) - cutterTip.position;
        if (tipToPointer.sqrMagnitude > 0.0001f)
            aimDirection = Vector3.ProjectOnPlane(tipToPointer, transform.forward).normalized;

        if (tipToPointer.sqrMagnitude < minDistance * minDistance) return false;

        Vector3 radialDir = (cutterTip.position - cutterMesh.position).normalized;
        Vector3 tangentDir = cutClockwise
            ? Vector3.Cross(radialDir, transform.forward).normalized
            : Vector3.Cross(transform.forward, radialDir).normalized;

        angleToTangent = Vector3.Angle(aimDirection, tangentDir);
        return true;
    }

    private void LateUpdate()
    {
        if (currentModel == null) return;

        float signedAngle = cutClockwise ? currentModel.CutAngle : -currentModel.CutAngle;
        cutterMesh.localRotation = initialCutterRotation * Quaternion.AngleAxis(signedAngle, cutterRotationAxis);

        // The cone rides the cutting wheel and points where the pointer is
        conePivot.position = cutterTip.position;
        conePivot.rotation = Quaternion.LookRotation(transform.forward, aimDirection);

        if (currentModel.IsStraining)
        {
            strainTimer += Time.deltaTime;
            float shakeMagnitude = Mathf.Lerp(maxShakeAngle * 0.5f, maxShakeAngle, Mathf.Clamp01(strainTimer * 4f));
            float coneShake = Mathf.Sin(Time.time * 60f) * shakeMagnitude;
            float offsetJitter = Mathf.Cos(Time.time * 55f) * (shakeOffset * 0.5f);

            coneVisual.localRotation = initialConeRotation * Quaternion.AngleAxis(coneShake, Vector3.forward);
            coneVisual.localPosition = initialConePosition + new Vector3(offsetJitter, 0f, 0f);
            SetConeColor(warningColor);
        }
        else
        {
            strainTimer = 0f;
            coneVisual.localRotation = initialConeRotation;
            coneVisual.localPosition = initialConePosition;
            SetConeColor(currentModel.IsAligned ? alignedColor : idleColor);
        }

        // Progressive cut groove line
        UpdateCutLine();

        if (!celebrated && currentModel.IsComplete)
        {
            celebrated = true;
            lineRenderer.enabled = false;
            SetConeColor(alignedColor);
        }
    }

    private void UpdateCutLine()
    {
        if (cutLineRenderer == null || currentModel == null) return;

        float cutAngleDeg = currentModel.CutAngle;
        if (cutAngleDeg <= 0.5f)
        {
            cutLineRenderer.positionCount = 0;
            cutLineRenderer.enabled = false;
            return;
        }

        cutLineRenderer.enabled = true;
        int arcPoints = Mathf.Max(2, Mathf.CeilToInt((cutAngleDeg / 360f) * circleSegments) + 1);
        cutLineRenderer.positionCount = arcPoints;

        float localRadius = cutterRadius / transform.lossyScale.x;
        Vector3 localCenter = transform.InverseTransformPoint(cutterMesh.position);
        float totalAngleRad = (cutAngleDeg * Mathf.Deg2Rad) * cutDirectionSign;

        for (int i = 0; i < arcPoints; i++)
        {
            float t = (float)i / (arcPoints - 1);
            float angle = startAngle + totalAngleRad * t;
            float x = Mathf.Cos(angle) * localRadius;
            float y = Mathf.Sin(angle) * localRadius;
            cutLineRenderer.SetPosition(i, localCenter + new Vector3(x, y, lineZOffset - 0.001f));
        }
    }

    private void OnMisaligned()
    {
        Tween.PunchLocalPosition(coneVisual, new Vector3(shakeOffset, 0f, 0f), duration: 0.25f, frequency: 12);
    }

    private void SetConeColor(Color color)
    {
        if (color == coneColor) return;
        coneColor = color;

        if (coneSpriteRenderer != null)
            coneSpriteRenderer.color = color;
        else if (coneMaterial != null)
            coneMaterial.color = color;
    }

    private void SetupDottedCircle()
    {
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = circleSegments;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = guideColor;
        lineRenderer.endColor = guideColor;
        lineRenderer.textureMode = LineTextureMode.RepeatPerSegment;

        if (lineRenderer.sharedMaterial == null || lineRenderer.sharedMaterial.name == "Default-Line")
        {
            proceduralDotTex = new Texture2D(32, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            for (int x = 0; x < 32; x++)
            {
                // Soft circular dot in the first ~40% of the segment, transparent in the rest
                float alpha = (x < 14) ? Mathf.Sin((x / 14f) * Mathf.PI) : 0f;
                proceduralDotTex.SetPixel(x, 0, new Color(1f, 1f, 1f, alpha));
            }
            proceduralDotTex.Apply();

            proceduralDottedMat = new Material(Shader.Find("Sprites/Default"))
            {
                name = "GlassCutter_DottedGuide",
                mainTexture = proceduralDotTex
            };
            lineRenderer.material = proceduralDottedMat;
        }

        // Setup cutLineRenderer if not assigned in inspector
        if (cutLineRenderer == null)
        {
            Transform existingCut = transform.Find("CutLine");
            if (existingCut != null)
            {
                cutLineRenderer = existingCut.GetComponent<LineRenderer>();
            }
            else
            {
                GameObject cutObj = new GameObject("CutLine");
                cutObj.transform.SetParent(transform, false);
                cutObj.layer = gameObject.layer;
                cutLineRenderer = cutObj.AddComponent<LineRenderer>();
            }
        }

        cutLineRenderer.useWorldSpace = false;
        cutLineRenderer.loop = false;
        cutLineRenderer.startWidth = lineWidth * 1.05f;
        cutLineRenderer.endWidth = lineWidth * 1.05f;
        cutLineRenderer.startColor = cutColor;
        cutLineRenderer.endColor = cutColor;
        cutLineRenderer.textureMode = LineTextureMode.Stretch;
        cutLineRenderer.sortingOrder = lineRenderer.sortingOrder + 1;

        if (cutLineRenderer.sharedMaterial == null || cutLineRenderer.sharedMaterial.name == "Default-Line")
        {
            proceduralSolidMat = new Material(Shader.Find("Sprites/Default"))
            {
                name = "GlassCutter_SolidCut",
                mainTexture = Texture2D.whiteTexture
            };
            cutLineRenderer.material = proceduralSolidMat;
        }

        cutLineRenderer.positionCount = 0;
        cutLineRenderer.enabled = false;

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
