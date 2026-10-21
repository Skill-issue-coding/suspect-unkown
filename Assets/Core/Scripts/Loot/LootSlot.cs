using UnityEngine;

// A hand-placed spot that holds its own loot (a glass display, later a smash table). LootSpawner
// decides whether it is live this run; a slot that is not live switches off whole, display and all
public class LootSlot : MonoBehaviour
{
    public void SetLive(bool live) => gameObject.SetActive(live);
}
