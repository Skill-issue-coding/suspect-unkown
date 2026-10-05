using System.Collections.Generic;
using UnityEngine;

// Marks a Light as one NPC perception counts. Registers itself so PlayerLighting can sample
// without Find*, and answers the unshadowed light reaching a point using URP-like falloff, so
// gameplay light matches what the player sees. Shadows are the sampler's job. Point and Spot only.
[RequireComponent(typeof(Light))]
public class GameplayLight : MonoBehaviour
{
    private static readonly List<GameplayLight> all = new();
    public static IReadOnlyList<GameplayLight> All => all;

    private Light source;
    public Light Source => source;

    private void Awake()
    {
        source = GetComponent<Light>();
        if (source.type != LightType.Point && source.type != LightType.Spot)
        {
            Debug.LogWarning($"{name} GameplayLight supports Point and Spot only.", this);
            enabled = false;
        }
    }

    private void OnEnable() => all.Add(this);
    private void OnDisable() => all.Remove(this);

    public float Contribution(Vector3 point)
    {
        if (!source.enabled) return 0f;

        Vector3 toPoint = point - transform.position;
        float d2 = toPoint.sqrMagnitude;
        if (d2 < 0.0001f) d2 = 0.0001f; // avoids divide-by-zero NaN in the spot normalize

        float range = source.range;
        float range2 = range * range;
        if (d2 >= range2) return 0f;

        float ratio = d2 / range2;
        float smooth = Mathf.Clamp01(1f - ratio * ratio);
        float atten = smooth * smooth / Mathf.Max(d2, 0.01f);

        if (source.type == LightType.Spot)
        {
            float cosAngle = Vector3.Dot(transform.forward, toPoint / Mathf.Sqrt(d2));
            float cosOuter = Mathf.Cos(source.spotAngle * 0.5f * Mathf.Deg2Rad);
            float cosInner = Mathf.Cos(source.innerSpotAngle * 0.5f * Mathf.Deg2Rad);
            float spot = cosInner > cosOuter
                ? Mathf.Clamp01(Mathf.InverseLerp(cosOuter, cosInner, cosAngle))
                : (cosAngle >= cosOuter ? 1f : 0f);

            atten *= spot * spot;
        }

        return atten * source.intensity * source.color.grayscale;
    }
}
