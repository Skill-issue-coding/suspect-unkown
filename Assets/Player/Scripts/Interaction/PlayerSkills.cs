using UnityEngine;

// Placeholder until the Phase 7 skill shop and save exist: the player's skill levels, set in the
// inspector. Reserved by the roadmap; replace its source, keep its getters
public class PlayerSkills : MonoBehaviour
{
    [SerializeField, Range(0, 3), Tooltip("Highest lock level the player may pick. 0 cannot pick at all")]
    private int lockpickingLevel = 1;

    public int LockpickingLevel => lockpickingLevel;
}
