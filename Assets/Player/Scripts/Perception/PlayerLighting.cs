using System.Collections.Generic;
using UnityEngine;

// Light level where the player stands, as NPC perception reads it. Tracks LightVolume triggers,
// counted per volume so multi-collider volumes work. The darkest volume wins; outside every volume is
// full light. Like PlayerConduct, a volume disabled while the player is inside never sends OnTriggerExit.
public class PlayerLighting : MonoBehaviour
{
    private readonly Dictionary<LightVolume, int> volumeCounts = new();

    public float Level
    {
        get
        {
            float darkest = 1f;
            foreach (LightVolume volume in volumeCounts.Keys)
                darkest = Mathf.Min(darkest, volume.Level);
            return darkest;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        LightVolume volume = other.GetComponent<LightVolume>();
        if (volume == null) return;

        volumeCounts.TryGetValue(volume, out int count);
        volumeCounts[volume] = count + 1;
        Debug.Log($"Light {Level:F2} ({volumeCounts.Count} volumes)", this);
    }

    private void OnTriggerExit(Collider other)
    {
        LightVolume volume = other.GetComponent<LightVolume>();
        if (volume == null || !volumeCounts.TryGetValue(volume, out int count)) return;

        if (count <= 1) volumeCounts.Remove(volume);
        else volumeCounts[volume] = count - 1;
        Debug.Log($"Light {Level:F2} ({volumeCounts.Count} volumes)", this);
    }
}
