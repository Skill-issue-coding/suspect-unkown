using UnityEngine;

// Makes a POI drain faster for the rest of the job once its safe has been opened, so guards visibly check
// the office more often (thriftshop-level.md 2.3). Lives on the POI. A runtime factor, separate from the
// security plan's DecayMultiplier; NPC code listening to Core's Safe keeps dependencies pointing one way
[RequireComponent(typeof(PointOfInterest))]
public class PoiSafeAlarm : MonoBehaviour
{
    [SerializeField, Tooltip("The safe whose opening raises this POI's importance")]
    private Safe safe;

    [SerializeField, Range(1f, 3f), Tooltip("How many times faster the POI drains once the safe has been opened")]
    private float alarmFactor = 1.5f;

    private PointOfInterest poi;

    private void Awake()
    {
        poi = GetComponent<PointOfInterest>();

        if (safe == null)
        {
            Debug.LogWarning($"{name} has no Safe; PoiSafeAlarm disabled", this);
            enabled = false;
        }
    }

    private void OnEnable() { if (safe != null) safe.Opened += OnOpened; }
    private void OnDisable() { if (safe != null) safe.Opened -= OnOpened; }

    private void OnOpened() => poi.RaiseAlarm(alarmFactor);
}
