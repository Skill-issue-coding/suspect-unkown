using UnityEngine;

// The sound of the player taking loot, items and cash alike. Hears LootPickup.Taken, as PickupToast does,
// so it needs no reference to any pickup or to the player. Lives on the HUD, not on the pickup: Take()
// hides the pickup straight after announcing it, which would cut off a sound playing there
[RequireComponent(typeof(AudioSource))]
public class PickupAudio : MonoBehaviour
{
    [SerializeField, Tooltip("Played once when the player takes an item or cash")]
    private AudioClip takenClip;

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;

        if (takenClip == null)
        {
            Debug.LogWarning($"{name} has no pickup clip; taking loot will be silent", this);
            enabled = false;
        }
    }

    // Taken is static and outlives the scene, so a missed unsubscribe would call a destroyed component
    // after the level reloads
    private void OnEnable() => LootPickup.Taken += OnTaken;
    private void OnDisable() => LootPickup.Taken -= OnTaken;

    private void OnTaken(LootPickup pickup) => source.PlayOneShot(takenClip);
}
