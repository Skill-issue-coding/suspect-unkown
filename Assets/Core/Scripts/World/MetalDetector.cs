using System.Collections;
using UnityEngine;

// The jewelry store's walk-through metal detector. When the player walks through the scan trigger it
// beeps, blinks its two lamps yellow with realtime lights that light the area around them, and publishes
// an alarming sound on NoiseBus, so guards in hearing range come to investigate (decision 10). It never
// alerts on its own: how far a guard reacts from is set by the guard's hearing data, not here. Sits on
// the invisible trigger child, not on the solid posts. The alarm lights are visual only: they carry no
// GameplayLight, so they do not make the player easier for guards to see (decision 17)
[RequireComponent(typeof(Collider))]
public class MetalDetector : MonoBehaviour
{
    // Property names in URP's Lit shader. Looked up once, since string lookups cost every call
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Noise")]
    [SerializeField, Min(0f), Tooltip("Metres the beep carries along the NavMesh")]
    private float noiseRadius = 12f;

    [SerializeField, Range(0f, 1f), Tooltip("How alarming the beep is: 0 harmless, 1 unmistakable. Above 0 makes guards investigate")]
    private float alarm = 0.8f;

    [Header("Alarm")]
    [SerializeField, Min(0.1f), Tooltip("Seconds the lamps blink after a beep. The detector cannot beep again until it ends")]
    private float alarmDuration = 2f;

    [SerializeField, Min(0.05f), Tooltip("Seconds per on or off half of a blink")]
    private float blinkInterval = 0.2f;

    [Header("Lamps")]
    [SerializeField, Tooltip("The two lamp meshes on top of the detector")]
    private Renderer[] lamps;

    [SerializeField, Tooltip("Lamp color while no alarm is active")]
    private Color idleColor = Color.white;

    [SerializeField, Tooltip("Lamp color during the on half of a blink")]
    private Color alarmColor = new Color(1f, 0.85f, 0f);

    [SerializeField, Min(0f), Tooltip("Glow strength while idle. Below the bloom threshold (2.5) the lamps glow softly, with no halo")]
    private float idleIntensity = 1f;

    [SerializeField, Min(0f), Tooltip("Glow strength during the on half of a blink. Yellow needs more than white to look as bright")]
    private float alarmIntensity = 8f;

    [SerializeField, Tooltip("Realtime lights switched on with each blink, so the alarm lights its surroundings. Start them disabled")]
    private Light[] alarmLights;

    [Header("Audio")]
    [SerializeField, Tooltip("3D source the beep plays from")]
    private AudioSource audioSource;

    [SerializeField, Tooltip("Played when the player walks through")]
    private AudioClip alarmClip;

    // One material instance per lamp, recoloring them leaves every other object using the shared material untouched
    private Material[] lampMaterials;
    private Coroutine blinking;

    public bool IsAlarmActive => blinking != null;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;

        int layer = LayerMask.NameToLayer("Volume");
        if (layer < 0)
            Debug.LogWarning("Layer 'Volume' does not exist", this);
        else
            gameObject.layer = layer;
    }

    private void Awake()
    {
        if (!GetComponent<Collider>().isTrigger)
            Debug.LogWarning($"{name} collider isn't a trigger; the player will collide with it", this);

        // Missing audio or lamps only lose that feedback: guards still hear the noise, so the detector stays on
        if (audioSource == null || alarmClip == null)
            Debug.LogWarning($"{name} has no AudioSource or alarm clip; detector will be silent", this);

        CacheLampMaterials();
        ShowAlarm(false);
    }

    private void OnDisable()
    {
        // Disabling stops coroutines, puts the lamps back to idle instead of freezing them mid-blink
        blinking = null;
        ShowAlarm(false);
    }

    private void OnDestroy()
    {
        // Renderer.material creates instances that Unity does not clean up by itself
        if (lampMaterials == null) return;
        foreach (Material material in lampMaterials)
            if (material != null) Destroy(material);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Trigger callbacks run even on a disabled component, check
        if (!enabled) return;

        // Only the player's root has a CharacterController, so the bone perception colliders don't
        // each beep. Checking a Unity type keeps Core free of Player references
        if (!other.TryGetComponent(out CharacterController _)) return;

        if (IsAlarmActive) return;

        Beep(other.transform.position);
    }

    // Publishes from the player's feet, not this transform: NpcHearing only snaps a sound onto the
    // NavMesh within 1 m, and the detector's posts or a high pivot would leave it off the mesh
    private void Beep(Vector3 at)
    {
        if (audioSource != null && alarmClip != null)
            audioSource.PlayOneShot(alarmClip);

        NoiseBus.Publish(new NoiseEvent(at, noiseRadius, alarm));
        blinking = StartCoroutine(Blink());
    }

    // Alternates between the alarm flash and idle for alarmDuration, then settles on idle. Runs only
    // while the alarm is active, so an idle detector does no per-frame work
    private IEnumerator Blink()
    {
        WaitForSeconds half = new WaitForSeconds(blinkInterval);
        float endsAt = Time.time + alarmDuration;
        bool on = true;

        while (Time.time < endsAt)
        {
            ShowAlarm(on);
            on = !on;
            yield return half;
        }

        ShowAlarm(false);
        blinking = null;
    }

    // One place that decides what each state looks like, so the lamps and lights never disagree
    private void ShowAlarm(bool on)
    {
        if (on) SetLamps(alarmColor, alarmIntensity);
        else SetLamps(idleColor, idleIntensity);
        SetAlarmLights(on);
    }

    private void CacheLampMaterials()
    {
        if (lamps == null || lamps.Length == 0)
        {
            Debug.LogWarning($"{name} has no lamps assigned; the alarm will not blink", this);
            lampMaterials = new Material[0];
            return;
        }

        lampMaterials = new Material[lamps.Length];
        for (int i = 0; i < lamps.Length; i++)
        {
            if (lamps[i] == null) continue;
            lampMaterials[i] = lamps[i].material;
            lampMaterials[i].EnableKeyword("_EMISSION");
        }
    }

    private void SetLamps(Color color, float intensity)
    {
        if (lampMaterials == null) return;

        Color glow = color * intensity;
        foreach (Material material in lampMaterials)
        {
            if (material == null) continue;
            material.SetColor(BaseColorId, color);
            material.SetColor(EmissionColorId, glow);
        }
    }

    private void SetAlarmLights(bool on)
    {
        if (alarmLights == null) return;

        foreach (Light alarmLight in alarmLights)
            if (alarmLight != null) alarmLight.enabled = on;
    }
}