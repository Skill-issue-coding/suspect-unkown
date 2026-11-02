using UnityEngine;

// Draws every published noise as a ring on the floor that grows to how far the best ear in the level would
// hear it (noise radius × NpcHearing.HighestSensitivity), then fades. The line keeps one width in metres at
// any size (the NoiseRing shader). Shows straight-line reach and ignores walls, so it reads as loudness:
// real hearing still goes around corners along the NavMesh (decision 10).
// Reads NoiseBus and decides nothing. One per level scene; the rings are pooled quads built in Awake
[DisallowMultipleComponent]
public class NoiseRings : MonoBehaviour
{
    [Header("Rings")]
    [SerializeField, Tooltip("Material using the Suspect Unknown/Noise Ring shader. Its values are set per ring")]
    private Material ringMaterial;

    [SerializeField, Min(0.01f), Tooltip("Metres across the ring's line, the same at every ring size")]
    private float lineWidth = 0.12f;

    [SerializeField, Min(1), Tooltip("Rings that can show at once. When all are busy the oldest is reused")]
    private int poolSize = 16;

    [SerializeField, Min(0.05f), Tooltip("Seconds for a ring to grow to full size")]
    private float duration = 0.8f;

    [SerializeField, Min(0f), Tooltip("Seconds after a ring before another can start, unless the new noise is louder. Spaces out footsteps without hiding a sudden loud sound")]
    private float minInterval = 0.4f;

    [SerializeField, Range(0f, 1f), Tooltip("Fraction of the grow time at which the ring starts to fade")]
    private float fadeStart = 0.5f;

    [SerializeField, Tooltip("Metres above the noise position, so the ring does not flicker against the floor")]
    private float heightOffset = 0.03f;

    [SerializeField, Tooltip("Ring colour; its alpha is the opacity before fading")]
    private Color color = new Color(1f, 1f, 1f, 0.6f);

    // The NoiseRing shader's properties
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int WidthId = Shader.PropertyToID("_Width");

    // A quad faces -Z; turned 90° about X it lies flat, facing up
    private static readonly Quaternion Flat = Quaternion.Euler(90f, 0f, 0f);

    private Transform[] rings;
    private MeshRenderer[] renderers;
    // Metres, the final radius of each ring
    private float[] radii;
    // Seconds since each ring started; below 0 while the ring is free
    private float[] ages;
    // The ring the next noise takes. Rings are taken in turn, so a full pool reuses the oldest
    private int next;
    // Rings showing, so Update can bail when there are none
    private int active;
    // When the latest ring started and how big it grows, for minInterval
    private float lastRingTime = float.NegativeInfinity;
    private float lastRingRadius;
    private MaterialPropertyBlock block;

    private void Awake()
    {
        if (ringMaterial == null)
        {
            Debug.LogWarning($"{name} has no ring material; noise rings are disabled", this);
            enabled = false;
            return;
        }

        rings = new Transform[poolSize];
        renderers = new MeshRenderer[poolSize];
        radii = new float[poolSize];
        ages = new float[poolSize];
        block = new MaterialPropertyBlock();

        for (int i = 0; i < poolSize; i++)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "NoiseRing";
            // The primitive comes with a collider; a ring must never block anything or anyone
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(transform, false);
            ring.transform.rotation = Flat;

            MeshRenderer meshRenderer = ring.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = ringMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            ring.SetActive(false);
            rings[i] = ring.transform;
            renderers[i] = meshRenderer;
            ages[i] = -1f;
        }
    }

    // NoiseBus is static and outlives the scene, so a missed unsubscribe would call a destroyed component
    private void OnEnable() => NoiseBus.Published += OnNoise;

    private void OnDisable()
    {
        NoiseBus.Published -= OnNoise;
        if (rings == null) return;

        for (int i = 0; i < rings.Length; i++)
        {
            rings[i].gameObject.SetActive(false);
            ages[i] = -1f;
        }
        active = 0;
    }

    private void OnNoise(NoiseEvent noise)
    {
        float radius = noise.Radius * NpcHearing.HighestSensitivity();
        if (radius <= 0f) return;

        // Display only: every noise still reaches NpcHearing, the rings just skip the ones too close
        // together. A louder noise always gets its ring
        if (Time.time - lastRingTime < minInterval && radius <= lastRingRadius) return;
        lastRingTime = Time.time;
        lastRingRadius = radius;

        int i = next;
        next = (next + 1) % rings.Length;
        // A free ring adds to the count; reusing a busy one leaves it unchanged
        if (ages[i] < 0f) active++;

        radii[i] = radius;
        ages[i] = 0f;
        rings[i].position = noise.Position + Vector3.up * heightOffset;
        rings[i].gameObject.SetActive(true);
        Apply(i, 0f);
    }

    // Continuous while rings are showing: each grows and fades every frame until it is done
    private void Update()
    {
        if (active == 0) return;

        for (int i = 0; i < rings.Length; i++)
        {
            if (ages[i] < 0f) continue;

            ages[i] += Time.deltaTime;
            float t = ages[i] / duration;
            if (t >= 1f)
            {
                rings[i].gameObject.SetActive(false);
                ages[i] = -1f;
                active--;
                continue;
            }
            Apply(i, t);
        }
    }

    private void Apply(int i, float t)
    {
        // Ease out: fast at first, slowing toward the edge
        float grow = 1f - (1f - t) * (1f - t);
        float radius = radii[i] * grow;
        // The quad is 1 m across, so its scale is the diameter
        rings[i].localScale = Vector3.one * (radius * 2f);

        // Full opacity until fadeStart, then down to 0 at the end
        Color c = color;
        c.a *= 1f - Mathf.InverseLerp(fadeStart, 1f, t);
        block.SetColor(BaseColorId, c);
        // The shader needs the radius in metres to keep the line lineWidth wide at every size
        block.SetFloat(RadiusId, radius);
        block.SetFloat(WidthId, lineWidth);
        renderers[i].SetPropertyBlock(block);
    }
}
