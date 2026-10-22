using UnityEngine;

// A hand-placed spot that holds its own loot (a glass display, a smash table). LootSpawner decides
// whether it is live this run. Without dormant content a slot that is not live switches off whole,
// display and all; with it (a smash table) the slot stays in the level and shows that content instead
public class LootSlot : MonoBehaviour
{
    [SerializeField, Tooltip("Shown only while live: the loot and its interaction. Empty: the whole slot")]
    private GameObject liveContent;

    [SerializeField, Tooltip("Shown instead while not live, so the slot stays in the level as decor. Empty: nothing is shown")]
    private GameObject dormantContent;

    public void SetLive(bool live)
    {
        (liveContent != null ? liveContent : gameObject).SetActive(live);
        if (dormantContent != null) dormantContent.SetActive(!live);
    }

    private void OnValidate()
    {
        if (dormantContent != null && liveContent == null)
            Debug.LogWarning($"{name} has dormant content but no live content: a dormant slot would switch off whole, decor included", this);
    }
}
