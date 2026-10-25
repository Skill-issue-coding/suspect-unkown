using UnityEngine;

// Player-facing vision cone (decision 21). The mesh is a static fan built once; 
// walls and low cover are cut in the shader from _RayData, which holds each ray's 
// wall, cover-start and shadow-end distances. Draws, never decides: perception 
// is the truth
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class NpcVisionCone : MonoBehaviour
{
    private const int FalloffResolution = 64;
    private static readonly int FalloffId = Shader.PropertyToID("_Falloff");
    private static readonly int AngleFalloffId = Shader.PropertyToID("_AngleFalloff");
    private static readonly int EdgeVisibilityId = Shader.PropertyToID("_EdgeVisibility");
    private static readonly int HalfFovId = Shader.PropertyToID("_HalfFov");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int RayDataId = Shader.PropertyToID("_RayData");
    private static readonly int RayCountId = Shader.PropertyToID("_RayCount");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int RangeScaleId = Shader.PropertyToID("_RangeScale");
    private static readonly int FovScaleId = Shader.PropertyToID("_FovScale");

    [Header("Shape")]
    [SerializeField, Range(8, 128), Tooltip("Rays across the cone. More rays give cleaner wall cuts and cost more raycasts, Changes need a restart")]
    private int rayCount = 48;

    [SerializeField, Range(0f, 0.1f), Tooltip("Metres above the NPC's feet the cone is drawn, so it does not flicker against the floor")]
    private float floorOffset = 0.02f;

    [SerializeField, Tooltip("What cuts the cone. Same layer as perception rays: Occluder")]
    private LayerMask occluderMask;

    [Header("Crouch-safe")]
    [SerializeField, Tooltip("Height of the player's head when crouched, in metres. Tune until the shaded strip matches where perception loses the head")]
    private float crouchHeadHeight = 0.9f;

    [SerializeField, Range(0.01f, 0.2f), Tooltip("How far into low cover the top probe starts, in metres")]
    private float topProbeDepth = 0.05f;

    [Header("Color")]
    [SerializeField, Tooltip("Cone color while Idle. Keep alpha low")]
    private Color idleColor = new Color(1f, 1f, 0.8f, 0.25f);

    [SerializeField, Tooltip("Cone color while Curious")]
    private Color curiousColor = new Color(1f, 0.9f, 0.3f, 0.3f);

    [SerializeField, Tooltip("Cone color while Investigating")]
    private Color investigatingColor = new Color(1f, 0.55f, 0.15f, 0.35f);

    [SerializeField, Tooltip("Cone color while Alerted")]
    private Color alertedColor = new Color(1f, 0.2f, 0.15f, 0.4f);

    [SerializeField, Min(0f), Tooltip("How fast the displayed color eases toward the state color. Higher is snappier")]
    private float colorSpeed = 6f;

    [Header("Gradient")]
    [SerializeField, Range(0f, 1f), Tooltip("Cone brightness at the far edge, so the end stays visible even where detection falls to zero")]
    private float edgeVisibility = 0.05f;

    [SerializeField, Range(0.05f, 1f), Tooltip("Width of the soft edge at walls and corners, in metres. About a body's width matches how perception turns partial")]
    private float edgeSoftness = 0.4f;

    private NpcSuspicion suspicion;
    private FloorHideable floorHideable;
    private MeshRenderer coneRenderer;
    private MaterialPropertyBlock block;
    private Color targetColor;
    private Color displayColor;
    private NpcPerception perception;
    private Mesh mesh;

    private float[] rayData;
    private Vector3[] localDirections;
    private float range;
    private Texture2D rayTexture;
    private Texture2D distanceTexture;
    private Texture2D angleTexture;

    private void Awake()
    {
        perception = GetComponentInParent<NpcPerception>();
        if (perception == null)
        {
            Debug.LogWarning($"{name} found no NpcPerception in its parents.", this);
            enabled = false;
            return;
        }

        suspicion = GetComponentInParent<NpcSuspicion>();
        floorHideable = GetComponentInParent<FloorHideable>();
        coneRenderer = GetComponent<MeshRenderer>();
        block = new MaterialPropertyBlock();
        targetColor = displayColor = idleColor;
    }

    private void OnEnable()
    {
        if (suspicion != null) suspicion.AlertStateChanged += OnAlertStateChanged;
        if (floorHideable != null) floorHideable.HideChanged += OnFloorHideChanged;
    }

    private void OnDisable()
    {
        if (suspicion != null) suspicion.AlertStateChanged -= OnAlertStateChanged;
        if (floorHideable != null) floorHideable.HideChanged -= OnFloorHideChanged;
    }

    // On another floor (decision 26) the cone is not drawn, so isVisible stays false and no rays are cast
    private void OnFloorHideChanged(float hide) => coneRenderer.enabled = hide < 1f;

    private void Start()
    {
        NpcArchetype archetype = perception.Archetype;
        if (archetype == null) { enabled = false; return; }

        distanceTexture = BakeCurve(archetype.EvaluateDistance);
        angleTexture = BakeCurve(archetype.EvaluateAngle);
        block.SetTexture(FalloffId, distanceTexture);
        block.SetTexture(AngleFalloffId, angleTexture);
        block.SetFloat(EdgeVisibilityId, edgeVisibility);

        range = archetype.ViewDistance;
        localDirections = BuildDirections(archetype.FovDegrees);
        mesh = BuildFan();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        rayData = new float[(rayCount + 1) * 4];
        rayTexture = new Texture2D(rayCount + 1, 1, TextureFormat.RGBAFloat, false, true)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        block.SetFloat(HalfFovId, archetype.FovDegrees * 0.5f * Mathf.Deg2Rad);
        block.SetTexture(RayDataId, rayTexture);
        block.SetFloat(RayCountId, rayCount);
    }

    private void LateUpdate()
    {
        // Off screen: nobody sees the cone, so cast nothing. isVisible comes from last frame's culling,
        // so a cone entering the screen shows last frame's walls for one frame, at the screen edge
        if (!coneRenderer.isVisible) return;

        Vector3 eye = perception.EyePosition;
        Vector3 low = transform.position + Vector3.up * crouchHeadHeight;
        Quaternion rotation = transform.rotation;

        // Rays stop at the scaled range; the data stays a fraction of the full range the fan was built at
        float reach = range * perception.RangeScale;

        for (int i = 0; i <= rayCount; i++)
        {
            Vector3 world = rotation * localDirections[i];

            float w = CastDistance(eye, world, reach);
            float s = w;
            float e = w;

            float coverStart = CastDistance(low, world, w);

            if (coverStart < w
                && TryCoverTop(low + world * (coverStart + topProbeDepth), eye.y, out float top)
                && top < eye.y)
            {
                s = coverStart;
                e = Mathf.Min(w, s * (eye.y - low.y) / (eye.y - top));
            }
            int p = i * 4;
            rayData[p] = w / range;
            rayData[p + 1] = s / range;
            rayData[p + 2] = e / range;
        }

        rayTexture.SetPixelData(rayData, 0);
        rayTexture.Apply(false, false);

        block.SetFloat(SoftnessId, edgeSoftness / range);
        displayColor = Color.Lerp(displayColor, targetColor, 1f - Mathf.Exp(-colorSpeed * Time.deltaTime));
        Color shownColor = displayColor;
        if (floorHideable != null) shownColor.a *= 1f - floorHideable.Hide;
        block.SetColor(BaseColorId, shownColor);
        block.SetFloat(RangeScaleId, perception.RangeScale);
        block.SetFloat(FovScaleId, perception.FovScale);
        coneRenderer.SetPropertyBlock(block);
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (distanceTexture != null) Destroy(distanceTexture);
        if (angleTexture != null) Destroy(angleTexture);
        if (rayTexture != null) Destroy(rayTexture);
    }

    // Bakes a 0..1 curve into a 1-pixel-tall linear texture the shader samples by its x coordinate
    private static Texture2D BakeCurve(System.Func<float, float> evaluate)
    {
        Texture2D texture = new Texture2D(FalloffResolution, 1, TextureFormat.R8, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        for (int x = 0; x < FalloffResolution; x++)
            texture.SetPixel(x, 0, new Color(evaluate(x / (FalloffResolution - 1f)), 0f, 0f));
        texture.Apply(false, true);
        return texture;
    }

    private Mesh BuildFan()
    {
        Vector3[] fanVertices = new Vector3[rayCount + 2];
        Vector2[] fanUvs = new Vector2[rayCount + 2];
        int[] triangles = new int[rayCount * 3];

        Vector3 lift = Vector3.up * floorOffset;

        fanVertices[0] = lift;
        for (int i = 0; i <= rayCount; i++)
        {
            Vector3 local = localDirections[i];
            fanVertices[i + 1] = local * range + lift;
            fanUvs[i + 1] = new Vector2(1f, 0f);
        }

        for (int i = 0; i < rayCount; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = i + 2;
        }

        Mesh fan = new Mesh { name = "VisionCone" };
        fan.SetVertices(fanVertices);
        fan.SetUVs(0, fanUvs);
        fan.SetTriangles(triangles, 0);
        fan.RecalculateBounds();
        return fan;
    }

    // Ray directions in the cone's own space, left edge to right edge. Built once: the fan and the rays
    // share them, so the drawn cone and the cast rays can never disagree
    private Vector3[] BuildDirections(float fovDegrees)
    {
        Vector3[] directions = new Vector3[rayCount + 1];
        float half = fovDegrees * 0.5f;
        float step = fovDegrees / rayCount;

        for (int i = 0; i <= rayCount; i++)
            directions[i] = Quaternion.Euler(0f, -half + i * step, 0f) * Vector3.forward;

        return directions;
    }

    // Distance to the first Occluder along a ray, or maxDistance if nothing is hit
    private float CastDistance(Vector3 origin, Vector3 direction, float maxDistance)
    {
        return Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance, occluderMask, QueryTriggerInteraction.Ignore)
            ? hit.distance
            : maxDistance;
    }

    // Height of the top of the low cover under a point, found by casting straight down from eye height
    private bool TryCoverTop(Vector3 insideCover, float fromHeight, out float top)
    {
        Vector3 origin = new Vector3(insideCover.x, fromHeight, insideCover.z);
        float depth = fromHeight - (transform.position.y + crouchHeadHeight) + 0.1f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, depth, occluderMask, QueryTriggerInteraction.Ignore))
        {
            top = hit.point.y;
            return true;
        }

        top = 0f;
        return false;
    }

    private void OnAlertStateChanged(NpcAlertState from, NpcAlertState to) => targetColor = to switch
    {
        NpcAlertState.Curious => curiousColor,
        NpcAlertState.Investigating => investigatingColor,
        NpcAlertState.Alerted => alertedColor,
        _ => idleColor,
    };
}
