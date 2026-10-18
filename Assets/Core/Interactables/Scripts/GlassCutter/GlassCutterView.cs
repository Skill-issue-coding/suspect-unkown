using PrimeTween;
using UnityEngine;
using UnityEngine.Rendering;

// The glass cutter minigame's screen: the 3D cutter and glass, the camera that films them into a
// RenderTexture, the dotted guide ring, and the background blur. Reads a GlassCutterModel and decides
// nothing. PlayerGlassCutting calls Begin and End, and asks TryGetAim where the pointer is on the glass
[RequireComponent(typeof(LineRenderer))]
public class GlassCutterView : MonoBehaviour
{
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
    [SerializeField, Tooltip("Global Volume with Depth of Field, weight 0. Kept outside this object so it can fade out after the view hides")]
    private Volume blurVolume;

    [SerializeField, Tooltip("Seconds to blur in or out")]
    private float blurDuration = 0.25f;

    [Header("Presentation")]
    [SerializeField, Tooltip("Camera that films the glass cutter into a RenderTexture; its field of view narrows as the cut progresses")]
    private Camera cutterCamera;

    [SerializeField, Tooltip("The RawImage rect that shows the camera's RenderTexture. The pointer is mapped through it, because the camera does not fill the screen")]
    private RectTransform screenRect;

    [SerializeField, Tooltip("Degrees the field of view narrows at a full 360 cut")]
    private float pushInFov = 6f;

    [SerializeField, Tooltip("How quickly the camera follows the cut progress. Higher is snappier")]
    private float pushInSharpness = 8f;

    [SerializeField, Tooltip("Scale punch on the cutter root when the circle finishes")]
    private float completePunch = 0.08f;

    private GlassCutterModel currentModel;
    private LineRenderer lineRenderer;
    private Camera uiCamera;
    private Material coneMaterial;
    private Color coneColor;
    private Tween blurFade;

    // Store the initial transforms so we don't ruin the Blender setup
    private Quaternion initialCutterRotation;
    private Quaternion initialConeRotation;
    private Vector3 initialConePosition;

    private Vector3 aimDirection;
    private float cutterRadius;
    private float baseFov;
    private float strainTimer;
    private bool cutClockwise;
    private bool celebrated;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();

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
        baseFov = cutterCamera.fieldOfView;
        aimDirection = conePivot.up;

        if (coneRenderer != null) coneMaterial = coneRenderer.material;

        cutterRadius = Vector3.Distance(cutterMesh.position, cutterTip.position);
        SetupDottedCircle();
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
        cutterCamera.fieldOfView = baseFov;

        FadeBlur(1f);
        currentModel.Misaligned += OnMisaligned;
    }

    public void End()
    {
        if (currentModel == null) return;

        FadeBlur(0f);
        currentModel.Misaligned -= OnMisaligned;
        currentModel = null;
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
        Vector2 viewport = new Vector2((local.x - rect.x) / rect.width, (local.y - rect.y) / rect.height);
        Ray ray = cutterCamera.ViewportPointToRay(viewport);

        Plane glassPlane = new Plane(transform.forward, cutterMesh.position);
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

        float signedAngle = cutClockwise ? -currentModel.CutAngle : currentModel.CutAngle;
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

        // Smooths only what is displayed: the camera eases toward the progress, the model stays exact.
        // Exponential smoothing, so the feel does not depend on frame rate
        float targetFov = baseFov - pushInFov * currentModel.Progress;
        float blend = 1f - Mathf.Exp(-pushInSharpness * Time.deltaTime);
        cutterCamera.fieldOfView = Mathf.Lerp(cutterCamera.fieldOfView, targetFov, blend);

        if (!celebrated && currentModel.IsComplete)
        {
            celebrated = true;
            SetConeColor(alignedColor);
            if (cutterRoot != null)
                Tween.PunchScale(cutterRoot, Vector3.one * completePunch, duration: 0.35f, frequency: 6);
        }
    }

    private void OnMisaligned()
    {
        Tween.PunchLocalPosition(coneVisual, new Vector3(shakeOffset, 0f, 0f), duration: 0.25f, frequency: 12);
    }

    private void SetConeColor(Color color)
    {
        if (coneMaterial == null || color == coneColor) return;

        coneColor = color;
        coneMaterial.color = color;
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
