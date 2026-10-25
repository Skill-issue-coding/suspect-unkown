using UnityEngine;

// The space one floor of a building fills, reported to the level's BuildingFloors (decision 26).
// Several colliders on this object make complex shapes, such as boxes running up a staircase;
// each one counts on its own. On the Volume layer, which collides with the player only, so NPCs
// walking between floors change nothing. Trigger events reach this through the player's
// CharacterController, as in PlayerConduct
[RequireComponent(typeof(Collider))]
public class FloorVolume : MonoBehaviour
{
    [SerializeField, Tooltip("Index of this floor in BuildingFloors' floor roots, bottom floor 0")]
    private int floor;

    private void OnValidate()
    {
        if (floor < 0) floor = 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out CharacterController _)) return;
        BuildingFloors floors = BuildingFloors.Instance;
        if (floors != null) floors.Enter(floor);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out CharacterController _)) return;
        BuildingFloors floors = BuildingFloors.Instance;
        if (floors != null) floors.Exit(floor);
    }
}
