using UnityEngine;

// A flight of stairs between two floors, reported to the level's BuildingFloors (decision 26). While the
// player is on it, the floors fade by how far up the player is instead of switching at the top. Several
// colliders on this object make complex shapes, such as a landing; each one counts on its own. On the
// Volume layer, which collides with the player only. Trigger events reach this through the player's
// CharacterController, as in PlayerConduct
[RequireComponent(typeof(Collider))]
public class FloorStairs : MonoBehaviour
{
    [SerializeField, Tooltip("Index of the floor at the bottom of these stairs in BuildingFloors; they lead to the next floor up")]
    private int lowerFloor;

    private void OnValidate()
    {
        if (lowerFloor < 0) lowerFloor = 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out CharacterController _)) return;
        BuildingFloors floors = BuildingFloors.Instance;
        if (floors != null) floors.EnterStairs(lowerFloor);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.TryGetComponent(out CharacterController _)) return;
        BuildingFloors floors = BuildingFloors.Instance;
        if (floors != null) floors.ExitStairs();
    }
}
